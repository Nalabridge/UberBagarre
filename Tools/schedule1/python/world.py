import pickle, numpy as np, re, collections, sys
D=pickle.load(open('scene.pkl','rb')); GO,TR,MF,MR,MB,TER,LI,COL=[D[k] for k in 'GO TR MF MR MB TER LI COL'.split()]
go2tr={t['go']:i for i,t in TR.items()}
children=collections.defaultdict(list)
for i,t in TR.items():
    if t['father'] and t['father']!='0': children[t['father']].append(i)
roots=[i for i,t in TR.items() if not t['father'] or t['father']=='0']
def qm(q):
    x,y,z,w=q
    return np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
W={}
def walk(i,M,active):
    t=TR[i]; p=t['p'] or (0,0,0); r=t['r'] or (0,0,0,1); s=t['sc'] or (1,1,1)
    L=np.eye(4); L[:3,:3]=qm(r)@np.diag(s); L[:3,3]=p
    G=M@L; a=active and GO.get(t['go'],{}).get('active',True)
    W[i]=(G,a)
    for c in children[i]: walk(c,G,a)
sys.setrecursionlimit(100000)
for r in roots: walk(r,np.eye(4),True)
comp=collections.defaultdict(lambda: collections.Counter())
for tab,k in ((MF,'mesh'),(MR,'rend'),(MB,'mono'),(TER,'terrain'),(LI,'light'),(COL,'col')):
    for i,c in tab.items(): comp[c['go']][k]+=1
pickle.dump(dict(W=W,children=dict(children),roots=roots,go2tr=go2tr,comp=dict(comp)),open('world.pkl','wb'))
# resume par racine
def stats(i):
    st=collections.Counter(); pts=[]
    stack=[i]
    while stack:
        j=stack.pop(); g=TR[j]['go']; st['obj']+=1; st.update(comp.get(g,{}))
        if comp.get(g,{}).get('rend'): pts.append(W[j][0][:3,3])
        stack+=children[j]
    return st,np.array(pts)
out=[]
for r in roots:
    st,pts=stats(r); g=GO[TR[r]['go']]
    bb=(pts.min(0).round(0).tolist(),pts.max(0).round(0).tolist()) if len(pts) else None
    out.append((st['rend'],g['name'],g['active'],dict(st),bb))
out.sort(key=lambda x:-x[0])
for o in out[:70]: print(o[0], repr(o[1]), 'actif' if o[2] else 'INACTIF', {k:v for k,v in o[3].items() if k in('obj','mono','terrain','light','col')}, o[4])
print(len(roots),'racines')
