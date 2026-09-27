# Points utiles de la carte (spawns, parkings, distributeurs, points de livraison...) -> points.pkl
import pickle, collections, re, numpy as np, math, json
D=pickle.load(open('scene.pkl','rb')); X=pickle.load(open('world.pkl','rb'))
GO,TR=D['GO'],D['TR']; W,children,roots,go2tr=X['W'],X['children'],X['roots'],X['go2tr']
suf=re.compile(r'\s*(\(\d+\)|_\d+|\.\d+)$')
tr2go={t:v['go'] for t,v in TR.items()}
parent={}
for p,cs in children.items():
    for c in cs: parent[c]=p
def path(t):
    out=[]
    while t is not None:
        out.append(GO[tr2go[t]]['name'].strip("'")); t=parent.get(t)
    return '/'.join(reversed(out))
cats={'parking':r'^ParkingSpot$','bench':r'^(OutdoorBench|outdoorbench|Bench)$','stand':r'^(StandPoint|Stand point)$','smoke':r'^Smoke point$','atm':r'^ATM$','vending':r'^Vending ?Machine','busstop':r'^Bus ?[Ss]top','sit':r'^SitPoint$','spawn':r'^(SpawnPoint|InteriorSpawnPoint)$','customer':r'^CustomerStandPoint$'}
res=collections.defaultdict(list)
for t,(M,act) in W.items():
    n=suf.sub('',GO[tr2go[t]]['name'].strip("'"))
    for k,rx in cats.items():
        if re.search(rx,n):
            p=M[:3,3]; f=M[:3,2]; yaw=math.degrees(math.atan2(f[0],f[2]))
            res[k].append(dict(p=[round(float(x),2) for x in p],yaw=round(yaw,1),path=path(t)))
for k,v in res.items():
    print(k,len(v))
    for e in v[:6]: print('   ',e)
pickle.dump(dict(res),open('points.pkl','wb'))
