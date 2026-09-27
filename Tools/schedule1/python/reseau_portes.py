# Complète reseau.json avec toutes les portes classiques de la ville (à lancer après pack.py et reseau.py).
import re,json
t=open('out/Schedule1/Carte/CarteSchedule1.unity').read()
GO={};TR={};father={}
for d in re.split(r'\n--- !u!',t):
    m=re.match(r'(\d+) &(-?\d+)',d)
    if not m: continue
    cls,fid=m.groups()
    if cls=='1': GO[fid]=re.search(r'm_Name: (.*)',d).group(1).strip().strip("'")
    elif cls=='4':
        TR[fid]=re.search(r'm_GameObject: \{fileID: (-?\d+)\}',d).group(1)
        father[fid]=re.search(r'm_Father: \{fileID: (-?\d+)\}',d).group(1)
def path(tr):
    p=[]
    while tr and tr!='0':
        p.append(GO.get(TR.get(tr))); tr=father.get(tr)
    return '/'.join(reversed(p))
doors=sorted(p for p in (path(tr) for tr in TR) if re.search(r'/(Classical Wooden door( \(\d+\))?|MansionDoor( \(\d\))?)/Container$',p))
d=json.load(open('out/Schedule1/Carte/reseau.json'))
homes=set([d['home']['door']]+[x for q in d['properties'] for x in q.get('doors',[])])
d['doors']=[p for p in doors if p not in homes and 'Manor/House/MansionDoor (2)' not in p]
json.dump(d,open('out/Schedule1/Carte/reseau.json','w'),ensure_ascii=False,separators=(',',':'))
print(len(d['doors']),'portes')
