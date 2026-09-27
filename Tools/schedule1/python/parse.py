import re, pickle, sys
hdr=re.compile(r'^--- !u!(\d+) &(-?\d+)')
docs={}
cls_keep={'1','4','224','33','23','114','218','108','137','65','64'}
cur=None; buf=[]
def flush():
    if cur and cur[0] in cls_keep: docs[cur[1]]=(cur[0],''.join(buf))
with open('Main.unity',encoding='utf-8',errors='replace') as f:
    for line in f:
        if line.startswith('--- !u!'):
            flush(); m=hdr.match(line); cur=(m.group(1),m.group(2)); buf=[]
        else: buf.append(line)
flush()
num=r'(-?[\d.eE+-]+)'
def vec(s,key,n=3):
    m=re.search(key+r': \{x: '+num+', y: '+num+', z: '+num+(', w: '+num if n==4 else '')+r'\}',s)
    return tuple(float(x) for x in m.groups()) if m else None
def fid(s,key):
    m=re.search(key+r': \{fileID: (-?\d+)(?:, guid: ([0-9a-f]+))?',s); return (m.group(1),m.group(2)) if m else (None,None)
GO={};TR={};MF={};MR={};MB={};TER={};LI={};SK={}
for i,(c,s) in docs.items():
    if c=='1':
        name=re.search(r'm_Name: ?(.*)',s); act=re.search(r'm_IsActive: (\d)',s); lay=re.search(r'm_Layer: (\d+)',s)
        GO[i]=dict(name=name.group(1).strip() if name else '',active=act.group(1)=='1' if act else True,layer=int(lay.group(1)) if lay else 0)
    elif c in('4','224'):
        TR[i]=dict(go=fid(s,'m_GameObject')[0],p=vec(s,'m_LocalPosition'),r=vec(s,'m_LocalRotation',4),sc=vec(s,'m_LocalScale'),father=fid(s,'m_Father')[0])
    elif c=='33': MF[i]=dict(go=fid(s,'m_GameObject')[0],mesh=fid(s,'m_Mesh'))
    elif c in ('23','137'):
        mats=re.findall(r'- \{fileID: (-?\d+), guid: ([0-9a-f]+)',s.split('m_Materials:')[1].split('m_')[0]) if 'm_Materials:' in s else []
        en=re.search(r'm_Enabled: (\d)',s)
        MR[i]=dict(go=fid(s,'m_GameObject')[0],mats=[g for _,g in mats],enabled=(en.group(1)=='1') if en else True,skinned=c=='137',mesh=fid(s,'m_Mesh') if c=='137' else None)
    elif c=='114': MB[i]=dict(go=fid(s,'m_GameObject')[0],script=fid(s,'m_Script')[1])
    elif c=='218': TER[i]=dict(go=fid(s,'m_GameObject')[0],data=fid(s,'m_TerrainData')[1],enabled=re.search(r'm_Enabled: (\d)',s).group(1)=='1')
    elif c=='108':
        t=re.search(r'm_Type: (\d)',s); inten=re.search(r'm_Intensity: '+num,s); rng=re.search(r'm_Range: '+num,s)
        LI[i]=dict(go=fid(s,'m_GameObject')[0],type=int(t.group(1)) if t else -1,intensity=float(inten.group(1)) if inten else 0,range=float(rng.group(1)) if rng else 0)
    elif c in('65','64'): SK[i]=dict(go=fid(s,'m_GameObject')[0],cls=c)
print(len(GO),len(TR),len(MF),len(MR),len(MB),len(TER),len(LI),len(SK))
pickle.dump(dict(GO=GO,TR=TR,MF=MF,MR=MR,MB=MB,TER=TER,LI=LI,COL=SK),open('scene.pkl','wb'))
