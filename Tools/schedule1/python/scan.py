import re, pickle, collections
hdr=re.compile(r'^--- !u!(\d+) &(-?\d+)(.*)')
K=pickle.load(open('keep.pkl','rb')); keep_go=K['keep_go']
docs=[]; cur=None; buf=[]
with open('Main.unity',encoding='utf-8',errors='replace') as f:
    head=[]
    for line in f:
        if line.startswith('--- !u!'):
            if cur: docs.append((cur[0],cur[1],cur[2],''.join(buf)))
            m=hdr.match(line); cur=(m.group(1),m.group(2),m.group(3)); buf=[]
        elif cur is None: head.append(line)
        else: buf.append(line)
    docs.append((cur[0],cur[1],cur[2],''.join(buf)))
print('header', head)
allc=collections.Counter(d[0] for d in docs)
print('classes', allc.most_common())
print('stripped', sum(1 for d in docs if d[2].strip()))
kc=collections.Counter(); byname={}
gore=re.compile(r'm_GameObject: \{fileID: (-?\d+)')
for c,i,extra,s in docs:
    first=s.split('\n',1)[0]; byname[c]=first
    if c=='1':
        if i in keep_go: kc['1 GameObject']+=1
        continue
    m=gore.search(s)
    if m and m.group(1) in keep_go: kc[c+' '+first]+=1
print('kept', kc.most_common())
pickle.dump(docs,open('docs.pkl','wb'),protocol=4)
