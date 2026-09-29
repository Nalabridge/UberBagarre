# Correctif « URP » du pack de la carte : les matériaux D'ORIGINE de Schedule 1.
#
# Le pack v1 avait converti chaque matériau vers le rendu intégré (Standard). Depuis que le jeu
# passe sous URP (le moteur de rendu de Schedule 1), ce correctif remet les matériaux du jeu
# d'origine tels quels :
#   - URP Lit / Complex Lit / Simple Lit (la grande majorité) : le fichier d'origine, rebranché sur le vrai
#     shader d'URP (l'export avait donné au shader un autre identifiant) ;
#   - le Shader Graph triplanaire (murs, briques) : le fichier d'origine, sur notre shader
#     « UberBagarre/Carte/Triplanaire » qui lit les mêmes propriétés ;
#   - le reste (shaders d'assets payants, matériaux absents de l'export) : converti vers URP Lit
#     ou vers nos shaders de la carte, comme dans la v1.
# Et les textures reprennent leur taille d'origine jusqu'à 2048 px (1024 dans la v1).
#
# À lancer dans le dossier de travail du pack (keep.pkl, texjobs.pkl, src/, out/Schedule1),
# APRÈS pack.py et textures.py. Produit out/CorrectifURP/Schedule1 et out/CorrectifURP.zip.
import os, re, sys, shutil, pickle, zipfile
from multiprocessing import Pool
from PIL import Image
from matparse import parse_mat
import convmat

Image.MAX_IMAGE_PIXELS = None
PACK = 'out/Schedule1'
OUT = 'out/CorrectifURP/Schedule1'
MAX = 2048

# Les vrais shaders d'URP (identifiants fixes du paquet com.unity.render-pipelines.universal).
URP_LIT = '933532a4fcc9baf4fa0491de14d08ed7'
URP_SIMPLE_LIT = '8d2bb70cbf9db8d4da26e15b26e74248'
URP_COMPLEX_LIT = 'ee7e4c9a5f6364b688a332c67fc32cca'
# Le petit objet « version du matériau » qu'URP range dans chaque .mat (sinon, à l'import, URP
# croit le matériau très ancien et le « met à niveau » en écrasant des réglages).
URP_ASSET_VERSION_SCRIPT = 'd0353a89b1f911e48b9e16bdc9f2e058'
URP_MATERIAL_VERSION = 7

# Shaders de l'export de Schedule 1 -> que faire.
LIT = {'8fefca38545dd814e9f5fe41b369c2c7'}
COMPLEX_LIT = {'63d40d552e036df4f927047c27662c72'}
SIMPLE_LIT = {'d22b679de8dd25f4aaec48ee06ba9faf'}
TRIPLANAR = {'3c6c31705f3a5bb4c90a96af8cd8de56'}
STANDARD_GUID = '0000000000000000f000000000000000'

ROLES = {
    '_BaseMap': 'albedo', '_MainTex': 'albedo', '_EmissionMap': 'albedo', '_DetailAlbedoMap': 'albedo', '_DiffuseTexture': 'albedo',
    '_BumpMap': 'normal', '_DetailNormalMap': 'normal', '_NormalTexture': 'normal',
    '_MetallicGlossMap': 'data', '_SpecGlossMap': 'data', '_OcclusionMap': 'data', '_ParallaxMap': 'data',
    '_DetailMask': 'data', '_ClearCoatMap': 'data', '_Metallic_Map': 'data',
}

K = pickle.load(open('keep.pkl', 'rb'))
g2p = K['guid2path']
v1jobs = pickle.load(open('texjobs.pkl', 'rb'))['texjobs']


def asset_version_doc():
    return ('--- !u!114 &-4917235811066283215\nMonoBehaviour:\n  m_ObjectHideFlags: 11\n  m_CorrespondingSourceObject: {fileID: 0}\n'
            '  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n'
            '  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: \n'
            '  version: %d\n' % (URP_ASSET_VERSION_SCRIPT, URP_MATERIAL_VERSION))


def set_float(text, key, value):
    """Écrit une valeur dans m_Floats (format de l'export : « _Nom: valeur »)."""
    pat = re.compile(r'(\n      %s: )(\S+)' % re.escape(key))
    if pat.search(text): return pat.sub(lambda m: m.group(1) + fmt(value), text, count=1)
    return text.replace('\n    m_Floats:\n', '\n    m_Floats:\n      %s: %s\n' % (key, fmt(value)), 1)


