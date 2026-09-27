import pickle, re, collections
docs=pickle.load(open('docs.pkl','rb'))
byid={}; comps=collections.defaultdict(list)
gore=re.compile(r'm_GameObject: \{fileID: (-?\d+)')
for c,i,extra,s in docs:
    byid[i]=(c,s)
    if c!='1':
        m=gore.search(s)
        if m: comps[m.group(1)].append(i)
pickle.dump(dict(byid=byid,comps=dict(comps)),open('index.pkl','wb'),protocol=4)
print(len(byid))
