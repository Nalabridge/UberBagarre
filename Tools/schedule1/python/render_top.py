import pickle, re, os, numpy as np, collections, sys
from PIL import Image
from unitymesh import parse_mesh, vertex_arrays, channel_view
P=pickle.load(open('packscene.pkl','rb')); GO,TR,MF,MR,TER,LI,COL=P['S']; W,A,G=P['W'],P['A'],P['G']
RES=float(sys.argv[1]) if len(sys.argv)>1 else 0.5
X0,X1,Z0,Z1=-340,480,-310,400
NX=int((X1-X0)/RES); NZ=int((Z1-Z0)/RES)
height=np.full(NX*NZ,-1e9,np.float32); rmin=np.full((3,NX*NZ),1e9,np.float32); color=np.zeros((NX*NZ,3),np.float32); kind=np.zeros(NX*NZ,np.uint8)
# ---- couleurs moyennes des materiaux
tavg={}
def texavg(g):
    if g in tavg: return tavg[g]
    p=G.get(g)
    c=np.array([0.5,0.5,0.5])
    if p and os.path.exists(p):
        try: c=np.asarray(Image.open(p).convert('RGB').resize((8,8))).reshape(-1,3).mean(0)/255
        except Exception: pass
    tavg[g]=c; return c
mcol={}; mskip=set()
def matcol(g):
    if g in mcol: return mcol[g]
    p=G.get(g); c=np.array([0.6,0.2,0.8]); skip=False
    if p and p.endswith('.mat'):
        t=open(p,encoding='utf-8').read()
        cm=re.search(r'- _Color: \{r: (\S+), g: (\S+), b: (\S+), a: (\S+)\}',t)
        base=np.array([float(cm.group(i)) for i in (1,2,3)]) if cm else np.ones(3)
        tm=re.search(r'- _MainTex:\n        m_Texture: \{fileID: \d+, guid: ([0-9a-f]+)',t)
        c=base*(texavg(tm.group(1)) if tm else 1)
        if "m_Name: Invisible" in t or 'RenderType: Transparent\n' in t and 'Eau' not in t: skip=True
        if 'guid: '+__import__('convmat').SH_EAU in t: c=np.array([0.05,0.14,0.18])
    mcol[g]=c
    if skip: mskip.add(g)
    return c
# ---- maillages
mcache={}
BOX=np.array([[-.5,-.5,-.5],[.5,-.5,-.5],[.5,.5,-.5],[-.5,.5,-.5],[-.5,-.5,.5],[.5,-.5,.5],[.5,.5,.5],[-.5,.5,.5]],np.float32)
BOXI=np.array([0,1,2,0,2,3,4,6,5,4,7,6,0,4,5,0,5,1,3,2,6,3,6,7,0,3,7,0,7,4,1,5,6,1,6,2])
def builtin(fid):
    s={'10202':(1,1,1),'10206':(1,2,1),'10207':(1,1,1),'10208':(1,2,1),'10209':(10,0.001,10),'10210':(1,1,0.001)}.get(fid,(1,1,1))
    return BOX*np.array(s,np.float32),[BOXI]
