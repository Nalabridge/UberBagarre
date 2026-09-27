import os, re, pickle, sys
from multiprocessing import Pool
from PIL import Image
Image.MAX_IMAGE_PIXELS=None
K=pickle.load(open('keep.pkl','rb')); g2p=K['guid2path']
jobs=pickle.load(open('texjobs.pkl','rb'))['texjobs']
OUT='out/Schedule1/Textures'
MAX=1024
def work(item):
    g,roles=item
    src=g2p[g]
    try:
        im=Image.open(src); im.load()
    except Exception as e:
        return (g,'ERR '+str(e))
    normal='normal' in roles
    data=('data' in roles or 'mask' in roles) and not normal
    w,h=im.size; s=min(1.0,MAX/max(w,h))
    if s<1: im=im.resize((max(1,int(round(w*s))),max(1,int(round(h*s)))),Image.LANCZOS)
    alpha=False
    if im.mode in ('RGBA','LA','P'):
        im=im.convert('RGBA'); lo,hi=im.getchannel('A').getextrema(); alpha=lo<250
    base=os.path.splitext(os.path.basename(src))[0]
    if alpha:
        dst=os.path.join(OUT,base+'.png'); im.save(dst,optimize=False,compress_level=6)
    else:
        dst=os.path.join(OUT,base+'.jpg'); im.convert('RGB').save(dst,quality=90,subsampling=0 if normal else 2)
    meta=open(src+'.meta',encoding='utf-8').read()
    meta=re.sub(r'\n  textureType: \d+','\n  textureType: %d'%(1 if normal else 0),meta)
    meta=re.sub(r'\n    sRGBTexture: \d','\n    sRGBTexture: %d'%(0 if (normal or data) else 1),meta)
    meta=re.sub(r'maxTextureSize: (\d+)',lambda m:'maxTextureSize: %d'%min(int(m.group(1)),MAX),meta)
    meta=re.sub(r'\n  spriteMode: \d','\n  spriteMode: 0',meta)
    meta=re.sub(r'\n  alphaIsTransparency: \d','\n  alphaIsTransparency: %d'%(1 if alpha and not normal and not data else 0),meta)
    if not alpha: meta=re.sub(r'\n  alphaUsage: \d','\n  alphaUsage: 0',meta)
    open(dst+'.meta','w',encoding='utf-8',newline='\n').write(meta)
    return (g,os.path.getsize(dst))
if __name__=='__main__':
    os.makedirs(OUT,exist_ok=True)
    with Pool(8) as p:
        res=p.map(work,sorted(jobs.items()),chunksize=8)
    err=[r for r in res if isinstance(r[1],str)]
    print('ok',len(res)-len(err),'erreurs',err[:5],'total MB',sum(r[1] for r in res if not isinstance(r[1],str))/1e6)
