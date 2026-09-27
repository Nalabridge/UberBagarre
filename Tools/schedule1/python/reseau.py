import numpy as np, pickle, math, json, os
from scipy import ndimage
from PIL import Image, ImageFilter
Gr=pickle.load(open('graph.pkl','rb')); route=Gr['route']
d=np.load('top_0_5.npz'); X0,X1,Z0,Z1,RES=d['bounds']; rmin=d['rmin']; smin=d['smin']; H=d['height']; kind=d['kind']
NZ,NX=rmin.shape
OUT='out/Schedule1/Carte'
def pix(x,z):
    return int(np.clip((z-Z0)/RES,0,NZ-1)), int(np.clip((x-X0)/RES,0,NX-1))
# hauteur de route la plus proche (fenetre)
road_ok=rmin<1e8
dist_r,ind_r=ndimage.distance_transform_edt(~road_ok,return_indices=True)
side_ok=smin<1e8
dist_s,ind_s=ndimage.distance_transform_edt(~side_ok,return_indices=True)
def road_h(x,z):
    i,j=pix(x,z); ii,jj=ind_r[0][i,j],ind_r[1][i,j]; return float(rmin[ii,jj]),dist_r[i,j]*RES
def side_h(x,z):
    i,j=pix(x,z); ii,jj=ind_s[0][i,j],ind_s[1][i,j]; return float(smin[ii,jj]),dist_s[i,j]*RES
def resample(pts,step):
    pts=np.asarray(pts,float); out=[pts[0]]; acc=0.0
    for k in range(1,len(pts)):
        a,b=pts[k-1],pts[k]; L=np.linalg.norm(b-a)
        while acc+L>=step:
            t=(step-acc)/L; a=a+(b-a)*t; out.append(a); L=np.linalg.norm(b-a); acc=0.0
        acc+=L
    return np.array(out)
def smooth(pts,n=2):
    p=np.asarray(pts,float)
    for _ in range(n):
        q=np.empty((len(p)*2,2)); q[0::2]=0.75*p+0.25*np.roll(p,-1,0); q[1::2]=0.25*p+0.75*np.roll(p,-1,0); p=q
    return p
def offset(pts,off):
    p=np.asarray(pts,float); n=len(p)
    t=np.roll(p,-1,0)-np.roll(p,1,0); t/=np.maximum(np.linalg.norm(t,axis=1,keepdims=True),1e-6)
    right=np.stack([t[:,1],-t[:,0]],1)   # (x,z) : droite = (tz,-tx)
    return p+right*off
base=resample(route,1.0)
base=smooth(base,1)
base=resample(np.vstack([base,base[:1]]),1.0)[:-1]
def lane(off,step,hfun,maxdrop):
    p=offset(base,off); p=resample(np.vstack([p,p[:1]]),step)[:-1]
    out=[]
    for x,z in p:
        h,dd=hfun(x,z)
        if dd>maxdrop: h,_=road_h(x,z)
        out.append([float(x),h+0.02,float(z)])
    a=np.array(out); y=a[:,1].copy(); n=len(y)
    # hauteurs aberrantes (tablier du viaduc, auvent) : mediane glissante sur 15 points
    for it in range(3):
        med=np.array([np.median(y[[(k+j)%n for j in range(-7,8)]]) for k in range(n)])
        bad=np.abs(y-med)>0.6; y[bad]=med[bad]
    a[:,1]=y
    return [[round(float(x),2),round(float(yy),2),round(float(z),2)] for x,yy,z in a]
drive=lane(1.75,3.0,road_h,2.5)
walk=lane(4.9,2.0,side_h,1.5)
print('voitures',len(drive),'points ; pietons',len(walk))
P=pickle.load(open('points.pkl','rb'))
FR={'Next to parking garage':('À côté du parking couvert','voiture'),'Next to barbershop':('À côté du barbier','fume'),
'Behind Taco Ticklers':('Derrière le Taco Ticklers','boit'),'Behind Thompson construction and demolition':('Derrière Thompson Démolition','sentraine'),
'West Warehouse Wharf':("Quai de l'entrepôt ouest",'peche'),'North waterfront':('Front de mer nord','telephone'),
'Behind Hyland Range':('Derrière le stand de tir','fume'),"Behind Handy Hank's":('Derrière la quincaillerie','deal'),
'Skatepark':('Au skatepark','tague'),'Next to the courthouse':('À côté du tribunal','attend'),
'Brick warehouse at the docks':('Entrepôt en briques, aux docks','deal'),'Outside brown apartment block room #2':("Devant l'immeuble marron, porte 2",'dispute'),
"Next to Bud's Bar":('À côté du Bud\'s Bar','boit'),'Basketball court':('Au terrain de basket','sentraine'),
'Western wharf':('Quai ouest','peche'),'Next to the statue in the west park':('Statue du parc ouest','telephone'),
"Behind Randy's bait & tackle":('Derrière le magasin de pêche','fume'),'Behind pawn shop':('Derrière le prêteur sur gages','deal'),
'Next to Medical Center':('À côté du centre médical','attend'),'Wharf near french restaurant':('Quai du restaurant français','telephone'),
'Behind Gas-Mart':('Derrière le Gas-Mart','boit'),'Alleyway behind Top Tattoo':('Ruelle du tatoueur','tague'),
'Behind diner':('Derrière le diner','fume'),'Behind the casino':('Derrière le casino','telephone'),
'Behind the auto shop':('Derrière le garage auto','voiture'),'Construction site':('Sur le chantier','sentraine'),
'Behind the western Gas Mart':('Derrière le Gas-Mart ouest','dispute'),'Under west bridge':('Sous le pont ouest','deal'),
'In front of motel':('Devant le motel','attend'),'Behind Sauerkraut Supreme Pizzeria':('Derrière la pizzeria','boit'),
'Residential park':('Parc du quartier résidentiel','sentraine'),'Alleyway next to HAM legal services':("Ruelle du cabinet d'avocats",'fume'),
'Behind Bank':('Derrière la banque','telephone')}
spots=[]
for e in P['customer']:
    key=e['path'].split('/')[1]
    name,k=FR.get(key,(key,'attend'))
    x,y,z=e['p']; spots.append(dict(name=name,kind=k,p=[x,y,z],yaw=e['yaw']))
