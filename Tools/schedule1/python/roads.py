import numpy as np, pickle
from scipy import ndimage
from PIL import Image, ImageDraw
d=np.load('top_0_5.npz'); X0,X1,Z0,Z1,RES=d['bounds']; rmin=d['rmin']; smin=d['smin']
NZ,NX=rmin.shape
road=rmin<1e8
road=ndimage.binary_closing(road,structure=np.ones((5,5)),iterations=2)
road=ndimage.binary_opening(road,structure=np.ones((3,3)))
lab,n=ndimage.label(road); sizes=ndimage.sum(road,lab,range(1,n+1))
road=np.isin(lab,1+np.nonzero(sizes>400)[0])
def zhang_suen(img):
    img=img.copy().astype(np.uint8)
    changed=True
    while changed:
        changed=False
        for step in (0,1):
            P=np.pad(img,1)
            p2=P[:-2,1:-1];p3=P[:-2,2:];p4=P[1:-1,2:];p5=P[2:,2:];p6=P[2:,1:-1];p7=P[2:,:-2];p8=P[1:-1,:-2];p9=P[:-2,:-2]
            B=p2+p3+p4+p5+p6+p7+p8+p9
            seq=[p2,p3,p4,p5,p6,p7,p8,p9,p2]
            A=sum(((seq[i]==0)&(seq[i+1]==1)).astype(np.uint8) for i in range(8))
            if step==0: c=(p2*p4*p6==0)&(p4*p6*p8==0)
            else: c=(p2*p4*p8==0)&(p2*p6*p8==0)
            m=(img==1)&(B>=2)&(B<=6)&(A==1)&c
            if m.any(): img[m]=0; changed=True
    return img.astype(bool)
sk=zhang_suen(road)
print('route px',road.sum(),'squelette',sk.sum())
# graphe
ys,xs=np.nonzero(sk); S=set(zip(ys,xs))
def nb(p):
    y,x=p
    return [(y+dy,x+dx) for dy in (-1,0,1) for dx in (-1,0,1) if (dy or dx) and (y+dy,x+dx) in S]
deg={p:len(nb(p)) for p in S}
nodes={p for p,k in deg.items() if k!=2}
edges=[]; seen=set()
for n0 in nodes:
    for q in nb(n0):
        if (n0,q) in seen: continue
        path=[n0,q]; prev=n0; cur=q
        while cur not in nodes:
            nx=[r for r in nb(cur) if r!=prev]
            if not nx: break
            prev,cur=cur,nx[0]; path.append(cur)
        seen.add((path[-1],path[-2])); seen.add((n0,q))
        edges.append(path)
print('noeuds',len(nodes),'aretes',len(edges))
def world(p):
    y,x=p
    return (X0+(x+0.5)*RES, Z0+(y+0.5)*RES)
W=[[world(p) for p in e] for e in edges]
pickle.dump(dict(edges=W,nodes=[world(p) for p in nodes],road=road,sk=sk),open('roads.pkl','wb'))
img=Image.open('top_0_5.png').convert('RGB'); dr=ImageDraw.Draw(img)
H=img.height
for e in edges:
    pts=[(x,H-1-y) for y,x in e]
    if len(e)>1: dr.line(pts,fill=(255,40,40),width=2)
for y,x in nodes: dr.ellipse((x-3,H-1-y-3,x+3,H-1-y+3),outline=(40,255,255))
img.crop((250,350,1300,1400)).save('roads.png')
