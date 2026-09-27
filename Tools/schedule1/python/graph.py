import numpy as np, pickle, math, collections
from scipy.sparse.csgraph import dijkstra
from scipy.sparse import coo_matrix
from PIL import Image, ImageDraw
R=pickle.load(open('roads.pkl','rb')); sk=R['sk']; road=R['road']
d=np.load('top_0_5.npz'); X0,X1,Z0,Z1,RES=d['bounds']; rmin=d['rmin']; smin=d['smin']; H=d['height']
NZ,NX=sk.shape
# ---------- graphe de pixels du squelette
ys,xs=np.nonzero(sk); idx=-np.ones(sk.shape,np.int64); idx[ys,xs]=np.arange(len(ys))
rows=[];cols=[];w=[]
for dy in (-1,0,1):
    for dx in (-1,0,1):
        if not (dy or dx): continue
        y2=ys+dy; x2=xs+dx; ok=(y2>=0)&(y2<NZ)&(x2>=0)&(x2<NX)
        j=np.full(len(ys),-1); j[ok]=idx[y2[ok],x2[ok]]
        m=j>=0
        rows+=list(np.nonzero(m)[0]); cols+=list(j[m]); w+=[math.hypot(dx,dy)]*int(m.sum())
G=coo_matrix((w,(rows,cols)),shape=(len(ys),len(ys))).tocsr()
deg=np.asarray((G>0).sum(1)).ravel()
def wpos(i): return np.array([X0+(xs[i]+0.5)*RES, Z0+(ys[i]+0.5)*RES])
# ---------- noeuds = extremites et carrefours ; aretes = chaines
node=set(np.nonzero(deg!=2)[0])
edges=[]; used=set()
nbrs=[G.indices[G.indptr[i]:G.indptr[i+1]] for i in range(len(ys))]
for n0 in node:
    for q in nbrs[n0]:
        if (n0,q) in used: continue
        path=[n0,q]; prev=n0; cur=q
        while cur not in node:
            nx=[r for r in nbrs[cur] if r!=prev]
            if not nx: break
            prev,cur=cur,nx[0]; path.append(cur)
        used.add((n0,q)); used.add((path[-1],path[-2]))
        edges.append(path)
# ---------- fusion des noeuds proches (union-find sur distance < 7 m)
nodes=list(node); P=np.array([wpos(i) for i in nodes]); parent=list(range(len(nodes)))
def find(a):
    while parent[a]!=a: parent[a]=parent[parent[a]]; a=parent[a]
    return a
for a in range(len(nodes)):
    for b in range(a+1,len(nodes)):
        if np.linalg.norm(P[a]-P[b])<7: parent[find(a)]=find(b)
ni={n:find(k) for k,n in enumerate(nodes)}
cl=collections.defaultdict(list)
for k,n in enumerate(nodes): cl[find(k)].append(P[k])
C={c:np.mean(v,0) for c,v in cl.items()}
E=[]
for e in edges:
    a,b=ni[e[0]],ni[e[-1]]
    pts=[wpos(i) for i in e]
    L=sum(np.linalg.norm(pts[k+1]-pts[k]) for k in range(len(pts)-1))
    if a==b and L<25: continue
    E.append([a,b,pts,L])
# ---------- bretelles du viaduc : la route monte, le squelette 2D croit qu'elle rejoint la rue
def edge_hmax(pts):
    hs=[]
    for p in pts[::3]:
        i=int((p[1]-Z0)/RES); j=int((p[0]-X0)/RES)
        v=rmin[max(0,i-2):i+3,max(0,j-2):j+3]; v=v[v<1e8]
        if len(v): hs.append(float(np.median(v)))
    return max(hs) if hs else 0.0