for e,label in ((P['atm'][0],'Au distributeur du centre'),(P['atm'][6],'Au distributeur de la poste'),(P['atm'][11],'Au distributeur du bar')):
    x,y,z=e['p']; yaw=e['yaw']; f=np.array([math.sin(math.radians(yaw)),math.cos(math.radians(yaw))])
    spots.append(dict(name=label,kind='distributeur',p=[round(x+f[0]*0.9,2),y,round(z+f[1]*0.9,2)],yaw=(yaw+180)%360,face=[x,y,z]))
parking=[[*e['p'],e['yaw']] for e in P['parking']]
def spawn(path):
    for e in P['spawn']:
        if e['path']==path: return [*e['p'],e['yaw']]
home=dict(name='Motel',inside=[-71.2,0.78,83.9,90.0],outside=spawn('@Properties/MotelRoom/SpawnPoint'),
          door='@Properties/MotelRoom/MotelRoom/Classical Wooden door/Container',
          room=[-74.0,0.72,81.8,-68.0,3.5,86.8],
          bed=[-72.85,0.72,82.65,-90.0],desk=[-70.45,0.72,86.4,0.0],wardrobe=[-71.0,0.72,82.15,0.0],letters=[-70.95,1.5,86.4,8.0])
props=[dict(name='Bungalow',price=6500,inside=[-173.4,-3.7,111.7,45.0],outside=spawn('@Properties/Bungalow/SpawnPoint'),
            bed=[-174.8,-3.7,110.15,-90.0],desk=[-175.55,-3.7,114.2,-90.0],wardrobe=[-172.9,-3.7,109.65,0.0],letters=[-175.55,-2.92,113.7,-82.0],
            doors=['@Properties/Bungalow/bungalow/Classical Wooden door/Container','@Properties/Bungalow/bungalow/Classical Wooden door (1)/Container'],
            open=[],bedWidth=1.45),
       dict(name='Manoir',price=85000,inside=[157.8,10.5,-54.6,150.0],outside=spawn('@Properties/Manor/SpawnPoint'),
            bed=[157.0,10.5,-52.25,0.0],desk=[160.65,10.5,-57.0,90.0],wardrobe=[157.8,10.5,-62.55,0.0],letters=[160.65,11.28,-56.5,98.0],
            doors=['@Properties/Manor/House/MansionDoor/Container','@Properties/Manor/House/MansionDoor (1)/Container'],
            open=['@Properties/Manor/Manor Gate/manor gate','@Properties/Manor/Manor Gate/manor gate (1)'],bedWidth=1.95)]
vertigo=dict(door=[-43.2,-3.0,157.0,-90.0],out=[-41.3,-4.0,157.0,90.0],sign=[-43.55,-0.35,157.0,90.0])
car=[-47.48,0.1,72.3,0.0]
cars=[]
for want in ('Medical center parking lot/Kerb (1)/ParkingSpot','Plaza/Corporate plaza parking lot/ParkingSpot (1)','Dealership Parking Lot/ParkingSpot (2)',
             'Slums Park/West parking lot/ParkingSpot (3)','North town/Pizzeria parking lot/Kerb (1)/ParkingSpot (1)','Construction yard/Construction parking/ParkingSpot',
             'HardwardStore/Parking lot/ParkingSpot','Parking lot/ParkingSpot (3)','Residential/Kennedy House/Kennedy House Parking/ParkingSpot'):
    for e in P['parking']:
        if e['path'].endswith(want): cars.append([*e['p'],e['yaw']])