def mesh(fid,g):
    key=(fid,g)
    if key in mcache: return mcache[key]
    if g=='0000000000000000e000000000000000': r=builtin(fid)
    else:
        p=G.get(g)
        if not p: mcache[key]=None; return None
        t=open(p,encoding='utf-8').read()
        if '--- !u!43 &%s\n'%fid in t:
            body=t.split('--- !u!43 &%s\n'%fid,1)[1].split('\n--- !u!',1)[0]
        else: body=t.split('\n',3)[3]
        m=parse_mesh(body); lay,arrs=vertex_arrays(m)
        cv=channel_view(m,arrs,0) if m['vcount'] else None
        if cv is None: mcache[key]=None; return None
        pos=cv[:,:3].astype(np.float32).copy()
        es=2 if m['index_format']==0 else 4
        subs=[m['indices'][s['firstByte']//es:s['firstByte']//es+s['indexCount']].astype(np.int64) for s in m['subs'] if s.get('topology',0)==0]
        r=(pos,subs)
    mcache[key]=r; return r
def bigtri(a,b,cc,c,k):
    x0=max(X0,min(a[0],b[0],cc[0])); x1=min(X1,max(a[0],b[0],cc[0])); z0=max(Z0,min(a[2],b[2],cc[2])); z1=min(Z1,max(a[2],b[2],cc[2]))
    if x0>=x1 or z0>=z1: return
    xs=np.arange(x0,x1,RES*0.5); zs=np.arange(z0,z1,RES*0.5)
    X,Z=np.meshgrid(xs,zs); X=X.ravel(); Z=Z.ravel()
    d=(b[2]-cc[2])*(a[0]-cc[0])+(cc[0]-b[0])*(a[2]-cc[2])
    if abs(d)<1e-9: return
    l1=((b[2]-cc[2])*(X-cc[0])+(cc[0]-b[0])*(Z-cc[2]))/d; l2=((cc[2]-a[2])*(X-cc[0])+(a[0]-cc[0])*(Z-cc[2]))/d; l3=1-l1-l2
    ok=(l1>=0)&(l2>=0)&(l3>=0)
    Y=(l1*a[1]+l2*b[1]+l3*cc[1])[ok]; X=X[ok]; Z=Z[ok]
    ix=((X-X0)/RES).astype(np.int64); iz=((Z-Z0)/RES).astype(np.int64)
    ok=(ix>=0)&(ix<NX)&(iz>=0)&(iz<NZ); lin=iz[ok]*NX+ix[ok]; Y=Y[ok]
    sel=Y>height[lin]; lin,Y=lin[sel],Y[sel]
    height[lin]=Y; color[lin]=c; kind[lin]=k
def splat(wp,tri,c,k):
    a=wp[tri[:,0]];b=wp[tri[:,1]];cc=wp[tri[:,2]]
    area0=np.abs((b[:,0]-a[:,0])*(cc[:,2]-a[:,2])-(cc[:,0]-a[:,0])*(b[:,2]-a[:,2]))*0.5/(RES*RES)
    big=area0>4000
    for j in np.nonzero(big)[0]: bigtri(a[j],b[j],cc[j],c,k)
    tri=tri[~big]
    if len(tri)==0: return
    a=wp[tri[:,0]];b=wp[tri[:,1]];cc=wp[tri[:,2]]
    area=np.abs((b[:,0]-a[:,0])*(cc[:,2]-a[:,2])-(cc[:,0]-a[:,0])*(b[:,2]-a[:,2]))*0.5/(RES*RES)
    n=np.clip(np.ceil(area*3).astype(np.int64),1,20000)
    idx=np.repeat(np.arange(len(tri)),n)
    u=np.random.rand(len(idx)); v=np.random.rand(len(idx)); f=u+v>1; u[f]=1-u[f]; v[f]=1-v[f]
    pts=a[idx]+(b[idx]-a[idx])*u[:,None]+(cc[idx]-a[idx])*v[:,None]
    pts=np.concatenate([pts,a,b,cc])
    ix=((pts[:,0]-X0)/RES).astype(np.int64); iz=((pts[:,2]-Z0)/RES).astype(np.int64)
    ok=(ix>=0)&(ix<NX)&(iz>=0)&(iz<NZ)
    ix,iz,y=ix[ok],iz[ok],pts[ok,1]
    lin=iz*NX+ix
    if k in (1,2):
        np.minimum.at(rmin[k],lin,y)
    order=np.argsort(y); lin=lin[order]; y=y[order]
    # derniere ecriture = plus haut
    sel=y>height[lin]
    lin,y=lin[sel],y[sel]
    height[lin]=y; color[lin]=c; kind[lin]=k
KIND={'road':1,'sidewalk':2}
def kind_of(name):
    n=name.lower()
    if n in ('road',) or n.startswith('road') and 'sign' not in n: return 1
    if 'sidewalk' in n: return 2
    return 3
# ---- terrains
def terrain(go,g):
    p=G[g]; t=open(p,encoding='utf-8').read()
    res=int(re.search(r'm_Heightmap:.*?m_Resolution: (\d+)',t,re.S).group(1))
    sc=re.search(r'm_Heightmap:.*?m_Scale: \{x: (\S+), y: (\S+), z: (\S+)\}',t,re.S)
    sx,sy,sz=float(sc.group(1)),float(sc.group(2)),float(sc.group(3))
    hx=re.search(r'm_Heights: ([0-9a-f]+)',t).group(1)
    h=np.frombuffer(bytes.fromhex(hx),dtype=np.int16).astype(np.float32).reshape(res,res)/32766.0*sy
    M=W[go]; o=M[:3,3]
    xs=o[0]+np.arange(res)*sx; zs=o[2]+np.arange(res)*sz
    X,Z=np.meshgrid(xs,zs)
    Y=o[1]+h
    ix=((X-X0)/RES).astype(np.int64).ravel(); iz=((Z-Z0)/RES).astype(np.int64).ravel(); y=Y.ravel()
    ok=(ix>=0)&(ix<NX)&(iz>=0)&(iz<NZ)
    lin=iz[ok]*NX+ix[ok]; y=y[ok]
    sel=y>height[lin]; lin=lin[sel]; y=y[sel]
    height[lin]=y; color[lin]=np.array([0.28,0.33,0.2])*(0.8+0.2*np.clip((y-o[1])/20,0,1))[:,None]; kind[lin]=4
    return res,(sx,sy,sz),o
for go,g in TER.items():
    if A.get(go): print('terrain',terrain(go,g)[:2])
nr=0
for go,mr in MR.items():
    if not A.get(go) or not mr['enabled'] or go not in MF: continue
    m=mesh(*MF[go])
    if m is None: continue
    pos,subs=m; M=W[go]
    wp=pos@M[:3,:3].T+M[:3,3]
    k=kind_of(GO[go]['name'])
    if 'Water' in GO[go]['name'] or 'Ocean' in GO[go]['name']: k=5
    for si,tri in enumerate(subs):
        g=mr['mats'][min(si,len(mr['mats'])-1)] if mr['mats'] else None
        if g is None: continue
        c=matcol(g)
        if g in mskip or len(tri)<3: continue
        mn=os.path.basename(G.get(g,'')).lower()
        kk=k
        if 'sidewalk' in mn: kk=2
        elif ('road' in mn and 'mat' in mn) or mn.startswith('road_c') or 'pedestrian crossing' in mn: kk=1
        elif k in (1,2): kk=3
        splat(wp,tri.reshape(-1,3),c,kk)
    nr+=1
    if nr%5000==0: print(nr)
print('rendus',nr)
# ombrage par la pente
H=height.reshape(NZ,NX).copy(); H[H<-1e8]=np.nan
gx=np.nan_to_num(np.gradient(np.nan_to_num(H,nan=-20),axis=1)); gz=np.nan_to_num(np.gradient(np.nan_to_num(H,nan=-20),axis=0))
shade=np.clip(1+(-gx*0.5+gz*0.5)/RES*0.15,0.55,1.35)
img=color.reshape(NZ,NX,3)*shade[...,None]
img[np.isnan(H)]=(0.04,0.07,0.1)
img=np.clip(img**(1/1.6)*255,0,255).astype(np.uint8)[::-1]
Image.fromarray(img).save('top_%s.png'%str(RES).replace('.','_'))
np.savez_compressed('top_%s.npz'%str(RES).replace('.','_'),rmin=rmin[1].reshape(NZ,NX),smin=rmin[2].reshape(NZ,NX),color=color.reshape(NZ,NX,3),height=H,kind=kind.reshape(NZ,NX),bounds=np.array([X0,X1,Z0,Z1,RES]))
print('fini')
