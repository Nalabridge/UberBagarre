import re, numpy as np
FMT_SIZE={0:4,1:2,2:1,3:1,4:2,5:2,6:1,7:1,8:2,9:2,10:4,11:4}
def _f(s):
    return float(s)
def parse_mesh(text):
    m={}
    m['name']=re.search(r'\n  m_Name: (.*)',text).group(1)
    subs=[]
    sm=text.split('\n  m_SubMeshes:\n',1)[1].split('\n  m_Shapes:',1)[0]
    for blk in sm.split('  - serializedVersion: 2\n')[1:]:
        d={k:int(v) for k,v in re.findall(r'    (firstByte|indexCount|topology|baseVertex|firstVertex|vertexCount): (-?\d+)',blk)}
        subs.append(d)
    m['subs']=subs
    m['index_format']=int(re.search(r'\n  m_IndexFormat: (\d)',text).group(1))
    ib=re.search(r'\n  m_IndexBuffer: ?([0-9a-f]*)\n',text).group(1)
    m['indices']=np.frombuffer(bytes.fromhex(ib),dtype=np.uint16 if m['index_format']==0 else np.uint32)
    vd=text.split('\n  m_VertexData:\n',1)[1].split('\n  m_CompressedMesh:',1)[0]
    m['vcount']=int(re.search(r'm_VertexCount: (\d+)',vd).group(1))
    ch=[]
    for s,o,f,d in re.findall(r'- stream: (\d+)\n      offset: (\d+)\n      format: (\d+)\n      dimension: (\d+)',vd):
        ch.append(tuple(int(x) for x in (s,o,f,d)))
    m['channels']=ch
    td=re.search(r'_typelessdata: ?([0-9a-f]*)',vd).group(1)
    m['data']=bytes.fromhex(td)
    m['vd_text']=vd
    return m
def stream_layout(channels,vcount):
    # returns list of (stride, start_offset) per stream
    streams={}
    for s,o,f,d in channels:
        if d==0: continue
        size=FMT_SIZE[f]*(d&15)
        streams[s]=max(streams.get(s,0),o+size)
    lay={}; off=0
    for s in sorted(streams):
        stride=streams[s]
        lay[s]=(stride,off)
        off+=stride*vcount
        off=(off+15)//16*16
    return lay
def vertex_arrays(m):
    """returns (lay, per-stream uint8 arrays [vcount,stride])"""
    lay=stream_layout(m['channels'],m['vcount'])
    arrs={}
    for s,(stride,off) in lay.items():
        arrs[s]=np.frombuffer(m['data'],dtype=np.uint8,count=stride*m['vcount'],offset=off).reshape(m['vcount'],stride)
    return lay,arrs
def channel_view(m,arrs,ci):
    s,o,f,d=m['channels'][ci]
    if d==0: return None
    assert f==0, 'format %d'%f
    a=np.ascontiguousarray(arrs[s][:,o:o+4*(d&15)]).view(np.float32).reshape(-1,d&15)
    return a
def pack_streams(lay,arrs,vcount):
    out=bytearray()
    for s in sorted(lay):
        out+=arrs[s].tobytes()
        pad=(-len(out))%16
        # padding only between streams
        if s!=max(lay): out+=b'\0'*pad
    return bytes(out)
def fmt(x):
    return ('%.9g'%x).replace('e+','E+').replace('e-','E-')
def aabb_text(indent,c,e):
    sp=' '*indent
    return (sp+'m_Center: {x: %s, y: %s, z: %s}\n'%tuple(fmt(v) for v in c)+sp+'m_Extent: {x: %s, y: %s, z: %s}'%tuple(fmt(v) for v in e))
def build_mesh_doc(template_text,fileid,name,subs,vcount,data,indices,index_format,aabbs,total_aabb):
    t=template_text
    head,rest=t.split('\n  m_SubMeshes:\n',1)
    rest=rest.split('\n  m_Shapes:',1)[1]
    head=re.sub(r'\n  m_Name: .*','\n  m_Name: '+yaml_str(name),head)
    sm=''
    for d,(c,e) in zip(subs,aabbs):
        sm+=('  - serializedVersion: 2\n    firstByte: %d\n    indexCount: %d\n    topology: %d\n    baseVertex: 0\n    firstVertex: %d\n    vertexCount: %d\n    localAABB:\n'%(d['firstByte'],d['indexCount'],d.get('topology',0),d['firstVertex'],d['vertexCount']))+aabb_text(6,c,e)+'\n'
    body=head+'\n  m_SubMeshes:\n'+sm+'  m_Shapes:'+rest
    body=re.sub(r'\n  m_IndexFormat: \d','\n  m_IndexFormat: %d'%index_format,body)
    ib=indices.astype(np.uint16 if index_format==0 else np.uint32).tobytes().hex()
    body=re.sub(r'\n  m_IndexBuffer: ?[0-9a-f]*\n',lambda _: '\n  m_IndexBuffer: '+ib+'\n',body)
    body=re.sub(r'(\n  m_VertexData:\n    serializedVersion: \d+\n    m_VertexCount: )\d+',lambda mm: mm.group(1)+str(vcount),body)
    body=re.sub(r'\n    m_DataSize: \d+','\n    m_DataSize: %d'%len(data),body)
    body=re.sub(r'_typelessdata: ?[0-9a-f]*',lambda _: '_typelessdata: '+data.hex(),body)
    body=re.sub(r'\n  m_LocalAABB:\n    m_Center: .*\n    m_Extent: .*',lambda _: '\n  m_LocalAABB:\n'+aabb_text(4,*total_aabb),body)
    body=re.sub(r'\n  m_MeshMetrics\[0\]: .*','\n  m_MeshMetrics[0]: 1',body)
    body=re.sub(r'\n  m_MeshMetrics\[1\]: .*','\n  m_MeshMetrics[1]: 1',body)
    body=re.sub(r'\n  m_IsReadable: \d','\n  m_IsReadable: 1',body)
    return '--- !u!43 &%d\n'%fileid+body
def yaml_str(s):
    s=s.strip()
    if s.startswith("'") or s.startswith('"'): return s
    if re.search(r'[:#\[\]{},&*!|>%@`]',s) or s!=s.strip(): return "'"+s.replace("'","''")+"'"
    return s