def get_float(text, key, default):
    m = re.search(r'\n      %s: (\S+)' % re.escape(key), text)
    try: return float(m.group(1)) if m else default
    except ValueError: return default


def fmt(v):
    return str(int(v)) if float(v).is_integer() else '%.6g' % v


def sanitize_legacy(text, simple=False):
    """Les propriétés « obsolètes » cachées d'URP recopiées sur les vraies : si URP rejoue un jour
    sa mise à niveau des vieux matériaux (qui recopie _Glossiness, _Color, _MainTex...), elle ne
    change plus rien."""
    smooth = get_float(text, '_Smoothness', 0.5)
    text = set_float(text, '_Glossiness', smooth)
    text = set_float(text, '_GlossMapScale', smooth)
    if simple: text = set_float(text, '_Shininess', smooth)
    text = set_float(text, '_GlossyReflections', get_float(text, '_EnvironmentReflections', 1))
    base = re.search(r'\n      _BaseColor: (\{[^}]*\})', text)
    if base: text = re.sub(r'(\n      _Color: )\{[^}]*\}', lambda m: m.group(1) + base.group(1), text, count=1)
    basemap = re.search(r'\n      _BaseMap:\n(        m_Texture: .*\n        m_Scale: .*\n        m_Offset: .*)\n', text)
    if basemap: text = re.sub(r'(\n      _MainTex:\n)        m_Texture: .*\n        m_Scale: .*\n        m_Offset: .*\n',
                              lambda m: m.group(1) + basemap.group(1) + '\n', text, count=1)
    return text


def with_shader(text, guid):
    return re.sub(r'm_Shader: \{[^}]*\}', 'm_Shader: {fileID: 4800000, guid: %s, type: 3}' % guid, text, count=1)


def textures_of(text):
    """(propriété, guid) des textures d'un matériau au format de l'export."""
    sec = text.split('m_TexEnvs:', 1)[1].split('m_Ints:')[0].split('m_Floats:')[0] if 'm_TexEnvs:' in text else ''
    return re.findall(r'\n      (\w+):\n        m_Texture: \{fileID: 2800000, guid: ([0-9a-f]+)', sec)


# ------------------------------------------------------------------ URP Lit depuis un matériau v1 (Standard)
def parse_v1(text):
    """Relit un .mat écrit par convmat.write_mat (format liste : « - _Nom: valeur »)."""
    o = convmat.Out(re.search(r'm_Name: (.*)', text).group(1).strip())
    o.kw = re.findall(r'\n  - (\w+)', text.split('m_InvalidKeywords')[0])
    o.queue = int(re.search(r'm_CustomRenderQueue: (-?\d+)', text).group(1))
    o.instancing = int(re.search(r'm_EnableInstancingVariants: (\d)', text).group(1))
    o.flags = int(re.search(r'm_LightmapFlags: (\d+)', text).group(1))
    for k, g, sx, sy, ox, oy in re.findall(r'\n    - (\w+):\n        m_Texture: \{fileID: 2800000, guid: ([0-9a-f]+), type: 3\}\n'
                                            r'        m_Scale: \{x: (\S+), y: (\S+)\}\n        m_Offset: \{x: (\S+), y: (\S+)\}', text):
        o.tex[k] = (g, (float(sx), float(sy)), (float(ox), float(oy)), k == '_BumpMap')
    fl = text.split('m_Floats:', 1)[1].split('m_Colors:')[0]
    for k, v in re.findall(r'\n    - (\w+): (\S+)', fl): o.floats[k] = float(v)
    co = text.split('m_Colors:', 1)[1]
    for k, r, g, b, a in re.findall(r'\n    - (\w+): \{r: (\S+), g: (\S+), b: (\S+), a: (\S+)\}', co):
        o.colors[k] = tuple(float(x) for x in (r, g, b, a))
    return o


