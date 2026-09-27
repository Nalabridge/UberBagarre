import os, re, collections, sys
import numpy as np
from unitymesh import parse_mesh, vertex_arrays, stream_layout
OUT='out/Schedule1'
guids={}
for root,_,fs in os.walk(OUT):
    for f in fs:
        if f.endswith('.meta'):
            g=re.search(r'guid: ([0-9a-f]{32})',open(os.path.join(root,f)).read()).group(1)
            if g in guids: print('GUID DUPLIQUE',g,guids[g],f)
            guids[g]=os.path.join(root,f[:-5])
print('guids',len(guids))
BUILTIN={'0000000000000000e000000000000000','0000000000000000f000000000000000'}
# 1. scene
txt=open(os.path.join(OUT,'Carte/CarteSchedule1.unity'),encoding='utf-8').read()
docs=re.split(r'\n(?=--- !u!)',txt)
ids={}; bad=collections.Counter()
for d in docs[1:] if not docs[0].startswith('---') else docs:
    m=re.match(r'--- !u!(\d+) &(-?\d+)',d)
    if not m: continue
    ids[m.group(2)]=(m.group(1),d)
print('docs',len(ids),collections.Counter(c for c,_ in ids.values()).most_common())
for i,(c,d) in ids.items():
    for f in re.findall(r'\{fileID: (-?\d+)\}',d):
        if f!='0' and f not in ids: bad['local manquant '+c]+=1
    for f,g in re.findall(r'\{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: \d\}',d):
        if g not in BUILTIN and g not in guids: bad['guid manquant '+c]+=1
    if c=='1':
        for comp in re.findall(r'component: \{fileID: (-?\d+)\}',d):
            if comp not in ids: bad['composant manquant']+=1
            elif not re.search(r'm_GameObject: \{fileID: %s\}'%i,ids[comp][1]): bad['composant pas a lui']+=1
    if c=='4':
        fa=re.search(r'm_Father: \{fileID: (-?\d+)\}',d).group(1)
        if fa!='0':
            if i not in re.findall(r'- \{fileID: (-?\d+)\}',ids[fa][1].split('m_Children',1)[1].split('m_Father')[0]): bad['enfant non liste']+=1
print('problemes scene',dict(bad))
# 2. materiaux
bad=collections.Counter()
for f in os.listdir(os.path.join(OUT,'Materiaux')):
    if f.endswith('.mat'):
        t=open(os.path.join(OUT,'Materiaux',f),encoding='utf-8').read()
        for g in re.findall(r'guid: ([0-9a-f]{32})',t):
            if g not in BUILTIN and g not in guids: bad['tex manquante']+=1
print('problemes materiaux',dict(bad))
# 3. prefabs / terrain layers
for d in ('Vegetation','Terrain'):
    for f in os.listdir(os.path.join(OUT,d)):
        if f.endswith('.meta') or f.endswith('.asset') and 'Terrain' in f and os.path.getsize(os.path.join(OUT,d,f))>10e6: continue
        t=open(os.path.join(OUT,d,f),encoding='utf-8').read()
        miss=[g for g in re.findall(r'guid: ([0-9a-f]{32})',t) if g not in BUILTIN and g not in guids]
        if miss: print('manquant dans',f,set(miss))
# 4. maillages decoupes
nb=0; badm=collections.Counter()
for f in sorted(os.listdir(os.path.join(OUT,'Maillages/Decoupes')))[:int(sys.argv[1]) if len(sys.argv)>1 else 999]:
    if not f.endswith('.asset'): continue
    t=open(os.path.join(OUT,'Maillages/Decoupes',f),encoding='utf-8').read()
    for d in re.split(r'\n(?=--- !u!43)',t)[1:] if not t.startswith('--- ') else []:
        pass
    parts=t.split('--- !u!43 &')[1:]
    for p in parts:
        body=p.split('\n',1)[1]
        m=parse_mesh(body); lay=stream_layout(m['channels'],m['vcount'])
        size=max(off+stride*m['vcount'] for stride,off in lay.values())
        if size!=len(m['data']): badm['taille']+=1
        ds=int(re.search(r'm_DataSize: (\d+)',body).group(1))
        if ds!=len(m['data']): badm['datasize']+=1
        if len(m['indices']) and m['indices'].max()>=m['vcount']: badm['index']+=1
        tot=sum(s['indexCount'] for s in m['subs'])
        if tot!=len(m['indices']): badm['nb index']+=1
        for s in m['subs']:
            es=2 if m['index_format']==0 else 4
            ii=m['indices'][s['firstByte']//es:s['firstByte']//es+s['indexCount']]
            if len(ii) and (ii.min()<s['firstVertex'] or ii.max()>=s['firstVertex']+s['vertexCount']): badm['plage sous-maillage']+=1
        nb+=1
print('maillages verifies',nb,dict(badm))
