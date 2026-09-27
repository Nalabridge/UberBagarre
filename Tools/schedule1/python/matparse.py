import re
def parse_mat(s):
    out={'tex':{},'float':{},'color':{},'kw':[],'name':'','shader':None,'queue':None,'tags':{}}
    m=re.search(r'm_Name: (.*)',s); out['name']=m.group(1).strip()
    m=re.search(r'm_Shader: \{fileID: (-?\d+)(?:, guid: ([0-9a-f]+))?',s); out['shader']=m.groups() if m else None
    m=re.search(r'm_CustomRenderQueue: (-?\d+)',s); out['queue']=int(m.group(1)) if m else -1
    m=re.search(r'm_ValidKeywords:(.*?)\n  m_InvalidKeywords:(.*?)\n  m_',s,re.S)
    if m: out['kw']=re.findall(r'- (\S+)',m.group(1)+m.group(2))
    m=re.search(r'm_ShaderKeywords: (.*)',s)
    if m: out['kw']+=m.group(1).split()
    m=re.search(r'stringTagMap:\n((?:    .*\n)*)',s)
    if m:
        for k,v in re.findall(r'    (\w+): (.*)',m.group(1)): out['tags'][k]=v.strip()
    sec=s.split('m_TexEnvs:',1)[1] if 'm_TexEnvs:' in s else ''
    texpart=sec.split('m_Ints:')[0].split('m_Floats:')[0]
    for name,fid,guid,sx,sy,ox,oy in re.findall(r'\n      (\w+):\n        m_Texture: \{fileID: (-?\d+)(?:, guid: ([0-9a-f]+), type: \d)?\}\n        m_Scale: \{x: (\S+), y: (\S+)\}\n        m_Offset: \{x: (\S+), y: (\S+)\}',texpart):
        if fid!='0' and guid: out['tex'][name]=(guid,(float(sx),float(sy)),(float(ox),float(oy)))
    fl=s.split('m_Floats:',1)[1].split('m_Colors:')[0] if 'm_Floats:' in s else ''
    for k,v in re.findall(r'\n      (\w+): (\S+)',fl):
        try: out['float'][k]=float(v)
        except: pass
    co=s.split('m_Colors:',1)[1] if 'm_Colors:' in s else ''
    for k,r,g,b,a in re.findall(r'\n      (\w+): \{r: (\S+), g: (\S+), b: (\S+), a: (\S+)\}',co):
        out['color'][k]=tuple(float(x) for x in (r,g,b,a))
    return out
