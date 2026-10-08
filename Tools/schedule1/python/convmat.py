# Conversion des materiaux URP/HDRP/shader graph de la carte vers le rendu Built-in.
import re, hashlib
STANDARD=('46','0000000000000000f000000000000000',0)
def guid_of(s): return hashlib.md5(('uberbagarre-carte:'+s).encode()).hexdigest()
SH_TRI=guid_of('shader:triplanaire'); SH_DBL=guid_of('shader:doubleface'); SH_EAU=guid_of('shader:eau')
URP_LIT='8fefca38545dd814e9f5fe41b369c2c7'
DROP_SHADERS={'158039fe8ab15854aa14241c45c2696e','c7e0529c74d520445a4825b4febf63c2','fc08284badb872e499808af9cf884fe0','053404c014e520243b1d28900b417e0c'}
def isdrop(m):
    sh=m['shader']
    if sh is None: return True
    if sh[1] in DROP_SHADERS: return True
    if sh[1]=='0000000000000000f000000000000000' and sh[0]=='203': return True
    return False
class Out:
    def __init__(s,name):
        s.name=name; s.shader=STANDARD; s.tex={}; s.floats={}; s.colors={}; s.kw=[]; s.queue=-1; s.tags={}; s.flags=4; s.instancing=0
    def t(s,prop,tex,scale=(1,1),offset=(0,0),normal=False):
        if tex: s.tex[prop]=(tex[0] if isinstance(tex,tuple) else tex,scale,offset,normal)
def col(m,k,d=(1,1,1,1)): return m['color'].get(k,d)
def fl(m,k,d=0.0): return m['float'].get(k,d)
def tex(m,k): return m['tex'].get(k)
def pick(m,words,exclude=()):
    for k,v in m['tex'].items():
        n=m['_texnames'].get(v[0],'').lower()
        if any(w in n for w in words) and not any(e in n for e in exclude): return v
    return None
def standard_mode(o,mode,cutoff=0.5):
    o.floats['_Mode']=mode
    if mode==0:
        o.floats.update(_SrcBlend=1,_DstBlend=0,_ZWrite=1); o.tags={'RenderType':'Opaque'}; o.queue=-1
    elif mode==1:
        o.floats.update(_SrcBlend=1,_DstBlend=0,_ZWrite=1,_Cutoff=cutoff); o.kw.append('_ALPHATEST_ON'); o.tags={'RenderType':'TransparentCutout'}; o.queue=2450
    elif mode==2:
        o.floats.update(_SrcBlend=5,_DstBlend=10,_ZWrite=0); o.kw.append('_ALPHABLEND_ON'); o.tags={'RenderType':'Transparent'}; o.queue=3000
    elif mode==3:
        o.floats.update(_SrcBlend=1,_DstBlend=10,_ZWrite=0); o.kw.append('_ALPHAPREMULTIPLY_ON'); o.tags={'RenderType':'Transparent'}; o.queue=3000
def finish_standard(o,albedo,color,normal=None,bump=1.0,metal=0.0,smooth=0.3,metalmap=None,glossscale=1.0,occl=None,occl_strength=1.0,emis=None,emismap=None,smooth_albedo=False,albedo_st=((1,1),(0,0))):
    if albedo: o.t('_MainTex',albedo,*albedo_st)
    o.colors['_Color']=color
    if normal and bump>0.001:
        o.t('_BumpMap',normal,*albedo_st,normal=True); o.floats['_BumpScale']=bump; o.kw.append('_NORMALMAP')
    o.floats['_Metallic']=metal; o.floats['_Glossiness']=smooth; o.floats['_GlossMapScale']=glossscale
    if metalmap:
        o.t('_MetallicGlossMap',metalmap,*albedo_st); o.kw.append('_METALLICGLOSSMAP')
    if smooth_albedo:
        o.floats['_SmoothnessTextureChannel']=1; o.kw.append('_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A')
    if occl:
        o.t('_OcclusionMap',occl,*albedo_st); o.floats['_OcclusionStrength']=occl_strength
    if emis and max(emis[:3])>0.004:
        o.colors['_EmissionColor']=emis; o.kw.append('_EMISSION'); o.flags=1
        if emismap: o.t('_EmissionMap',emismap,*albedo_st)
    else:
        o.colors['_EmissionColor']=(0,0,0,1)
