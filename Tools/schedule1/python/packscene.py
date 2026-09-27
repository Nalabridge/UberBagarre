# Lecture de la scene du pack (apres conversion) : transforms, rendus, terrains.
import re, os, pickle, numpy as np, collections
OUT='out/Schedule1'
num=r'(-?[\d.eE+-]+)'
def vec(s,key,n=3):
    m=re.search(key+r': \{x: '+num+', y: '+num+', z: '+num+(', w: '+num if n==4 else '')+r'\}',s)
    return tuple(float(x) for x in m.groups()) if m else None
def load():
    txt=open(os.path.join(OUT,'Carte/CarteSchedule1.unity'),encoding='utf-8').read()
    GO={};TR={};MF={};MR={};TER={};LI={};COL={}
    for d in re.split(r'\n(?=--- !u!)',txt):
        m=re.match(r'--- !u!(\d+) &(-?\d+)',d)
        if not m: continue
        c,i=m.groups()
        if c=='1':
            GO[i]=dict(name=re.search(r'm_Name: ?(.*)',d).group(1).strip().strip("'"),active=re.search(r'm_IsActive: (\d)',d).group(1)=='1')
        elif c=='4':
            TR[i]=dict(go=re.search(r'm_GameObject: \{fileID: (-?\d+)',d).group(1),p=vec(d,'m_LocalPosition'),r=vec(d,'m_LocalRotation',4),s=vec(d,'m_LocalScale'),
                       father=re.search(r'm_Father: \{fileID: (-?\d+)',d).group(1))
        elif c=='33':
            mm=re.search(r'm_Mesh: \{fileID: (-?\d+)(?:, guid: ([0-9a-f]+))?',d); MF[re.search(r'm_GameObject: \{fileID: (-?\d+)',d).group(1)]=(mm.group(1),mm.group(2))
        elif c=='23':
            blk=d.split('m_Materials:',1)[1].split('\n  m_',1)[0]
            MR[re.search(r'm_GameObject: \{fileID: (-?\d+)',d).group(1)]=dict(mats=re.findall(r'guid: ([0-9a-f]+)',blk),enabled=re.search(r'm_Enabled: (\d)',d).group(1)=='1')
        elif c=='218':
            TER[re.search(r'm_GameObject: \{fileID: (-?\d+)',d).group(1)]=re.search(r'm_TerrainData: \{fileID: -?\d+, guid: ([0-9a-f]+)',d).group(1)
        elif c=='108':
            LI[re.search(r'm_GameObject: \{fileID: (-?\d+)',d).group(1)]=d
        elif c in ('65','64','136','135'):
            COL.setdefault(re.search(r'm_GameObject: \{fileID: (-?\d+)',d).group(1),[]).append((c,d))
    return GO,TR,MF,MR,TER,LI,COL
def qm(q):
    x,y,z,w=q
    return np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
def world(GO,TR):
    children=collections.defaultdict(list)
    for i,t in TR.items():
        if t['father']!='0': children[t['father']].append(i)
    W={}; A={}
    stack=[(i,np.eye(4),True) for i,t in TR.items() if t['father']=='0']
    while stack:
        i,M,act=stack.pop(); t=TR[i]
        L=np.eye(4); L[:3,:3]=qm(t['r'])@np.diag(t['s']); L[:3,3]=t['p']
        G=M@L; a=act and GO[t['go']]['active']
        W[t['go']]=G; A[t['go']]=a
        for c in children[i]: stack.append((c,G,a))
    return W,A
def guidmap():
    g={}
    for root,_,fs in os.walk(OUT):
        for f in fs:
            if f.endswith('.meta'):
                m=re.search(r'guid: ([0-9a-f]{32})',open(os.path.join(root,f)).read())
                g[m.group(1)]=os.path.join(root,f[:-5])
    return g
if __name__=='__main__':
    S=load(); W,A=world(S[0],S[1])
    pickle.dump(dict(S=S,W=W,A=A,G=guidmap()),open('packscene.pkl','wb'),protocol=4)
    print('ok',len(S[0]),sum(A.values()))
