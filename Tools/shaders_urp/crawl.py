import os, re, sys, urllib.request, concurrent.futures
BASE='https://raw.githubusercontent.com/Unity-Technologies/Graphics/6000.0/staging/'
ROOT='root'
seen=set()
def fetch(path):
    dst=os.path.join(ROOT,path)
    if os.path.exists(dst): return open(dst,encoding='utf-8',errors='ignore').read()
    try:
        data=urllib.request.urlopen(BASE+path,timeout=30).read().decode('utf-8','ignore')
    except Exception as e:
        print('MISS',path,e); return None
    os.makedirs(os.path.dirname(dst),exist_ok=True)
    open(dst,'w',encoding='utf-8').write(data); return data
def deps(text):
    return re.findall(r'#\s*include(?:_with_pragmas)?\s+"(Packages/[^"]+)"',text or '')
todo=list(sys.argv[1:])
while todo:
    batch=[p for p in todo if p not in seen]; todo=[]
    for p in batch: seen.add(p)
    with concurrent.futures.ThreadPoolExecutor(16) as ex:
        for text in ex.map(fetch,batch):
            todo+=deps(text)
print('files',len(seen))