E=[e for e in E if edge_hmax(e[2])<6.0]
# ---------- elagage des bouts (culs-de-sac courts) iteratif
for it in range(6):
    dg=collections.Counter()
    for a,b,_,_ in E: dg[a]+=1; dg[b]+=1
    E2=[e for e in E if not ((dg[e[0]]==1 or dg[e[1]]==1) and e[3]<14)]
    if len(E2)==len(E): break
    E=E2
# doublons (meme paire, longueurs proches) -> un seul
seen={}
E3=[]
for e in E:
    k=tuple(sorted((e[0],e[1])))
    if k in seen and abs(seen[k]-e[3])<6: continue
    seen[k]=e[3]; E3.append(e)
E=E3
# plus grande composante
adj=collections.defaultdict(list)
for k,(a,b,_,_) in enumerate(E): adj[a].append(k); adj[b].append(k)
comp={}; best=None
for s in adj:
    if s in comp: continue
    st=[s]; comp[s]=s; members=[s]
    while st:
        u=st.pop()
        for k in adj[u]:
            v=E[k][1] if E[k][0]==u else E[k][0]
            if v not in comp: comp[v]=s; st.append(v); members.append(v)
    L=sum(E[k][3] for m in members for k in adj[m])
    if best is None or L>best[0]: best=(L,s)
E=[e for e in E if comp[e[0]]==best[1]]
print('aretes',len(E),'longueur totale %.0f m'%(sum(e[3] for e in E)))
# ---------- circuit d'Euler sur le graphe double (chaque rue dans les deux sens)
adj=collections.defaultdict(list)
for k,(a,b,pts,L) in enumerate(E):
    adj[a].append((k,0)); adj[b].append((k,1))
def heading(pts):
    v=pts[min(3,len(pts)-1)]-pts[0]; return math.atan2(v[0],v[1])
usedir=set()
start=E[0][0]
# Hierholzer avec preference pour tourner a droite (circuit « main droite »)
def seg(k,rev):
    pts=E[k][2]; return pts[::-1] if rev else pts
stack=[(start,None,None)]; circuit=[]
cur_dir={}
while stack:
    u,kin,rin=stack[-1]
    cands=[(k,r) for (k,r) in adj[u] if (k,r) not in usedir]
    if cands:
        if kin is not None:
            inc=seg(kin,rin); h_in=heading(inc[::-1])+math.pi  # direction d'arrivee
            def turn(c):
                k,r=c; out=seg(k,r); h=heading(out); t=(h-h_in+math.pi)%(2*math.pi)-math.pi
                if k==kin: return 10  # demi-tour en dernier
                return -t   # plus a droite d'abord
            cands.sort(key=turn)
        k,r=cands[0]; usedir.add((k,r))
        out=seg(k,r); v=E[k][1] if r==0 else E[k][0]
        stack.append((v,k,r))
    else:
        stack.pop()
        if kin is not None: circuit.append((kin,rin))
circuit.reverse()
route=[]
for k,r in circuit:
    pts=seg(k,r)
    if route and np.linalg.norm(route[-1]-pts[0])<1e-6: pts=pts[1:]
    route+=list(pts)
route=np.array(route)
print('circuit',len(circuit),'troncons, %.0f m'%(np.sum(np.linalg.norm(np.diff(route,axis=0),axis=1))))
pickle.dump(dict(E=E,C=C,route=route,circuit=circuit),open('graph.pkl','wb'))
# ---------- image
img=Image.open('top_0_5.png').convert('RGB'); dr=ImageDraw.Draw(img); Hh=img.height
def px(p): return ((p[0]-X0)/RES,Hh-1-(p[1]-Z0)/RES)
for a,b,pts,L in E: dr.line([px(p) for p in pts],fill=(255,50,50),width=3)
for c,p in C.items(): 
    if any(e[0]==c or e[1]==c for e in E): q=px(p); dr.ellipse((q[0]-4,q[1]-4,q[0]+4,q[1]+4),outline=(40,255,255),width=2)
img.crop((250,350,1300,1400)).save('graph.png')
