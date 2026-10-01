import re, sys, subprocess, os, itertools
ROOT=os.path.abspath('root')
BASE_DEFS=['INTRINSIC_WAVEREADFIRSTLANE','SHADER_API_VULKAN','UNITY_VERSION=600000','SHADER_TARGET=35','UNITY_REVERSED_Z=1','UNITY_UV_STARTS_AT_TOP=1','UNITY_COLORSPACE_LINEAR=1']
def blocks(shader_text):
    """URP SubShader : (hlslinclude, [(pass_name, program)])"""
    subs=[m.start() for m in re.finditer(r'\n\s*SubShader\s*\n',shader_text)]
    out=[]
    for i,start in enumerate(subs):
        end=subs[i+1] if i+1<len(subs) else len(shader_text)
        body=shader_text[start:end]
        if 'UniversalPipeline' not in body: continue
        inc=re.search(r'HLSLINCLUDE(.*?)ENDHLSL',body,re.S)
        inc=inc.group(1) if inc else ''
        progs=[]
        for m in re.finditer(r'Name\s+"([^"]+)".*?HLSLPROGRAM(.*?)ENDHLSL',body,re.S):
            progs.append((m.group(1),m.group(2)))
        out.append((inc,progs))
    return out
def compile_one(src, stage, entry, defs, tag):
    path='/tmp/_t_%s.hlsl'%tag
    open(path,'w').write(src)
    cmd=['glslangValidator','-D','-V','--target-env','vulkan1.1','-S',stage,'-e',entry,'-I'+ROOT,'-o','/tmp/_t.spv']+['-D'+d for d in defs]+[path]
    r=subprocess.run(cmd,capture_output=True,text=True)
    msg=(r.stdout+r.stderr)
    errs=[l for l in msg.splitlines() if 'ERROR' in l]
    return r.returncode, errs
def keywords(prog):
    sets=[]
    for m in re.finditer(r'#pragma\s+(?:multi_compile|shader_feature)(?:_local)?(?:_fragment|_vertex)?\s+(.*)',prog):
        opts=[o for o in m.group(1).split() if o!='_']
        if opts: sets.append(opts)
    return sets
def variants(prog):
    sets=keywords(prog)
    yield []
    for s in sets:
        for o in s: yield [o]
    # un variant « tout allumé » réaliste
    yield [s[-1] for s in sets]
    yield ['_MAIN_LIGHT_SHADOWS_CASCADE','_FORWARD_PLUS','_SHADOWS_SOFT','_SCREEN_SPACE_OCCLUSION','FOG_LINEAR']
for f in sys.argv[1:]:
    text=open(f,encoding='utf-8').read()
    for inc,progs in blocks(text):
        for name,prog in progs:
            v=re.search(r'#pragma\s+vertex\s+(\w+)',prog) or re.search(r'#pragma\s+vertex\s+(\w+)',inc)
            fr=re.search(r'#pragma\s+fragment\s+(\w+)',prog) or re.search(r'#pragma\s+fragment\s+(\w+)',inc)
            src=inc+'\n'+prog
            seen=set(); bad=0
            for kw in variants(prog):
                key=tuple(sorted(kw))
                if key in seen: continue
                seen.add(key)
                for stage,entry,sd in (('vert',v.group(1),'SHADER_STAGE_VERTEX'),('frag',fr.group(1),'SHADER_STAGE_FRAGMENT')):
                    rc,errs=compile_one(src,stage,entry,BASE_DEFS+[sd]+list(kw),'x')
                    if rc!=0:
                        bad+=1
                        if bad<=3: print('  %s %s %s %s:\n    %s'%(os.path.basename(f),name,stage,kw,'\n    '.join(errs[:6])))
            print('%s / %s : %s'%(os.path.basename(f),name,'OK' if bad==0 else '%d echecs'%bad))