def standard_to_urp(o):
    """Un matériau Standard (v1) -> URP Lit, mêmes réglages."""
    u = convmat.Out(o.name)
    u.shader = ('4800000', URP_LIT, 3)
    u.instancing = o.instancing
    u.flags = o.flags
    mode = int(o.floats.get('_Mode', 0))
    smooth = o.floats.get('_Glossiness', 0.5)
    color = o.colors.get('_Color', (1, 1, 1, 1))

    if '_MainTex' in o.tex:
        g, sc, of, _ = o.tex['_MainTex']
        u.tex['_BaseMap'] = (g, sc, of, False)
        u.tex['_MainTex'] = (g, sc, of, False)
    if '_BumpMap' in o.tex:
        u.tex['_BumpMap'] = o.tex['_BumpMap']
        u.kw.append('_NORMALMAP')
    if '_MetallicGlossMap' in o.tex:
        u.tex['_MetallicGlossMap'] = o.tex['_MetallicGlossMap']
        u.kw.append('_METALLICSPECGLOSSMAP')
    if '_OcclusionMap' in o.tex:
        u.tex['_OcclusionMap'] = o.tex['_OcclusionMap']
        u.kw.append('_OCCLUSIONMAP')
    if '_EMISSION' in o.kw:
        u.kw.append('_EMISSION')
        if '_EmissionMap' in o.tex: u.tex['_EmissionMap'] = o.tex['_EmissionMap']
    if '_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A' in o.kw: u.kw.append('_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A')

    u.colors.update(_BaseColor=color, _Color=color, _EmissionColor=o.colors.get('_EmissionColor', (0, 0, 0, 1)),
                    _SpecColor=(0.2, 0.2, 0.2, 1))
    u.floats.update(_WorkflowMode=1, _Smoothness=smooth, _Glossiness=smooth, _GlossMapScale=smooth,
                    _SmoothnessTextureChannel=o.floats.get('_SmoothnessTextureChannel', 0), _Metallic=o.floats.get('_Metallic', 0),
                    _BumpScale=o.floats.get('_BumpScale', 1), _OcclusionStrength=o.floats.get('_OcclusionStrength', 1),
                    _Cutoff=o.floats.get('_Cutoff', 0.5), _Cull=2, _EnvironmentReflections=1, _GlossyReflections=1,
                    _SpecularHighlights=1, _ReceiveShadows=1, _QueueOffset=0, _BlendModePreserveSpecular=1, _AlphaToMask=0,
                    _SrcBlend=1, _DstBlend=0, _SrcBlendAlpha=1, _DstBlendAlpha=0, _ZWrite=1, _Surface=0, _Blend=0, _AlphaClip=0)
    u.tags = {'RenderType': 'Opaque'}
    u.queue = -1
    if mode == 1:
        u.floats['_AlphaClip'] = 1
        u.kw.append('_ALPHATEST_ON')
        u.tags = {'RenderType': 'TransparentCutout'}
        u.queue = 2450
    elif mode in (2, 3):
        u.floats.update(_Surface=1, _ZWrite=0, _DstBlend=10, _DstBlendAlpha=10)
        u.kw.append('_SURFACE_TYPE_TRANSPARENT')
        if mode == 2: u.floats.update(_Blend=0, _SrcBlend=5)
        else:
            u.floats.update(_Blend=1, _SrcBlend=1)
            u.kw.append('_ALPHAPREMULTIPLY_ON')
        u.tags = {'RenderType': 'Transparent'}
        u.queue = 3000
    return u


# ------------------------------------------------------------------ textures
def texture_job(item):
    g, roles, dst_name, ext = item
    src = g2p[g]
    try:
        im = Image.open(src); im.load()
    except Exception as e:
        return (g, 'ERR ' + str(e))
    normal = 'normal' in roles
    data = ('data' in roles or 'mask' in roles) and not normal
    w, h = im.size
    s = min(1.0, MAX / max(w, h))
    if s < 1: im = im.resize((max(1, int(round(w * s))), max(1, int(round(h * s)))), Image.LANCZOS)
    alpha = False
    if im.mode in ('RGBA', 'LA', 'P'):
        im = im.convert('RGBA'); lo, hi = im.getchannel('A').getextrema(); alpha = lo < 250
    base = dst_name or os.path.splitext(os.path.basename(src))[0]
    # Une texture déjà dans le pack garde son fichier (même nom, même format), sinon Unity
    # verrait deux fichiers pour un même identifiant.
    if ext: alpha = ext == '.png'
    if alpha:
        dst = os.path.join(OUT, 'Textures', base + '.png'); im.save(dst, optimize=False, compress_level=6)
    else:
        dst = os.path.join(OUT, 'Textures', base + '.jpg'); im.convert('RGB').save(dst, quality=90, subsampling=0 if normal else 2)
    meta = open(src + '.meta', encoding='utf-8').read()
    meta = re.sub(r'\n  textureType: \d+', '\n  textureType: %d' % (1 if normal else 0), meta)
    meta = re.sub(r'\n    sRGBTexture: \d', '\n    sRGBTexture: %d' % (0 if (normal or data) else 1), meta)
    meta = re.sub(r'maxTextureSize: (\d+)', lambda m: 'maxTextureSize: %d' % min(int(m.group(1)), MAX), meta)
    meta = re.sub(r'\n  spriteMode: \d', '\n  spriteMode: 0', meta)
    meta = re.sub(r'\n  alphaIsTransparency: \d', '\n  alphaIsTransparency: %d' % (1 if alpha and not normal and not data else 0), meta)
    if not alpha: meta = re.sub(r'\n  alphaUsage: \d', '\n  alphaUsage: 0', meta)
    open(dst + '.meta', 'w', encoding='utf-8', newline='\n').write(meta)
    return (g, os.path.getsize(dst))


