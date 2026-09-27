import numpy as np, re
from unitymesh import *
def winding_sign(pos,nrm,idx):
    tri=idx.reshape(-1,3)
    a,b,c=pos[tri[:,0]],pos[tri[:,1]],pos[tri[:,2]]
    cr=np.cross(b-a,c-a)
    nn=nrm[tri[:,0]]+nrm[tri[:,1]]+nrm[tri[:,2]]
    d=(cr*nn).sum(1)
    return np.sign(d[np.abs(d)>1e-12]).sum()
def extract(m,lay,arrs,first,count,M):
    """cut submeshes [first,first+count) of combined mesh m into local space of M. returns dict"""
    subs=m['subs'][first:first+count]
    v0=min(s['firstVertex'] for s in subs); v1=max(s['firstVertex']+s['vertexCount'] for s in subs)
    newarrs={s:arr[v0:v1].copy() for s,arr in arrs.items()}
    vc=v1-v0
    M3=M[:3,:3]; det=np.linalg.det(M3)
    if abs(det)<1e-12: inv=np.linalg.pinv(M); iM3=np.linalg.pinv(M3)
    else: inv=np.linalg.inv(M); iM3=np.linalg.inv(M3)
    ch=m['channels']
    def view(ci):
        s,o,f,d=ch[ci]
        d&=15
        if d==0: return None
        if f==0: return newarrs[s][:,o:o+4*d].view(np.float32)
        if f==1: return newarrs[s][:,o:o+2*d].view(np.float16)
        return None
    pos=view(0); pos[:,:3]=(np.c_[pos[:,:3].astype(np.float64),np.ones(vc)]@inv.T)[:,:3]
    nrm=view(1)
    if nrm is not None:
        n=nrm[:,:3].astype(np.float64)@M3; l=np.linalg.norm(n,axis=1,keepdims=True); l[l==0]=1; nrm[:,:3]=n/l
    tan=view(2)
    if tan is not None:
        t=tan[:,:3].astype(np.float64)@iM3.T; l=np.linalg.norm(t,axis=1,keepdims=True); l[l==0]=1; tan[:,:3]=t/l
        if det<0: tan[:,3]*=-1
    # indices
    newsubs=[]; idx_parts=[]; byte=0
    for s in subs:
        fb=s['firstByte']; ic=s['indexCount']
        es=2 if m['index_format']==0 else 4
        ii=m['indices'][fb//es:fb//es+ic].astype(np.int64)-v0+s.get('baseVertex',0)
        idx_parts.append(ii)
        newsubs.append(dict(firstByte=0,indexCount=ic,topology=s.get('topology',0),firstVertex=s['firstVertex']-v0,vertexCount=s['vertexCount']))
    allidx=np.concatenate(idx_parts) if idx_parts else np.zeros(0,np.int64)
    fmt_=0 if vc<=65535 else 1
    es=2 if fmt_==0 else 4
    b=0
    for ns,ii in zip(newsubs,idx_parts):
        ns['firstByte']=b; b+=len(ii)*es
    return dict(subs=newsubs,arrs=newarrs,vcount=vc,indices=allidx,fmt=fmt_,det=det,pos=pos[:,:3],nrm=None if nrm is None else nrm[:,:3],idx_parts=idx_parts)
def fix_winding(r,ref_sign):
    # Le static batching inverse l'ordre des triangles des objets en miroir (echelle negative) :
    # on le remet a l'endroit, et seulement pour eux.
    if len(r['indices'])<3: return False
    if r['det']<0:
        for ii in r['idx_parts']:
            t=ii.reshape(-1,3); t[:,[1,2]]=t[:,[2,1]]
        r['indices']=np.concatenate(r['idx_parts'])
        return True
    return False
def aabbs(r):
    out=[]; pos=r['pos']
    allmin=None
    for ns,ii in zip(r['subs'],r['idx_parts']):
        if len(ii)==0: p=pos[:1]
        else: p=pos[np.unique(ii)]
        mn,mx=p.min(0),p.max(0); out.append(((mn+mx)/2,(mx-mn)/2))
    mn,mx=pos.min(0),pos.max(0)
    return out,((mn+mx)/2,(mx-mn)/2)
