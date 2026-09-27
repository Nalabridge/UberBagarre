import os, re, pickle, shutil, hashlib, collections, json, sys
import numpy as np
from unitymesh import parse_mesh, vertex_arrays, build_mesh_doc, yaml_str
from unbatch import extract, fix_winding, aabbs
from matparse import parse_mat
import convmat
from convmat import guid_of, convert, write_mat, Out, finish_standard, standard_mode

SRC='src'; OUT='out/Schedule1'
if os.path.exists(OUT): shutil.rmtree(OUT)
for d in ('Carte','Maillages','Maillages/Decoupes','Materiaux','Textures','Terrain','Vegetation','Shaders'):
    os.makedirs(os.path.join(OUT,d),exist_ok=True)
BUILTIN={'0000000000000000e000000000000000','0000000000000000f000000000000000'}

I=pickle.load(open('index.pkl','rb')); byid,comps=I['byid'],I['comps']
D=pickle.load(open('scene.pkl','rb')); GO,TR=D['GO'],D['TR']
X=pickle.load(open('world.pkl','rb')); W,children,roots,go2tr=X['W'],X['children'],X['roots'],X['go2tr']
K=pickle.load(open('keep.pkl','rb')); g2p=K['guid2path']
texname={g:os.path.basename(p) for g,p in g2p.items()}

def name(t): return GO[TR[t]['go']]['name'].strip("'")
# ---------------------------------------------------------------- 1. objets gardes
keep_roots=[r for r in roots if name(r) in ('Map','@Properties','@Businesses')]
keep_tr=[]; stack=list(keep_roots)
while stack:
    t=stack.pop()
    if byid[t][0]=='224': continue          # interfaces (textes TMP, ecrans) : dehors
    keep_tr.append(t); stack+=children.get(t,[])
keep_tr_set=set(keep_tr)
keep_go={TR[t]['go'] for t in keep_tr}
activate=set()
for r in keep_roots:
    activate.add(TR[r]['go'])
    for c in children.get(r,[]):
        activate.add(TR[c]['go'])
        if name(r)=='Map':
            for cc in children.get(c,[]): activate.add(TR[cc]['go'])
        if name(c)=='RV':
            for cc in children.get(c,[]):
                if name(cc)=='RV': activate.add(TR[cc]['go'])
print('objets',len(keep_go),'districts actives',len(activate))

# ---------------------------------------------------------------- 2. materiaux
mat_cache={}
def material(g):
    if g in mat_cache: return mat_cache[g]
    r=None
    if g in g2p and g2p[g].endswith('.mat'):
        m=parse_mat(open(g2p[g],encoding='utf-8').read()); m['_texnames']=texname
        r=('conv',convert(m))
    else: r=('missing',None)
    mat_cache[g]=r; return r

def comp_list(go):
    s=byid[go][1]
    return re.findall(r'component: \{fileID: (-?\d+)\}',s)
def mref(s,key):
    m=re.search(key+r': \{fileID: (-?\d+)(?:, guid: ([0-9a-f]+), type: \d)?\}',s)
    return (m.group(1),m.group(2)) if m else ('0',None)
def mesh_ok(ref):
    fid,g=ref
    if fid=='0': return False
    if g in BUILTIN: return True
    return g in g2p
def mats_of(s):
    blk=s.split('m_Materials:',1)[1].split('\n  m_',1)[0] if 'm_Materials:' in s else ''
    return re.findall(r'\{fileID: (-?\d+)(?:, guid: ([0-9a-f]+), type: \d)?\}',blk)

