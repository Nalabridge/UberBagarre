# Banc de test : retire (dans la copie locale d'URP) les surcharges « half » en double pour glslang.
import re, subprocess, sys, os
ROOT=os.path.abspath('root')
src=sys.argv[1]; stage=sys.argv[2]; entry=sys.argv[3]; defs=sys.argv[4:]
for it in range(400):
    cmd=['glslangValidator','-D','-V','--target-env','vulkan1.1','-S',stage,'-e',entry,'-I'+ROOT,'-o','/tmp/_a.spv']+['-D'+d for d in defs]+[src]
    out=subprocess.run(cmd,capture_output=True,text=True)
    msg=out.stdout+out.stderr
    m=re.search(r'ERROR: (\S+?):(\d+): \'(\w+)\' : function already has a body',msg)
    if not m:
        print('\n'.join(l for l in msg.splitlines() if 'ERROR' in l or 'WARN' in l)[:3000] or 'OK'); break
    f,line=m.group(1),int(m.group(2))
    if not f.startswith(ROOT+'/Packages'): print('dans notre code :',msg[:2000]); break
    L=open(f).read().split('\n')
    i=line-1
    if L[i].lstrip().startswith('TEMPLATE'):
        L[i]='// [banc] '+L[i]
    else:
        # retirer la fonction : de la ligne jusqu'à l'accolade fermante équilibrée
        if L[i].strip().startswith('{'):
            i-=1
            while i>0 and L[i-1].strip() and not L[i-1].rstrip().endswith((';','}','{')) and not L[i-1].lstrip().startswith(('#','//')): i-=1
        depth=0; started=False; j=i
        while j<len(L):
            depth+=L[j].count('{'); 
            if '{' in L[j]: started=True
            depth-=L[j].count('}')
            L[j]='// [banc] '+L[j]
            if started and depth<=0: break
            j+=1
    open(f,'w').write('\n'.join(L))
else:
    print('trop d iterations')