def main():
    if os.path.exists('out/CorrectifURP'): shutil.rmtree('out/CorrectifURP')
    for d in ('Materiaux', 'Shaders', 'Textures'): os.makedirs(os.path.join(OUT, d))

    # --- matériaux : ceux du pack v1, un par un (même identifiant, donc mêmes références)
    v1 = {}
    for f in sorted(os.listdir(os.path.join(PACK, 'Materiaux'))):
        if not f.endswith('.mat'): continue
        meta = open(os.path.join(PACK, 'Materiaux', f + '.meta')).read()
        v1[re.search(r'guid: (\w+)', meta).group(1)] = f[:-4]

    stats = {'origine URP Lit': 0, 'origine Complex Lit': 0, 'origine Simple Lit': 0, 'origine triplanaire': 0, 'converti URP Lit': 0, 'nos shaders': 0}
    roles = {}
    used_names = set()

    def unique(name):
        # Unique même sans tenir compte de la casse (Windows).
        base, n = name, 1
        while name.lower() in used_names:
            n += 1; name = '%s_%d' % (base, n)
        used_names.add(name.lower())
        return name

    def need(g, role):
        if g in g2p: roles.setdefault(g, set()).add(role)

    for g, name in sorted(v1.items(), key=lambda kv: kv[1].lower()):
        src = g2p.get(g)
        orig = open(src, encoding='utf-8').read() if src and src.endswith('.mat') else None
        sh = parse_mat(orig)['shader'] if orig else None
        sh = sh[1] if sh else None

        if sh in LIT or sh in COMPLEX_LIT or sh in SIMPLE_LIT:
            simple = sh in SIMPLE_LIT
            target = URP_SIMPLE_LIT if simple else (URP_COMPLEX_LIT if sh in COMPLEX_LIT else URP_LIT)
            text = sanitize_legacy(with_shader(orig, target), simple)
            text = text.rstrip('\n') + '\n' + asset_version_doc()
            stats['origine Simple Lit' if simple else ('origine Complex Lit' if sh in COMPLEX_LIT else 'origine URP Lit')] += 1
            for k, tg in textures_of(text):
                if k in ROLES: need(tg, ROLES[k])
        elif sh in TRIPLANAR:
            text = with_shader(orig, convmat.SH_TRI)
            stats['origine triplanaire'] += 1
            for k, tg in textures_of(text):
                if k in ('_DiffuseTexture', '_NormalTexture'): need(tg, ROLES[k])
        else:
            text = open(os.path.join(PACK, 'Materiaux', name + '.mat'), encoding='utf-8').read()
            if STANDARD_GUID in re.search(r'm_Shader: .*', text).group(0):
                u = standard_to_urp(parse_v1(text))
                text = convmat.write_mat(u).rstrip('\n') + '\n' + asset_version_doc()
                stats['converti URP Lit'] += 1
                for k, (tg, sc, of, nm) in u.tex.items(): need(tg, 'normal' if nm else ROLES.get(k, 'albedo'))
            else:
                stats['nos shaders'] += 1
                for k, tg in re.findall(r'\n    - (\w+):\n        m_Texture: \{fileID: 2800000, guid: ([0-9a-f]+)', text):
                    need(tg, 'normal' if k == '_BumpMap' else ROLES.get(k, 'albedo'))

        out_name = unique(name)
        path = os.path.join(OUT, 'Materiaux', out_name + '.mat')
        with open(path, 'w', encoding='utf-8', newline='\n') as f: f.write(text if text.endswith('\n') else text + '\n')
        with open(path + '.meta', 'w', newline='\n') as f:
            f.write('fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n'
                    '  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % g)
    print('materiaux', stats)

    # --- shaders de la carte (mêmes identifiants que la v1)
    here = os.path.dirname(os.path.abspath(__file__))
    for sname, sg in (('CarteTriplanaire', convmat.SH_TRI), ('CarteDoubleFace', convmat.SH_DBL), ('CarteEau', convmat.SH_EAU)):
        shutil.copy(os.path.join(here, '..', 'shaders', sname + '.shader'), os.path.join(OUT, 'Shaders', sname + '.shader'))
        with open(os.path.join(OUT, 'Shaders', sname + '.shader.meta'), 'w', newline='\n') as f:
            f.write('fileFormatVersion: 2\nguid: %s\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n'
                    '  nonModifiableTextures: []\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % sg)

    # --- textures : les nouvelles, celles qui grandissent (> 1024 px), celles qui changent de rôle
    v1_names = {}
    for f in os.listdir(os.path.join(PACK, 'Textures')):
        if f.endswith('.meta'):
            m = re.search(r'guid: (\w+)', open(os.path.join(PACK, 'Textures', f)).read())
            if m: v1_names[m.group(1)] = os.path.splitext(f[:-5])
    taken = {n.lower() for n, _ in v1_names.values()}
    for g, r in v1jobs.items(): roles.setdefault(g, set()).update(r)

    def kind(r):
        return 'normal' if 'normal' in r else ('data' if ('data' in r or 'mask' in r) else 'albedo')

    jobs = []
    for g, r in sorted(roles.items()):
        try: size = max(Image.open(g2p[g]).size)
        except Exception: continue
        if g in v1_names:
            if size <= 1024 and kind(r) == kind(v1jobs.get(g, r)): continue
            jobs.append((g, r, v1_names[g][0], v1_names[g][1]))
        else:
            base = os.path.splitext(os.path.basename(g2p[g]))[0]
            name, n = base, 1
            while name.lower() in taken: n += 1; name = '%s_%d' % (base, n)
            taken.add(name.lower())
            jobs.append((g, r, name, None))
    with Pool(8) as p:
        res = p.map(texture_job, jobs, chunksize=4)
    err = [x for x in res if isinstance(x[1], str)]
    print('textures', len(res) - len(err), 'erreurs', err[:5], 'Mo', sum(x[1] for x in res if not isinstance(x[1], str)) / 1e6)

    with open(os.path.join(OUT, 'LISEZMOI_URP.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write(README)

    with zipfile.ZipFile('out/CorrectifURP.zip', 'w', zipfile.ZIP_STORED) as z:
        for root, _, files in os.walk('out/CorrectifURP'):
            for fn in sorted(files):
                full = os.path.join(root, fn)
                z.write(full, os.path.relpath(full, 'out/CorrectifURP'))
    print('zip', os.path.getsize('out/CorrectifURP.zip') / 1e6, 'Mo')


README = '''Über Bagarre — correctif « URP » de la ville (les matériaux d'origine de Schedule 1)

Le jeu passe sous URP, le moteur de rendu de Schedule 1. Ce correctif remplace les matériaux
convertis de la v1 par ceux du jeu d'origine, tels quels, et rend aux textures leur taille
d'origine (jusqu'à 2048 px).

Installation :
1. D'abord, dans Unity : menu « Uber Bagarre > 0 - Passer sous URP (le rendu de Schedule 1) »
   (le paquet URP doit être là avant ces matériaux). Puis fermer Unity.
2. Supprimer les dossiers Assets/Schedule1/Materiaux et Assets/Schedule1/Shaders.
3. Dézipper ce correctif dans Assets/ (il contient un dossier « Schedule1 ») : accepter de
   remplacer les textures existantes.
4. Rouvrir Unity, puis « Uber Bagarre > 3b - Construire le MONDE OUVERT ».
'''

if __name__ == '__main__':
    main()
