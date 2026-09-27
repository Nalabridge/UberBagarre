import os, re, pickle, collections
SRC='src'
guid2path={}
for root,_,files in os.walk(SRC):
    for f in files:
        if f.endswith('.meta'):
            p=os.path.join(root,f)
            m=re.search(r'^guid: ([0-9a-f]{32})',open(p,encoding='utf-8',errors='replace').read(),re.M)
            if m: guid2path[m.group(1)]=p[:-5]
print(len(guid2path),'guids dans le pack')
# scene: re-read docs for kept roots
D=pickle.load(open('scene.pkl','rb')); GO,TR=D['GO'],D['TR']
X=pickle.load(open('world.pkl','rb')); children,roots=X['children'],X['roots']
keep_roots=[r for r in roots if GO[TR[r]['go']]['name'].strip("'") in ('Map','@Properties','@Businesses')]
keep_tr=set(); stack=list(keep_roots)
while stack:
    j=stack.pop(); keep_tr.add(j); stack+=children.get(j,[])
keep_go={TR[t]['go'] for t in keep_tr}
print(len(keep_go),'objets gardes')
pickle.dump(dict(guid2path=guid2path,keep_go=keep_go,keep_tr=keep_tr),open('keep.pkl','wb'))