KEEP={'4','33','23','65','64','136','135','205','108','218','154'}
kept_comp={}      # go -> [component ids]
split_jobs=collections.defaultdict(list)   # combined guid -> [(renderer id, filter id, first, count, go)]
missing_mats=collections.defaultdict(collections.Counter)
drop_stats=collections.Counter()
for go in keep_go:
    lst=comp_list(go); cls={c:byid[c][0] for c in lst if c in byid}
    mf=[c for c in lst if cls.get(c)=='33']; mr=[c for c in lst if cls.get(c)=='23']
    render_ok=False
    if mf and mr:
        fs=byid[mf[0]][1]; rs=byid[mr[0]][1]
        ref=mref(fs,'m_Mesh')
        mats=mats_of(rs)
        states=[material(g)[0]=='missing' or material(g)[1] is not None for f,g in mats if g]
        if mesh_ok(ref) and mats and any(states):
            render_ok=True
            for f,g in mats:
                if g and material(g)[0]=='missing': missing_mats[g][GO[go]['name']]+=1
        else: drop_stats['rendu supprime']+=1
    out=[]
    for c in lst:
        k=cls.get(c)
        if k not in KEEP: 
            if k: drop_stats['classe '+k]+=1
            continue
        s=byid[c][1]
        if k in ('33','23') and not render_ok: continue
        if k in ('65','64','136','135'):
            if re.search(r'm_IsTrigger: 1',s): drop_stats['declencheur']+=1; continue
            if k=='64' and not mesh_ok(mref(s,'m_Mesh')): continue
        if k=='108':
            t=int(re.search(r'm_Type: (\d)',s).group(1))
            if t in (1,3): continue
        out.append(c)
    kept_comp[go]=out
    if render_ok:
        fs=byid[mf[0]][1]; rs=byid[mr[0]][1]; ref=mref(fs,'m_Mesh')
        p=g2p.get(ref[1],'')
        sc=int(re.search(r'subMeshCount: (\d+)',rs).group(1)); fsm=int(re.search(r'firstSubMesh: (\d+)',rs).group(1))
        if 'Combined Mesh (root_ scene)' in p and sc>0:
            split_jobs[ref[1]].append((mr[0],mf[0],fsm,sc,go))
print('suppressions',dict(drop_stats))
print('materiaux manquants',len(missing_mats))