L=lambda label,x,z,c=(1,1,1): dict(label=label,p=[x,z],color=list(c))
landmarks=[L('Chez toi (motel)',-68,84,(1,0.85,0.3)),L('Vertigo',-44,157,(0.95,0.3,0.7)),L('Taco Ticklers',-30,70),L('Laverie',-22,25),
    L('Lave-auto',-5,-20),L('Poste',47,2),L('Commissariat',15,36),L('Centre médical',104,61),L('Casino',19,89),L("Bud's Bar",-29,121),
    L('Bar',40,68),L('Diner',-6,40),L('Tribunal',79,24),L('Concession auto',26,-39),L('Docks',-80,-50),L('Skatepark',-45,78),
    L('Terrain de basket',-74,138),L('Quartier résidentiel',100,-100),L('Manoir',163,-57),L('Grange',190,-10),L('Bungalow',-170,115),
    L('Pizzeria',-28,145),L('Tatoueur',-134,69),L('Prêteur sur gages',-61,51)]
V=lambda lst:[dict(v=[float(x) for x in v]) for v in lst]
flat=lambda pts:dict(points=[float(c) for p in pts for c in p])
for s_ in spots: s_.setdefault('face',[])
data=dict(version=1,bounds=[float(X0),float(Z0),float(X1-X0),float(Z1-Z0)],play=[-200.0,-140.0,420.0,330.0],water=-6.5,home=home,properties=props,vertigo=vertigo,car=car,cars=V(cars),
          spots=spots,parking=V(parking),drive=[flat(drive)],walk=[flat(walk)],landmarks=landmarks,
          smoke=V([[*e['p'],e['yaw']] for e in P['smoke']]),atm=V([[*e['p'],e['yaw']] for e in P['atm']]),vending=V([[*e['p'],e['yaw']] for e in P['vending'] if e['p']!=[0.0,0.0,0.0]]))
json.dump(data,open(os.path.join(OUT,'reseau.json'),'w',encoding='utf-8'),ensure_ascii=False,indent=0)
open(os.path.join(OUT,'reseau.json.meta'),'w').write('fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'%__import__('convmat').guid_of('carte:reseau'))
print('spots',len(spots),'voitures garees',len(cars))
# ------------------------------------------------------------------ mini-carte stylisee
Hn=np.nan_to_num(H,nan=-50)
ground=ndimage.minimum_filter(Hn,size=31)
build=(kind==3)&(Hn-ground>2.2)
build=ndimage.binary_opening(build,np.ones((2,2)))
water=np.isnan(H)|(kind==5)
img=np.zeros((NZ,NX,3),np.float32)
land=np.array([0.13,0.17,0.13]); img[:]=land
shade=np.clip(0.85+(Hn-Hn[~water].mean())*0.01,0.7,1.2); img*=shade[...,None]
img[kind==2]=(0.33,0.34,0.36)
img[kind==1]=(0.47,0.48,0.51)
img[build]=(0.22,0.23,0.27)
edge=build&~ndimage.binary_erosion(build,np.ones((3,3)))
img[edge]=(0.34,0.35,0.40)
img[water]=(0.05,0.09,0.13)
coast=water&ndimage.binary_dilation(~water,np.ones((3,3)))
img[coast]=(0.10,0.17,0.22)
im=Image.fromarray(np.clip(img[::-1]*255,0,255).astype(np.uint8))
im=im.filter(ImageFilter.SMOOTH)
im.save(os.path.join(OUT,'CarteDessus.png'))
meta=open('out/Schedule1/Textures/'+sorted(f for f in os.listdir('out/Schedule1/Textures') if f.endswith('.png.meta'))[0],encoding='utf-8').read()
import re
meta=re.sub(r'guid: [0-9a-f]{32}','guid: '+__import__('convmat').guid_of('carte:dessus'),meta)
meta=re.sub(r'\n  textureType: \d+','\n  textureType: 0',meta); meta=re.sub(r'\n    enableMipMap: \d','\n    enableMipMap: 0',meta)
meta=re.sub(r'maxTextureSize: \d+','maxTextureSize: 2048',meta); meta=re.sub(r'\n    wrapU: \d','\n    wrapU: 1',meta); meta=re.sub(r'\n    wrapV: \d','\n    wrapV: 1',meta)
meta=re.sub(r'\n  nPOTScale: \d','\n  nPOTScale: 0',meta); meta=re.sub(r'\n  alphaIsTransparency: \d','\n  alphaIsTransparency: 0',meta)
meta=re.sub(r'\n    sRGBTexture: \d','\n    sRGBTexture: 1',meta); meta=re.sub(r'\n  isReadable: \d','\n  isReadable: 0',meta)
meta=re.sub(r'textureCompression: \d','textureCompression: 0',meta)
open(os.path.join(OUT,'CarteDessus.png.meta'),'w',newline='\n').write(meta)
im.crop((250,350,1300,1400)).save('minicarte_apercu.png')