def st(v):
    return ((v[1],v[2])) if v else ((1,1),(0,0))
def convert(m):
    """m: parsed material (with _texnames). returns Out or None (renderer a supprimer)."""
    if isdrop(m): return None
    sh=m['shader'][1]; o=Out(m['name'])
    if sh in (URP_LIT,'63d40d552e036df4f927047c27662c72','d22b679de8dd25f4aaec48ee06ba9faf'):
        base=tex(m,'_BaseMap') or tex(m,'_MainTex')
        color=col(m,'_BaseColor',col(m,'_Color'))
        surf=fl(m,'_Surface'); clip=fl(m,'_AlphaClip') or ('_ALPHATEST_ON' in m['kw']); cull=fl(m,'_Cull',2)
        cutoff=fl(m,'_Cutoff',0.5)
        if sh=='d22b679de8dd25f4aaec48ee06ba9faf' and '_ALPHATEST_ON' in m['kw']: cull=0
        if cull==0:
            o.shader=(4800000,SH_DBL,3); o.t('_MainTex',base,*st(base)); o.colors['_Color']=color
            n=tex(m,'_BumpMap')
            if n: o.t('_BumpMap',n,*st(base),normal=True); o.floats['_BumpScale']=fl(m,'_BumpScale',1); o.kw.append('_NORMALMAP')
            o.floats['_Cutoff']=cutoff if (clip or surf==1) else 0.0; o.floats['_Glossiness']=min(fl(m,'_Smoothness',0.3),0.6); o.floats['_Metallic']=fl(m,'_Metallic')
            o.floats['_Wind']=0.0; o.queue=2450; o.instancing=1
            return o
        spec = fl(m,'_WorkflowMode',1)==0
        metalmap=None if spec else tex(m,'_MetallicGlossMap')
        smooth=fl(m,'_Smoothness',0.5)
        finish_standard(o,base,color,tex(m,'_BumpMap'),fl(m,'_BumpScale',1),0.0 if spec else fl(m,'_Metallic'),smooth,metalmap,smooth,
            tex(m,'_OcclusionMap'),fl(m,'_OcclusionStrength',1),col(m,'_EmissionColor',(0,0,0,1)) if '_EMISSION' in m['kw'] else None,tex(m,'_EmissionMap'),
            fl(m,'_SmoothnessTextureChannel')==1,st(base))
        if clip: standard_mode(o,1,cutoff)
        elif surf==1: standard_mode(o,3 if '_ALPHAPREMULTIPLY_ON' in m['kw'] or fl(m,'_Blend')==1 else 2)
        else: standard_mode(o,0)
        return o
    if sh in ('3c6c31705f3a5bb4c90a96af8cd8de56','bfe58bf947fb8fc44aaf2bb0c8b710fa'):
        o.shader=(4800000,SH_TRI,3)
        d=tex(m,'_DiffuseTexture') or tex(m,'_Texture'); n=tex(m,'_NormalTexture') or tex(m,'_Normal_Texture')
        o.t('_MainTex',d); o.colors['_Color']=col(m,'_BaseColor',(1,1,1,1))
        strength=fl(m,'_NormalStrength',0) or fl(m,'_Normal_Strength',0)
        if n and strength>0.01: o.t('_BumpMap',n,normal=True); o.floats['_BumpScale']=strength; o.kw.append('_NORMALMAP')
        o.floats['_Tiling']=fl(m,'_Tiling',1) or 1; o.floats['_Sharpness']=6
        o.floats['_Glossiness']=fl(m,'_Smoothness',0.1); o.floats['_Metallic']=fl(m,'_Metallic',0)
        return o
    if sh=='a9fe75469c0f05f40aa8eb8691501d9b':  # herbe stylisee
        o.shader=(4800000,SH_DBL,3); b=tex(m,'_BaseMap'); o.t('_MainTex',b,*st(b))
        c=col(m,'_BaseColor'); o.colors['_Color']=(c[0]*0.85,c[1]*0.85,c[2]*0.85,1)
        o.floats.update(_Cutoff=fl(m,'_Cutoff',0.5) or 0.5,_Glossiness=0.05,_Metallic=0,_Wind=1); o.queue=2450; o.instancing=1
        return o
    if sh=='9316645860ab1df4e96bc43f2a84dbce' or sh=='2e39d15138265474fbc6d2d899f495f9':
        uv=fl(m,'_Mat1_UV',1) or 1
        a=tex(m,'_Mat1_BaseTexture') or tex(m,'_Albedo'); n=tex(m,'_Mat1_Base_Normal') or tex(m,'_NormalMap')
        c1=col(m,'_Mat1_Color'); c=col(m,'_Color')
        color=(c[0]*c1[0],c[1]*c1[1],c[2]*c1[2],1)
        scale=(uv,uv) if sh=='9316645860ab1df4e96bc43f2a84dbce' else (1,1)
        rough=fl(m,'_Mat1_Roughness',0.5)
        finish_standard(o,a,color,n,fl(m,'_BumpScale',1),fl(m,'_Mat1_Metallic',0),max(0.05,1-rough) if sh.startswith('9316') else 0.35,None,1,None,1,None,None,False,(scale,(0,0)))
        standard_mode(o,0); return o
    if sh=='e7eb376c67f8010488e94b0d4f9dbaab':  # vitres
        finish_standard(o,None,(0.16,0.19,0.21,0.28),None,1,0.1,0.94)
        standard_mode(o,3); return o
    if sh in ('9820ca1d0c3fa4c49a92e44d3d9ff692','559d8f956a8df384ca234613b4a81e9e'):
        finish_standard(o,tex(m,'_Albedo') or tex(m,'_MainTex'),(1,1,1,1),tex(m,'_NormalMap'),1,0,0.15)
        standard_mode(o,0); return o
    if sh=='f250600644fffcb4381559e328dbb6a9':
        o.shader=(4800000,SH_DBL,3); a=tex(m,'_Albedo'); o.t('_MainTex',a)
        n=tex(m,'_NormalMap')
        if n: o.t('_BumpMap',n,normal=True); o.floats['_BumpScale']=1; o.kw.append('_NORMALMAP')
        o.colors['_Color']=(0.6,0.68,0.58,1); o.floats.update(_Cutoff=0.4,_Glossiness=0.05,_Metallic=0,_Wind=0.5); o.queue=2450; o.instancing=1
        return o
    if sh in ('c6ea859aa19bc27458b61cb66fc3269c','a5ee4875340a65c43b281798f7b95194','60424e672167bb34499888633d96a62a'):
        a=pick(m,('basecolor','albedo','diffuse','color'),('mask',)); n=pick(m,('normal',))
        c=col(m,'_BaseColor',col(m,'_Color'))
        if sh=='c6ea859aa19bc27458b61cb66fc3269c': c=(1,1,1,1)
        occ=pick(m,('_ao','ao.'))
        finish_standard(o,a,c,n,1,0,0.3,None,1,occ,1)
        standard_mode(o,0); return o
    if sh in ('abfbbad39ae511245a6bde771c2fe7e1','1e455454e98b67a48b2d74f2e3f5a347'):
        finish_standard(o,None,col(m,'_SkinColor',(0.8,0.64,0.52,1)),None,1,0,0.25); standard_mode(o,0); return o
    if sh in ('e2dcc36eccd2c704696423aefe75dde4','a2902146f6413b840b805d88f113e600'):
        o.shader=(4800000,SH_EAU,3); o.t('_BumpMap',tex(m,'_BumpMap'),normal=True)
        ocean=sh.startswith('a29')
        o.colors['_Color']=(0.025,0.06,0.075,0.82) if ocean else (0.03,0.06,0.04,0.8)
        o.colors['_HorizonColor']=(0.06,0.11,0.14,1) if ocean else (0.05,0.09,0.07,1)
        o.floats.update(_BumpScale=0.7,_Tiling=0.06 if ocean else 0.2,_Speed=0.6,_Glossiness=0.93); o.queue=2990
        return o
    # inconnu : on prend ce qu'on trouve
    a=pick(m,('basecolor','albedo','diffuse','color','_d.'),('mask','normal')) or tex(m,'_BaseMap') or tex(m,'_MainTex')
    finish_standard(o,a,col(m,'_BaseColor',col(m,'_Color')),pick(m,('normal',)),1,0,0.3)
    standard_mode(o,0); return o