# ---------------------------------------------------------------- 3. maillages redecoupes
split_ref={}   # filter id -> (fileID, guid)
split_count=0; flipped=0
for cg,jobs in sorted(split_jobs.items(),key=lambda x:g2p[x[0]]):
    src=g2p[cg]; txt=open(src,encoding='utf-8').read(); body=txt.split('\n',3)[3]
    m=parse_mesh(body); lay,arrs=vertex_arrays(m)
    base=os.path.basename(src)[:-6].replace('Combined Mesh (root_ scene)','Decoupe').strip()
    ng=guid_of('mesh:'+cg)
    docs=[]
    for k,(rid,fid,first,count,go) in enumerate(jobs):
        M=W[go2tr[go]][0]
        r=extract(m,lay,arrs,first,count,M)
        if fix_winding(r,1): flipped+=1
        ab,tot=aabbs(r)
        data=b''.join(r['arrs'][s].tobytes() for s in sorted(r['arrs']))
        fileid=4300000+k
        docs.append(build_mesh_doc(body,fileid,GO[go]['name'].strip("'"),r['subs'],r['vcount'],data,r['indices'],r['fmt'],ab,tot))
        split_ref[fid]=(str(fileid),ng)
        split_count+=1
    path=os.path.join(OUT,'Maillages/Decoupes',base+'.asset')
    with open(path,'w',encoding='utf-8',newline='\n') as f:
        f.write('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+''.join(d if d.endswith('\n') else d+'\n' for d in docs))
    with open(path+'.meta','w',newline='\n') as f:
        f.write('fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 4300000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'%ng)
print('maillages decoupes',split_count,'retournes',flipped)

# ---------------------------------------------------------------- 4. documents de la scene
out_ids=set(keep_go)
for go,lst in kept_comp.items(): out_ids.update(lst)
used_mesh=set(); used_mat=set(); used_terrain=set()
def fix_refs(s):
    def local(mm):
        return mm.group(0) if (mm.group(1) in out_ids or mm.group(1)=='0') else '{fileID: 0}'
    s=re.sub(r'\{fileID: (-?\d+)\}',local,s)
    def ext(mm):
        g=mm.group(2)
        return mm.group(0) if (g in BUILTIN or g in out_guids) else '{fileID: 0}'
    return re.sub(r'\{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: (\d)\}',ext,s)
# les guids sortants sont connus apres coup : on accumule les textes puis on nettoie
pending=[]
tr_of_go={TR[t]['go']:t for t in keep_tr}
order=[i for i,(c,s) in byid.items()]  # ordre du fichier
for i in order:
    c,s=byid[i]
    if c=='1' and i in keep_go:
        lst=[x for x in comp_list(i) if x in set(kept_comp[i]) or x==tr_of_go.get(i)]
        s=re.sub(r'  m_Component:\n(  - component: \{fileID: -?\d+\}\n)*','  m_Component:\n'+''.join('  - component: {fileID: %s}\n'%x for x in lst),s)
        s=re.sub(r'm_Layer: \d+','m_Layer: 0',s)
        s=re.sub(r'm_TagString: .*','m_TagString: Untagged',s)
        s=re.sub(r'm_StaticEditorFlags: \d+','m_StaticEditorFlags: 86',s)
        if i in activate: s=re.sub(r'm_IsActive: \d','m_IsActive: 1',s)
        pending.append((c,i,s)); continue
    if c in ('4',) and i in keep_tr_set:
        def kids(mm):
            ids=re.findall(r'\{fileID: (-?\d+)\}',mm.group(0))
            ids=[x for x in ids if x in keep_tr_set]
            return '  m_Children:\n'+''.join('  - {fileID: %s}\n'%x for x in ids) if ids else '  m_Children: []\n'
        s=re.sub(r'  m_Children:(?: \[\])?\n(  - \{fileID: -?\d+\}\n)*',kids,s)
        pending.append((c,i,s)); continue
    if c=='1' or c=='4': continue
    gm=re.search(r'm_GameObject: \{fileID: (-?\d+)\}',s)
    if not gm or gm.group(1) not in keep_go or i not in set(kept_comp[gm.group(1)]): continue
    if c=='33':
        if i in split_ref:
            f,g=split_ref[i]; s=re.sub(r'm_Mesh: \{[^}]*\}','m_Mesh: {fileID: %s, guid: %s, type: 2}'%(f,g),s)
        else:
            used_mesh.add(mref(s,'m_Mesh')[1])
    elif c=='23':
        for f,g in mats_of(s):
            if g: used_mat.add(g)
        s=re.sub(r'm_StaticBatchInfo:\n    firstSubMesh: \d+\n    subMeshCount: \d+','m_StaticBatchInfo:\n    firstSubMesh: 0\n    subMeshCount: 0',s)
        s=re.sub(r'm_LightmapIndex: \d+','m_LightmapIndex: 65535',s)
        s=re.sub(r'm_LightmapIndexDynamic: \d+','m_LightmapIndexDynamic: 65535',s)
        s=re.sub(r'm_StaticBatchRoot: \{[^}]*\}','m_StaticBatchRoot: {fileID: 0}',s)
        # materiau a supprimer dans un rendu mixte -> invisible
        def mslot(mm):
            g=mm.group(2)
            st=material(g)
            if st[0]=='conv' and st[1] is None: return '{fileID: 2100000, guid: %s, type: 2}'%guid_of('mat:invisible')
            return mm.group(0)
        head,sep,tail=s.partition('m_Materials:')
        blk,sep2,rest=tail.partition('\n  m_')
        blk=re.sub(r'\{fileID: (-?\d+), guid: ([0-9a-f]+), type: \d\}',mslot,blk)
        s=head+sep+blk+sep2+rest
    elif c=='64':
        gid=gm.group(1)
        ref=mref(s,'m_Mesh')
        if 'Combined Mesh' in g2p.get(ref[1],''):
            f2=[x for x in kept_comp[gid] if byid[x][0]=='33' and x in split_ref]
            if f2:
                f,g=split_ref[f2[0]]; s=re.sub(r'm_Mesh: \{[^}]*\}','m_Mesh: {fileID: %s, guid: %s, type: 2}'%(f,g),s)
            else: continue
        else: used_mesh.add(ref[1])
    elif c=='108':
        s=re.sub(r'(m_Shadows:\n    m_Type: )\d','\\g<1>0',s)
        s=re.sub(r'm_Lightmapping: \d','m_Lightmapping: 4',s)
        s=re.sub(r'lightmapBakeType: \d','lightmapBakeType: 4',s)
        s=re.sub(r'isBaked: \d','isBaked: 0',s)
        s=re.sub(r'(m_CullingMask:\n    serializedVersion: 2\n    m_Bits: )\d+','\\g<1>4294967295',s)
    elif c=='218':
        s=re.sub(r'm_MaterialTemplate: \{[^}]*\}','m_MaterialTemplate: {fileID: 10652, guid: 0000000000000000f000000000000000, type: 0}',s)
        used_terrain.add(mref(s,'m_TerrainData')[1])
    elif c=='154':
        used_terrain.add(mref(s,'m_TerrainData')[1])
    pending.append((c,i,s))
print('docs scene',len(pending),'maillages',len(used_mesh),'materiaux',len(used_mat),'terrains',[os.path.basename(g2p.get(g,'?')) for g in used_terrain])

# ---------------------------------------------------------------- 5. terrains, vegetation
texjobs={}   # guid -> set(roles)
def need_tex(g,role):
    if g and g in g2p: texjobs.setdefault(g,set()).add(role)
out_guids=set()
tree_protos=set(); detail_protos=set()
for tg in used_terrain:
    src=g2p[tg]; dst=os.path.join(OUT,'Terrain',os.path.basename(src))
    shutil.copy(src,dst); shutil.copy(src+'.meta',dst+'.meta'); out_guids.add(tg)
    head=open(src,encoding='utf-8').read(400000)
    for g in re.findall(r'- \{fileID: 1953259897, guid: ([0-9a-f]+), type: 2\}',head):
        lp=g2p[g]; ld=os.path.join(OUT,'Terrain',os.path.basename(lp))
        if not os.path.exists(ld):
            shutil.copy(lp,ld); shutil.copy(lp+'.meta',ld+'.meta'); out_guids.add(g)
            lt=open(lp,encoding='utf-8').read()
            need_tex(mref(lt,'m_DiffuseTexture')[1],'albedo'); need_tex(mref(lt,'m_NormalMapTexture')[1],'normal'); need_tex(mref(lt,'m_MaskMapTexture')[1],'mask')
    with open(src,encoding='utf-8') as f:
        for line in f:
            mm=re.search(r'prefab: \{fileID: \d+, guid: ([0-9a-f]+)',line)
            if mm: tree_protos.add(mm.group(1))
            mm=re.search(r'      prototype: \{fileID: \d+, guid: ([0-9a-f]+)',line)
            if mm: detail_protos.add(mm.group(1))
print('prototypes arbres',tree_protos,'details',detail_protos)
def find_mesh(nm):
    for g,p in g2p.items():
        if p.endswith('/'+nm+'.asset') and '/Mesh/' in p: return g
def find_mat(nm):
    for g,p in g2p.items():
        if p.endswith('/'+nm+'.mat'): return g
pine0,pine1=find_mesh('Pine_L2_LOD0'),find_mesh('Pine_L2_LOD1'); bark,leaves=find_mat('Fir Common Bark'),find_mat('Fir Common Leaves')
grass,grassmat=find_mesh('GrassSingle_LOD0'),find_mat('StylizedGrass')
used_mesh.update([pine0,pine1,grass]); used_mat.update([bark,leaves,grassmat])
PH='{fileID: 0}'
def go_doc(fid,name_,comps_,active=1):
    return ('--- !u!1 &%d\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n'%fid
            +''.join('  - component: {fileID: %d}\n'%c for c in comps_)+'  m_Layer: 0\n  m_Name: %s\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: %d\n'%(name_,active))
def tr_doc(fid,go,father,kids):
    return ('--- !u!4 &%d\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %d}\n  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n'%(fid,go)
            +('  m_Children:\n'+''.join('  - {fileID: %d}\n'%k for k in kids) if kids else '  m_Children: []\n')+'  m_Father: {fileID: %d}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n'%father)
def mf_doc(fid,go,mesh):
    return '--- !u!33 &%d\nMeshFilter:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %d}\n  m_Mesh: {fileID: 4300000, guid: %s, type: 2}\n'%(fid,go,mesh)
def mr_doc(fid,go,mats,shadows=1):
    return ('--- !u!23 &%d\nMeshRenderer:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %d}\n  m_Enabled: 1\n  m_CastShadows: %d\n  m_ReceiveShadows: 1\n  m_DynamicOccludee: 1\n  m_StaticShadowCaster: 0\n  m_MotionVectors: 1\n  m_LightProbeUsage: 1\n  m_ReflectionProbeUsage: 1\n  m_RayTracingMode: 2\n  m_RayTraceProcedural: 0\n  m_RenderingLayerMask: 1\n  m_RendererPriority: 0\n  m_Materials:\n'%(fid,go,shadows)
            +''.join('  - {fileID: 2100000, guid: %s, type: 2}\n'%m for m in mats)+
            '  m_StaticBatchInfo:\n    firstSubMesh: 0\n    subMeshCount: 0\n  m_StaticBatchRoot: {fileID: 0}\n  m_ProbeAnchor: {fileID: 0}\n  m_LightProbeVolumeOverride: {fileID: 0}\n  m_ScaleInLightmap: 1\n  m_ReceiveGI: 1\n  m_PreserveUVs: 0\n  m_IgnoreNormalsForChartDetection: 0\n  m_ImportantGI: 0\n  m_StitchLightmapSeams: 1\n  m_SelectedEditorRenderState: 3\n  m_MinimumChartSize: 4\n  m_AutoUVMaxDistance: 0.5\n  m_AutoUVMaxAngle: 89\n  m_LightmapParameters: {fileID: 0}\n  m_SortingLayerID: 0\n  m_SortingLayer: 0\n  m_SortingOrder: 0\n  m_AdditionalVertexStreams: {fileID: 0}\n')
ROOT=1709254077376921
def write_prefab(path,guid,text):
    with open(path,'w',encoding='utf-8',newline='\n') as f: f.write('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+text)
    with open(path+'.meta','w',newline='\n') as f: f.write('fileFormatVersion: 2\nguid: %s\nPrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'%guid)
    out_guids.add(guid)
def tree_prefab(nm):
    t=go_doc(ROOT,nm,[11,12])+tr_doc(11,ROOT,0,[21,31])
    t+=('--- !u!205 &12\nLODGroup:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %d}\n  serializedVersion: 2\n  m_LocalReferencePoint: {x: 0, y: 6, z: 0}\n  m_Size: 12\n  m_FadeMode: 0\n  m_AnimateCrossFading: 0\n  m_LastLODIsBillboard: 0\n  m_LODs:\n  - screenRelativeHeight: 0.25\n    fadeTransitionWidth: 0\n    renderers:\n    - renderer: {fileID: 23}\n  - screenRelativeHeight: 0.01\n    fadeTransitionWidth: 0\n    renderers:\n    - renderer: {fileID: 33}\n  m_Enabled: 1\n'%ROOT)
    t+=go_doc(20,'LOD0',[21,22,23])+tr_doc(21,20,11,[])+mf_doc(22,20,pine0)+mr_doc(23,20,[bark,leaves])
    t+=go_doc(30,'LOD1',[31,32,33])+tr_doc(31,30,11,[])+mf_doc(32,30,pine1)+mr_doc(33,30,[bark,leaves])
    t=t.replace('m_Name: LOD0\n','m_Name: LOD0\n').replace('  m_Father: {fileID: 11}\n','  m_Father: {fileID: 11}\n')
    return t
for k,g in enumerate(sorted(tree_protos)):
    write_prefab(os.path.join(OUT,'Vegetation','Sapin_%d.prefab'%(k+1)),g,tree_prefab('Sapin'))
for k,g in enumerate(sorted(detail_protos)):
    write_prefab(os.path.join(OUT,'Vegetation','Herbe_%d.prefab'%(k+1)),g,go_doc(ROOT,'Herbe',[11,22,23])+tr_doc(11,ROOT,0,[])+mf_doc(22,ROOT,grass)+mr_doc(23,ROOT,[grassmat],0))

# ---------------------------------------------------------------- 6. maillages, materiaux, textures
for g in sorted(x for x in used_mesh if x and x not in BUILTIN):
    src=g2p[g]; dst=os.path.join(OUT,'Maillages',os.path.basename(src))
    shutil.copy(src,dst); shutil.copy(src+'.meta',dst+'.meta'); out_guids.add(g)
out_guids.update(guid_of('mesh:'+cg) for cg in split_jobs)
ROLE={'_MainTex':'albedo','_BumpMap':'normal','_MetallicGlossMap':'data','_OcclusionMap':'data','_EmissionMap':'albedo'}
FALLBACK_COLORS=[(('rearlight','brakelight','brake light','tail'),(0.55,0.02,0.02,1),(0.9,0.05,0.03,1)),
                 (('indicator','reflector'),(0.6,0.3,0.02,1),None),(('headlight','reverselight','reverse'),(0.85,0.85,0.8,1),None),
                 (('liquid',),(0.25,0.3,0.12,1),None),(('label',),(0.75,0.72,0.66,1),None),(('seat','leg','cap','pole','bar'),(0.18,0.18,0.19,1),None),
                 (('body','solidbody'),(0.32,0.34,0.36,1),None)]
def fallback(g):
    names=' '.join(missing_mats[g]).lower()
    o=Out('Manquant_'+g[:6]); c=(0.46,0.45,0.43,1); e=None
    for keys,cc,ee in FALLBACK_COLORS:
        if any(k in names for k in keys): c,e=cc,ee; break
    finish_standard(o,None,c,None,1,0,0.35,emis=e); standard_mode(o,0); return o
inv=Out('Invisible'); finish_standard(inv,None,(0,0,0,0),None,1,0,0); standard_mode(inv,2)
mats_out={guid_of('mat:invisible'):inv}
for g in used_mat:
    st=material(g)
    if st[0]=='missing': mats_out[g]=fallback(g)
    elif st[1] is not None: mats_out[g]=st[1]
    if g in ('%s'%bark,): mats_out[g].instancing=1
for g,o in mats_out.items():
    base=re.sub(r'[\\/:*?"<>|]','_',o.name) or g[:8]
    path=os.path.join(OUT,'Materiaux',base+'.mat')
    n=1
    while os.path.exists(path): n+=1; path=os.path.join(OUT,'Materiaux','%s_%d.mat'%(base,n))
    with open(path,'w',encoding='utf-8',newline='\n') as f: f.write(write_mat(o))
    with open(path+'.meta','w',newline='\n') as f: f.write('fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'%g)
    out_guids.add(g)
    for k,(tg,sc,of,nm) in o.tex.items(): need_tex(tg,'normal' if nm else ROLE.get(k,'albedo'))
for sname,sg in (('CarteTriplanaire',convmat.SH_TRI),('CarteDoubleFace',convmat.SH_DBL),('CarteEau',convmat.SH_EAU)):
    shutil.copy(os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','shaders','%s.shader'%sname),os.path.join(OUT,'Shaders',sname+'.shader'))
    with open(os.path.join(OUT,'Shaders',sname+'.shader.meta'),'w',newline='\n') as f:
        f.write('fileFormatVersion: 2\nguid: %s\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'%sg)
    out_guids.add(sg)
pickle.dump(dict(texjobs=texjobs),open('texjobs.pkl','wb'))
print('textures a traiter',len(texjobs))

# ---------------------------------------------------------------- 7. scene
out_guids.update(texjobs.keys())
SETTINGS='''--- !u!29 &1
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {fileID: 0}
--- !u!104 &2
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 10
  m_Fog: 0
  m_FogColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}
  m_FogMode: 3
  m_FogDensity: 0.01
  m_LinearFogStart: 0
  m_LinearFogEnd: 300
  m_AmbientSkyColor: {r: 0.212, g: 0.227, b: 0.259, a: 1}
  m_AmbientEquatorColor: {r: 0.114, g: 0.125, b: 0.133, a: 1}
  m_AmbientGroundColor: {r: 0.047, g: 0.043, b: 0.035, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 0
  m_SubtractiveShadowColor: {r: 0.42, g: 0.478, b: 0.627, a: 1}
  m_SkyboxMaterial: {fileID: 10304, guid: 0000000000000000f000000000000000, type: 0}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {fileID: 0}
  m_SpotCookie: {fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {fileID: 0}
  m_Sun: {fileID: 0}
  m_UseRadianceAmbientProbe: 0
--- !u!157 &3
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 12
  m_GIWorkflowMode: 1
  m_GISettings:
    serializedVersion: 2
    m_BounceScale: 1
    m_IndirectOutputScale: 1
    m_AlbedoBoost: 1
    m_EnvironmentLightingMode: 0
    m_EnableBakedLightmaps: 0
    m_EnableRealtimeLightmaps: 0
  m_LightmapEditorSettings:
    serializedVersion: 12
    m_Resolution: 2
    m_BakeResolution: 40
    m_AtlasSize: 1024
    m_AO: 0
    m_AOMaxDistance: 1
    m_CompAOExponent: 1
    m_CompAOExponentDirect: 0
    m_ExtractAmbientOcclusion: 0
    m_Padding: 2
    m_LightmapParameters: {fileID: 0}
    m_LightmapsBakeMode: 1
    m_TextureCompression: 1
    m_FinalGather: 0
    m_FinalGatherFiltering: 1
    m_FinalGatherRayCount: 256
    m_ReflectionCompression: 2
    m_MixedBakeMode: 2
    m_BakeBackend: 1
    m_PVRSampling: 1
    m_PVRDirectSampleCount: 32
    m_PVRSampleCount: 512
    m_PVRBounces: 2
    m_PVREnvironmentSampleCount: 256
    m_PVREnvironmentReferencePointCount: 2048
    m_PVRFilteringMode: 1
    m_PVRDenoiserTypeDirect: 1
    m_PVRDenoiserTypeIndirect: 1
    m_PVRDenoiserTypeAO: 1
    m_PVRFilterTypeDirect: 0
    m_PVRFilterTypeIndirect: 0
    m_PVRFilterTypeAO: 0
    m_PVREnvironmentMIS: 1
    m_PVRCulling: 1
    m_PVRFilteringGaussRadiusDirect: 1
    m_PVRFilteringGaussRadiusIndirect: 5
    m_PVRFilteringGaussRadiusAO: 2
    m_PVRFilteringAtrousPositionSigmaDirect: 0.5
    m_PVRFilteringAtrousPositionSigmaIndirect: 2
    m_PVRFilteringAtrousPositionSigmaAO: 1
    m_ExportTrainingData: 0
    m_TrainingDataDestination: TrainingData
    m_LightProbeSampleCountMultiplier: 4
  m_LightingDataAsset: {fileID: 0}
  m_LightingSettings: {fileID: 0}
--- !u!196 &4
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 3
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    buildHeightMesh: 0
    maxJobWorkers: 0
    preserveTilesOutsideBounds: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {fileID: 0}
'''
with open(os.path.join(OUT,'Carte','CarteSchedule1.unity'),'w',encoding='utf-8',newline='\n') as f:
    f.write('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+SETTINGS)
    for c,i,s in pending:
        s=fix_refs(s)
        f.write('--- !u!%s &%s\n'%(c,i)+s)
SCENE_GUID=guid_of('scene:carte')
with open(os.path.join(OUT,'Carte','CarteSchedule1.unity.meta'),'w',newline='\n') as f:
    f.write('fileFormatVersion: 2\nguid: %s\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'%SCENE_GUID)
json.dump(dict(scene_guid=SCENE_GUID),open(os.path.join(OUT,'Carte','infos.json'),'w'))
print('scene ecrite')
