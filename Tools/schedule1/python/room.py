import pickle, re, os, numpy as np, sys
from PIL import Image, ImageDraw
exec(open('render_top.py').read().split('# ---- terrains')[0].replace("RES=float(sys.argv[1]) if len(sys.argv)>1 else 0.5","RES=0.05").replace("X0,X1,Z0,Z1=-340,480,-310,400","X0,X1,Z0,Z1=[float(v) for v in sys.argv[1:5]]"))
YMAX=float(sys.argv[5]); YMIN=float(sys.argv[6])
height=np.full(NX*NZ,-1e9,np.float32); color=np.zeros((NX*NZ,3),np.float32); kind=np.zeros(NX*NZ,np.uint8)
labels=[]
for go,mr in MR.items():
    if not A.get(go) or not mr['enabled'] or go not in MF: continue
    M=W[go]; p=M[:3,3]
    if not (X0-5<p[0]<X1+5 and Z0-5<p[2]<Z1+5): continue
    m=mesh(*MF[go])
    if m is None: continue
    pos,subs=m
    wp=pos@M[:3,:3].T+M[:3,3]
    for si,tri in enumerate(subs):
        if len(tri)<3: continue
        g=mr['mats'][min(si,len(mr['mats'])-1)]
        c=matcol(g)
        if g in mskip: continue
        t=tri.reshape(-1,3)
        keep=(wp[t,1].max(1)<YMAX)&(wp[t,1].min(1)>YMIN)
        if keep.any(): splat(wp,t[keep],c,3)
    if X0<p[0]<X1 and Z0<p[2]<Z1 and YMIN<p[1]<YMAX: labels.append((GO[go]['name'],p))
Hh=height.reshape(NZ,NX); img=color.reshape(NZ,NX,3).copy(); img[Hh<-1e8]=(0.1,0.1,0.15)
im=Image.fromarray(np.clip(img[::-1]**(1/1.6)*255,0,255).astype(np.uint8)).resize((NX*2,NZ*2),Image.NEAREST)
dr=ImageDraw.Draw(im)
def px(x,z): return ((x-X0)/RES*2,(NZ-1-(z-Z0)/RES)*2)
import math
for x in np.arange(math.ceil(X0),X1+0.01,1.0): dr.line([px(x,Z0),px(x,Z1)],fill=(90,90,90)); dr.text((px(x,Z1)[0]+2,2),'%g'%x,fill=(255,255,0))
for z in np.arange(math.ceil(Z0),Z1+0.01,1.0): dr.line([px(X0,z),px(X1,z)],fill=(90,90,90)); dr.text((2,px(X0,z)[1]+2),'%g'%z,fill=(255,255,0))
seen=set()
for n,p in labels:
    if n in seen: continue
    seen.add(n); q=px(p[0],p[2]); dr.text((q[0],q[1]),n[:14],fill=(255,120,255))
im.save(sys.argv[7])