def fmt(x):
    if isinstance(x,int): return str(x)
    s='%.6g'%x
    return s
def write_mat(o):
    L=['%YAML 1.1','%TAG !u! tag:unity3d.com,2011:','--- !u!21 &2100000','Material:','  serializedVersion: 8','  m_ObjectHideFlags: 0',
       '  m_CorrespondingSourceObject: {fileID: 0}','  m_PrefabInstance: {fileID: 0}','  m_PrefabAsset: {fileID: 0}','  m_Name: '+yq(o.name),
       '  m_Shader: {fileID: %s, guid: %s, type: %d}'%o.shader,'  m_Parent: {fileID: 0}','  m_ModifiedSerializedProperties: 0']
    kw=sorted(set(o.kw))
    if kw: L.append('  m_ValidKeywords:'); L+=['  - '+k for k in kw]
    else: L.append('  m_ValidKeywords: []')
    L+=['  m_InvalidKeywords: []','  m_LightmapFlags: %d'%o.flags,'  m_EnableInstancingVariants: %d'%o.instancing,'  m_DoubleSidedGI: 0','  m_CustomRenderQueue: %d'%o.queue]
    if o.tags: L.append('  stringTagMap:'); L+=['    %s: %s'%kv for kv in o.tags.items()]
    else: L.append('  stringTagMap: {}')
    L+=['  disabledShaderPasses: []','  m_LockedProperties: ','  m_SavedProperties:','    serializedVersion: 3','    m_TexEnvs:']
    for k,(g,sc,of,nm) in sorted(o.tex.items()):
        L+=['    - %s:'%k,'        m_Texture: {fileID: 2800000, guid: %s, type: 3}'%g,'        m_Scale: {x: %s, y: %s}'%(fmt(sc[0]),fmt(sc[1])),'        m_Offset: {x: %s, y: %s}'%(fmt(of[0]),fmt(of[1]))]
    if not o.tex: L[-1]='    m_TexEnvs: []'
    L.append('    m_Ints: []')
    if o.floats:
        L.append('    m_Floats:'); L+=['    - %s: %s'%(k,fmt(float(v))) for k,v in sorted(o.floats.items())]
    else: L.append('    m_Floats: []')
    if o.colors:
        L.append('    m_Colors:'); L+=['    - %s: {r: %s, g: %s, b: %s, a: %s}'%((k,)+tuple(fmt(float(x)) for x in v)) for k,v in sorted(o.colors.items())]
    else: L.append('    m_Colors: []')
    L+=['  m_BuildTextureStacks: []','  m_AllowLocking: 1','']
    return '\n'.join(L)
def yq(s):
    s=s.strip()
    if s[:1] in "'\"": return s
    if re.search(r'[:#\[\]{},&*!|>%@`]',s) or s[:1] in '-?': return "'"+s.replace("'","''")+"'"
    return s
