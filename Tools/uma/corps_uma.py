"""
Les corps du jeu, fabriqués à partir d'UMA 2 (Unity Multipurpose Avatar, gratuit sur l'Asset
Store, utilisable dans un jeu) — à la place des anciens corps MakeHuman de Tools/corps.

    python3 Tools/uma/corps_uma.py --uma CHEMIN/UMA2_For_UMA3_f5.unitypackage [--seulement Athlete]

Pour chaque silhouette (le joueur, les adversaires, les femmes du scénario, la foule) :
1. les morceaux du corps UMA (visage, torse, mains, jambes, pieds, yeux) en haute définition,
   et en définition standard pour la foule (_Foule) ;
2. la morphologie : on règle les os « Adjust » d'UMA (bras, avant-bras, épaules, trapèzes,
   poitrine, ventre, cou, cuisses), exactement comme le fait l'ADN d'UMA ;
3. les vêtements : maillages UMA (sweat à capuche, jean, chaussures, tee-shirt et débardeur
   féminins) ou décalques taillés dans la peau (tee-shirt et débardeur d'homme, haut à
   manches longues, jean féminin, bandes de boxe) ; la peau cachée est marquée ;
4. le squelette du jeu (Tools/corps/rig.py) posé sur les articulations UMA, les poids UMA
   fusionnés sur ses os ;
5. les textures : peau et visages UMA (sourcils et cils peints dedans), tissus teintables ;
6. Corps_<nom>.bytes (format UBCORPS2, lu par Editor/CorpsImporter.cs) et Corps_<nom>.json
   (textures, tenues, teint).
Les coiffures et les barbes sont dans Tools/uma/coiffures.py.

Dépendances : numpy, scipy, pillow, UnityPy (pip install UnityPy).
"""
import argparse
import gzip
import json
import os
import struct
import sys

import numpy as np
from PIL import Image
from scipy.spatial import cKDTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', 'corps'))

import umalib  # noqa: E402
import rig  # noqa: E402

OUT_DIR = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', 'UberBagarre', 'Art', 'Models', 'Corps'))
MAGIC = b'UBCORPS2'

# Bits des masques de peau (mêmes valeurs que CorpsImporter.cs).
TSHIRT, VESTE, DEBARDEUR, JEAN, CHAUSSURES, TETE, BANDES = 1, 2, 4, 8, 16, 32, 64
TOP_BIT = {'TShirt': TSHIRT, 'Veste': VESTE, 'Debardeur': DEBARDEUR}

# ---------------------------------------------------------------------- contenu UMA

ROLES = ('face', 'torso', 'hands', 'legs', 'feet', 'eyes')

SEXES = {
    'H': dict(
        high=['Races/HumanMale/Slots/Body/HighPoly/Male_High_%s_Slot.asset' % n
              for n in ('Face', 'Torso', 'Hands', 'Legs', 'Feet', 'Eyes')],
        low=['Races/HumanMale/Slots/Body/UMA_Human_Male_%s_Slot.asset' % n
             for n in ('Face', 'Torso', 'Hands', 'Legs', 'Feet', 'Eyes')],
        lashes=None,
        body_albedo='Races/HumanMale/Textures/Body/M_H_body1_Albedo.png',
        body_normal='Races/HumanMale/Textures/Body/M_H_bod_N.png',
        face_normal='Races/HumanMale/Textures/Face/M_H_face_Nnew.png',
        faces=[('Adulte', 'Races/HumanMale/Textures/AlternateFaces/UMA3MaleAdult.png'),
               ('Variante1', 'Races/HumanMale/Textures/AlternateFaces/UMA2MaleVariation1.png'),
               ('Variante3', 'Races/HumanMale/Textures/AlternateFaces/UMA2MaleVariation3.png'),
               ('Teint', 'Races/HumanMale/Textures/AlternateFaces/UMA2MaleComplexion.png'),
               ('Use', 'Races/HumanMale/Textures/AlternateFaces/UMA2MaleHaggard.png'),
               ('Balafre', 'Races/HumanMale/Textures/AlternateFaces/UMA2MaleScarred1.png'),
               ('Age', 'Races/HumanMale/Textures/AlternateFaces/UMA2MaleAged.png')],
        face_layers=[('Wearables/Example/HumanMale/Overlays/Hair/MaleEyebrow01.asset', (0.20, 0.15, 0.11), 1.0),
                     ('Races/HumanMale/Overlays/Face/M_Eyelash.asset', (0.10, 0.08, 0.07), 1.0)],
        eye_albedo='Races/HumanShared/Textures/Face/Eye/EyeAdjust/Default Eye Adjust_diffuse.png',
    ),
    'F': dict(
        high=['Races/HumanFemale/Slots/Female_High_%s_Slot.asset' % n
              for n in ('Face', 'Torso', 'Hands', 'Legs', 'Feet', 'Eyes')],
        low=['Races/HumanFemale/Slots/UMA_Human_Female_%s_Slot.asset' % n
             for n in ('Face', 'Torso', 'Hands', 'Legs', 'Feet', 'Eyes')],
        lashes=('Races/HumanFemale/Slots/FemaleEyelashfix_Slot.asset', 'Races/HumanFemale/Overlays/Hair/FemaleEyelash.asset'),
        body_albedo='Races/HumanFemale/Textures/Body/F_H_bod_Albedo2.png',
        body_normal='Races/HumanFemale/Textures/Body/F_bod_N.png',
        face_normal='Races/HumanFemale/Textures/Face/F_H_face_N.png',
        faces=[('Adulte', 'Races/HumanFemale/Textures/AlternateFaces/UMA2Female_Adult.png'),
               ('Adulte2', 'Races/HumanFemale/Textures/AlternateFaces/UMA2Female_Adult2.png'),
               ('Jeune', 'Races/HumanFemale/Textures/AlternateFaces/UMA2Female_Young2.png'),
               ('Taches', 'Races/HumanFemale/Textures/AlternateFaces/UMA2Female_Freckles1.png'),
               ('Mure', 'Races/HumanFemale/Textures/AlternateFaces/UMA2Female_Older1.png'),
               ('Balafre', 'Races/HumanFemale/Textures/AlternateFaces/UMA2Female_Scarred2.png')],
        face_layers=[('Wearables/Example/HumanFemale/Overlays/Hair/FemaleEyebrow01.asset', (0.22, 0.16, 0.12), 0.9)],
        eye_albedo='Races/HumanShared/Textures/Face/Eye/EyeAdjust/Default Eye Adjust_diffuse.png',
    ),
}

# Vêtements. kind : 'slot' (maillage UMA), 'overlay' (décalque : les faces de peau couvertes
# par la texture de l'overlay), 'cut' (décalque : faces choisies par la géométrie).
#   hides : rôle du corps entièrement caché (UMA « Hides ») ; cover : peau cachée par contact.
#   texture : (albedo, relief) teintables ; fabric : tissu procédural d'Unity (UV en mètres).
GARMENTS = {
    'H': {
        'TShirt': dict(kind='cut', on='torso', cut='tshirt', fabric='jersey', bit=TSHIRT),
        'Veste': dict(kind='slot', slot='Wearables/MaleHoodie/MaleHoodie_Slot.asset',
                      overlay='Wearables/MaleHoodie/MaleHoodie_Overlay.asset', cover='torso', bit=VESTE, size=2048),
        'Debardeur': dict(kind='overlay', on='torso', overlay='Wearables/Example/HumanMale/Overlays/Clothing/MaleShirt01.asset',
                          bit=DEBARDEUR, size=1024),
        'Jean': dict(kind='slot', slot='Wearables/Example/HumanMale/Slots/Clothing/MaleJeans02.asset',
                     overlay='Wearables/Example/HumanMale/Overlays/Clothing/MaleJeans01.asset', hides='legs', bit=JEAN, size=1024),
        'Chaussures': dict(kind='slot', slot='Wearables/Male_TallShoes/TallShoes_Slot.asset',
                           overlay='Wearables/Male_TallShoes/TallShoes_White_Overlay.asset', hides='feet', bit=CHAUSSURES,
                           size=1024, keep_colors=True),
    },
    'F': {
        'TShirt': dict(kind='slot', slot='Wearables/Example/HumanFemale/Slots/Clothing/FemaleTshirt01.asset',
                       overlay='Wearables/Example/HumanFemale/Overlays/Clothing/FemaleTshirt01.asset', cover='torso',
                       bit=TSHIRT, size=1024),
        'Veste': dict(kind='cut', on='torso', cut='manches_longues', fabric='jersey', bit=VESTE),
        'Debardeur': dict(kind='slot', slot='Wearables/FemaleTankTop/FemaleTankTop_Slot.asset',
                          overlay='Wearables/FemaleTankTop/FemaleTankTop_Overlay.asset', cover='torso', bit=DEBARDEUR, size=1024),
        'Jean': dict(kind='overlay', on='legs', overlay='Wearables/Example/HumanFemale/Overlays/Clothing/FemaleJeans01.asset',
                     bit=JEAN, size=1024),
        'Chaussures': dict(kind='slot', slot='Wearables/F_Shoes/FemaleTallShoes_Slot.asset',
                           overlay='Wearables/Male_TallShoes/TallShoes_White_Overlay.asset', hides='feet', bit=CHAUSSURES,
                           size=1024, keep_colors=True, texture_from_male=True),
    },
}

# Morphologies : os UMA « Adjust » (et muscles) -> (le long, en largeur, en profondeur).
# Les côtés Left/Right sont ajoutés tout seuls. Pour un membre, largeur = profondeur = épaisseur.
def _limbs(arm, forearm, shoulder, trap, neck, thigh, calf):
    return {'ArmAdjust': (1.0, arm, arm), 'ForeArmAdjust': (1.0, forearm, forearm),
            'ForeArmTwistAdjust': (1.0, forearm * 0.97, forearm * 0.97), 'ShoulderAdjust': (1.0, shoulder, shoulder),
            'Trapezius': (1.0, trap, trap), 'NeckAdjust': (1.0, neck, neck),
            'UpLegAdjust': (1.0, thigh, thigh), 'LegAdjust': (1.0, calf, calf)}


def _torso(chest_w, chest_d, waist_w, waist_d, belly_w, belly_d, hips=1.0):
    return {'Spine1Adjust': (1.0, chest_w, chest_d), 'SpineAdjust': (1.0, waist_w, waist_d),
            'LowerBackAdjust': (1.0, waist_w, waist_d), 'LowerBackBelly': (1.0, belly_w, belly_d),
            'Gluteus': (1.0, hips, hips)}


SILHOUETTES = {
    # Le joueur : un boxeur sec et dense. Ses yeux tombent sur la caméra.
    'Athlete': dict(sexe='H', eyes=1.62, teint=(1.0, 0.94, 0.88), visage='Adulte',
                    forme={**_limbs(1.14, 1.10, 1.12, 1.18, 1.10, 1.06, 1.05), **_torso(1.07, 1.05, 0.95, 0.93, 0.92, 0.86)}),
    'Costaud': dict(sexe='H', eyes=1.66, teint=(0.98, 0.89, 0.81), visage='Variante1',
                    forme={**_limbs(1.18, 1.12, 1.14, 1.22, 1.18, 1.12, 1.08), **_torso(1.12, 1.12, 1.12, 1.14, 1.12, 1.25, 1.06)}),
    'Sec': dict(sexe='H', eyes=1.64, teint=(0.80, 0.65, 0.53), visage='Variante3',
                forme={**_limbs(0.98, 1.0, 0.98, 1.0, 0.98, 0.96, 0.97), **_torso(0.97, 0.96, 0.92, 0.92, 0.88, 0.84)}),
    'Colosse': dict(sexe='H', eyes=1.79, teint=(0.58, 0.42, 0.33), visage='Balafre',
                    forme={**_limbs(1.32, 1.22, 1.24, 1.38, 1.28, 1.18, 1.12), **_torso(1.16, 1.14, 1.08, 1.10, 1.02, 1.05, 1.06)}),
    'Femme': dict(sexe='F', eyes=1.53, teint=(1.0, 0.95, 0.91), visage='Adulte',
                  forme={**_limbs(0.98, 0.98, 1.0, 1.0, 1.0, 1.0, 1.0), **_torso(1.0, 1.0, 0.97, 0.97, 0.94, 0.92)}),
    'Sportive': dict(sexe='F', eyes=1.56, teint=(0.80, 0.64, 0.52), visage='Jeune',
                     forme={**_limbs(1.06, 1.05, 1.06, 1.06, 1.04, 1.04, 1.04), **_torso(1.02, 1.0, 0.95, 0.94, 0.9, 0.88)}),
}

# ---------------------------------------------------------------------- outils


def to_unity(p):
    """Repère des slots UMA (X vers le bas, Y vers la gauche du personnage, Z vers l'avant) -> Unity."""
    p = np.asarray(p, dtype=np.float64)
    return np.stack([p[..., 1], -p[..., 0], p[..., 2]], axis=-1)


def _sides(table):
    out = {}
    for k, v in table.items():
        if k in ('Spine1Adjust', 'SpineAdjust', 'LowerBackAdjust', 'LowerBackBelly', 'NeckAdjust', 'HeadAdjust'):
            out[k] = v
        else:
            out['Left' + k] = v
            out['Right' + k] = v
    return out


def shape(skeleton, forme):
    """Applique la morphologie : chaque os mis à l'échelle dans son propre repère."""
    lateral = np.array([0.0, 1.0, 0.0])        # gauche-droite dans le repère UMA
    for name, (along, wide, deep) in _sides(forme).items():
        h = skeleton.names.get(name)
        if h is None:
            continue
        rot = skeleton.world[h][:3, :3]
        rot = rot / np.maximum(np.linalg.norm(rot, axis=0, keepdims=True), 1e-9)
        # X local : le long de l'os. Des deux autres, celui qui suit la largeur du corps.
        y_lat = abs(np.dot(rot[:, 1], lateral))
        z_lat = abs(np.dot(rot[:, 2], lateral))
        if y_lat >= z_lat:
            skeleton.scales[name] = (along, wide, deep)
        else:
            skeleton.scales[name] = (along, deep, wide)
    skeleton.rebuild()


GAME_DIRECT = {
    'Global': 'Pelvis', 'Position': 'Pelvis', 'Hips': 'Pelvis',
    'LowerBack': 'Spine', 'LowerBackAdjust': 'Spine', 'LowerBackBelly': 'Spine',
    'Spine': 'Chest', 'SpineAdjust': 'Chest', 'Spine1': 'Chest', 'Spine1Adjust': 'Chest',
    'Neck': 'Neck', 'NeckAdjust': 'Neck', 'Head': 'Head', 'HeadAdjust': 'Head',
}
GAME_SIDE = {
    'Shoulder': 'Clavicle', 'ShoulderAdjust': 'Clavicle', 'Trapezius': 'Clavicle',
    'Arm': 'UpperArm', 'ArmAdjust': 'UpperArm',
    'ForeArm': 'Forearm', 'ForeArmAdjust': 'Forearm',
    'ForeArmTwist': 'ForearmTwist', 'ForeArmTwistAdjust': 'ForearmTwist',
    'Hand': 'Palm',
    'UpLeg': 'Thigh', 'UpLegAdjust': 'Thigh', 'Gluteus': 'Thigh',
    'Leg': 'Shin', 'LegAdjust': 'Shin', 'Foot': 'Ankle', 'ToeBase': 'Toe',
    'OuterBreast': 'Chest', 'InnerBreast': 'Chest',
}
FINGER = {'01': 'Pouce', '02': 'Index', '03': 'Majeur', '04': 'Annulaire', '05': 'Auriculaire'}


def game_bone(name, skeleton):
    n = name
    while n:
        if n in GAME_DIRECT:
            return GAME_DIRECT[n]
        for side in ('Left', 'Right'):
            if n.startswith(side):
                rest = n[len(side):]
                if rest in GAME_SIDE:
                    g = GAME_SIDE[rest]
                    return g if g == 'Chest' else side + g
                if rest.startswith('HandFinger') and len(rest) == len('HandFinger01_01'):
                    return side + FINGER[rest[10:12]] + str(int(rest[13:15]))
        n = skeleton.parent_name(n)
    return 'Pelvis'


# ---------------------------------------------------------------------- textures

def open_rgba(path, size=None):
    im = Image.open(path)
    im = im.convert('RGBA')
    if size and im.size != (size, size):
        im = im.resize((size, size), Image.LANCZOS)
    return im


def overlay_box(rect, size):
    """Rectangle UMA (pixels d'un atlas 2048, ou fraction s'il est <= 1 ; origine en bas) -> boîte PIL."""
    x, y, w, h = rect
    if w <= 0 or h <= 0:
        return 0, 0, size, size
    if w <= 1.0001 and h <= 1.0001:
        x, y, w, h = x * size, y * size, w * size, h * size
    else:
        k = size / 2048.0
        x, y, w, h = x * k, y * k, w * k, h * k
    w, h = max(1, int(round(w))), max(1, int(round(h)))
    return int(round(x)), int(round(size - (y + h))), w, h


def paint(base, layer_path, rect, color, strength=1.0):
    """Peint une couche UMA (son alpha) sur une texture, à sa place dans l'atlas."""
    size = base.size[0]
    x, top, w, h = overlay_box(rect, size)
    ov = Image.open(layer_path).convert('RGBA').resize((w, h), Image.LANCZOS)
    a = np.asarray(ov).astype(np.float32) / 255.0
    rgb = a[..., :3]
    if color is not None:
        # Les couches de poils sont en niveaux de gris : la couleur les teinte.
        lum = rgb.mean(axis=2, keepdims=True)
        rgb = lum * np.array(color, dtype=np.float32)[None, None, :] * 2.2
    alpha = a[..., 3:4] * strength
    dst = np.asarray(base.crop((x, top, x + w, top + h))).astype(np.float32) / 255.0
    out = dst * (1 - alpha) + np.clip(rgb, 0, 1) * alpha
    base.paste(Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8)), (x, top))
    return base


def tintable(path, size, keep_colors=False):
    """Albedo d'un vêtement, ramené à des gris clairs : la couleur du personnage le teinte."""
    im = Image.open(path).convert('RGB')
    if im.size != (size, size):
        im = im.resize((size, size), Image.LANCZOS)
    if keep_colors:
        return im
    a = np.asarray(im).astype(np.float32) / 255.0
    lum = a @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    med = np.median(lum[lum > 0.02]) if np.any(lum > 0.02) else 0.5
    lum = np.clip(lum * (0.82 / max(med, 1e-3)), 0, 1)
    return Image.fromarray((lum * 255).astype(np.uint8)).convert('RGB')


def save_jpg(im, name, quality=90):
    path = os.path.join(OUT_DIR, name)
    im.convert('RGB').save(path, quality=quality, optimize=True)
    return name


def save_png(im, name):
    path = os.path.join(OUT_DIR, name)
    im.save(path, optimize=True)
    return name


def write_textures(sexe, garments_used):
    """Textures partagées par toutes les silhouettes d'un même sexe. Renvoie les noms."""
    cfg = SEXES[sexe]
    names = {}
    body = Image.open(umalib.path(cfg['body_albedo'])).convert('RGB').resize((2048, 2048), Image.LANCZOS)
    names['peau'] = save_jpg(body, 'Peau_%s_Albedo.jpg' % sexe, 90)
    nrm = Image.open(umalib.path(cfg['body_normal'])).convert('RGB').resize((2048, 2048), Image.LANCZOS)
    names['peau_relief'] = save_jpg(nrm, 'Peau_%s_Relief.jpg' % sexe, 92)
    fn = Image.open(umalib.path(cfg['face_normal'])).convert('RGB').resize((2048, 2048), Image.LANCZOS)
    names['visage_relief'] = save_jpg(fn, 'Visage_%s_Relief.jpg' % sexe, 92)

    names['visages'] = []
    for key, rel in cfg['faces']:
        face = Image.open(umalib.path(rel)).convert('RGB').resize((2048, 2048), Image.LANCZOS)
        for layer, color, strength in cfg['face_layers']:
            ov = umalib.Overlay(layer)
            if ov.albedo:
                paint(face, ov.albedo, ov.rect, color, strength)
        names['visages'].append(save_jpg(face, 'Visage_%s_%s_Albedo.jpg' % (sexe, key), 88))

    eye = Image.open(umalib.path(cfg['eye_albedo'])).convert('RGB')
    names['yeux'] = save_jpg(eye, 'Yeux_UMA.jpg', 92)

    if cfg['lashes']:
        ov = umalib.Overlay(cfg['lashes'][1])
        im = Image.open(ov.albedo).convert('RGBA')
        a = np.asarray(im).astype(np.float32)
        a[..., :3] = a[..., :3].mean(axis=2, keepdims=True) * np.array([0.25, 0.2, 0.18])
        names['cils'] = save_png(Image.fromarray(a.astype(np.uint8), 'RGBA'), 'Cils_%s.png' % sexe)

    names['tenues'] = []
    for gname, g in GARMENTS[sexe].items():
        entry = {'nom': gname, 'albedo': '', 'relief': '', 'tissu': g.get('fabric', '') or ''}
        if g.get('overlay') and not g.get('fabric'):
            ov = umalib.Overlay(g['overlay'])
            if g.get('texture_from_male'):
                ov = umalib.Overlay(GARMENTS['H']['Chaussures']['overlay'])
            if ov.albedo:
                size = g.get('size', 1024)
                entry['albedo'] = save_jpg(tintable(ov.albedo, size, g.get('keep_colors', False)),
                                           'Tissu_%s_%s_Albedo.jpg' % (gname, sexe), 90)
            if ov.normal and os.path.exists(ov.normal):
                size = min(g.get('size', 1024), 1024)
                n = Image.open(ov.normal).convert('RGB').resize((size, size), Image.LANCZOS)
                entry['relief'] = save_jpg(n, 'Tissu_%s_%s_Relief.jpg' % (gname, sexe), 92)
        names['tenues'].append(entry)
    entry = {'nom': 'Bandes', 'albedo': '', 'relief': '', 'tissu': 'bandes'}
    names['tenues'].append(entry)
    return names


# ---------------------------------------------------------------------- assemblage

class Piece:
    """Un morceau prêt à exporter : sommets (repère Unity, mètres), faces, poids du jeu."""

    def __init__(self, positions, normals, uvs, tris, ids, weights, tags):
        self.positions = positions
        self.normals = normals
        self.uvs = uvs
        self.tris = tris
        self.ids = ids
        self.weights = weights
        self.tags = tags          # étiquette (sous-maillage) par face


class Body:
    def __init__(self, name, settings, crowd):
        self.name = name
        self.settings = settings
        self.sexe = settings['sexe']
        cfg = SEXES[self.sexe]
        files = cfg['low' if crowd else 'high']
        self.slots = {role: umalib.Slot(f) for role, f in zip(ROLES, files)}
        if cfg['lashes']:
            self.slots['lashes'] = umalib.Slot(cfg['lashes'][0])
        self.garments = {}
        for gname, g in GARMENTS[self.sexe].items():
            if g['kind'] == 'slot':
                self.garments[gname] = umalib.Slot(g['slot'])

        everything = list(self.slots.values()) + list(self.garments.values())
        self.skeleton = umalib.Skeleton(everything)
        shape(self.skeleton, settings['forme'])

        raw = {}
        for key, slot in list(self.slots.items()) + [('g:' + k, v) for k, v in self.garments.items()]:
            p, n = umalib.skin(slot, self.skeleton)
            raw[key] = (to_unity(p), to_unity(n))

        # Le sol : sous les semelles. L'échelle : les yeux à la hauteur voulue.
        floor = raw['g:Chaussures'][0][:, 1].min()
        eyes = raw['eyes'][0]
        self.eye_left = eyes[eyes[:, 0] < 0].mean(axis=0)
        self.eye_right = eyes[eyes[:, 0] >= 0].mean(axis=0)
        eye_y = (self.eye_left[1] + self.eye_right[1]) * 0.5
        self.scale = settings['eyes'] / (eye_y - floor)
        self.floor = floor

        self.pos = {k: self.place(v[0]) for k, v in raw.items()}
        self.nor = {k: v[1] for k, v in raw.items()}
        self.eye_left = self.place(self.eye_left[None])[0]
        self.eye_right = self.place(self.eye_right[None])[0]

        self.bones = rig.build(self.joint, settings['eyes'])
        self.bone_index = {b.name: i for i, b in enumerate(self.bones)}

    def place(self, p):
        q = np.array(p, dtype=np.float64)
        q[:, 1] -= self.floor
        return q * self.scale

    def bone_position(self, uma_name):
        p = self.skeleton.position(uma_name)
        return self.place(to_unity(p)[None])[0]

    def joint(self, mh, end):
        if mh == 'eye.L':
            return self.eye_left
        if mh == 'eye.R':
            return self.eye_right
        side = None
        base = mh
        if mh.endswith('.L') or mh.endswith('.R'):
            side = 'Left' if mh.endswith('.L') else 'Right'
            base = mh[:-2]
        if base.startswith('finger'):
            f, k = base[len('finger'):].split('-')
            bone = '%sHandFinger0%s_0%s' % (side, f, k)
            p = self.bone_position(bone)
            if end == 'tail':
                return self.finger_tip(side, f)
            return p
        names = {'root': 'Hips', 'spine05': 'LowerBack', 'spine03': 'Spine', 'neck01': 'Neck', 'head': 'Head',
                 'clavicle': 'Shoulder', 'upperarm01': 'Arm', 'lowerarm01': 'ForeArm', 'lowerarm02': 'ForeArmTwist',
                 'wrist': 'Hand', 'upperleg01': 'UpLeg', 'lowerleg01': 'Leg', 'foot': 'Foot', 'toe3-1': 'ToeBase'}
        uma = names[base]
        if side:
            uma = side + uma
        return self.bone_position(uma)

    def finger_tip(self, side, f):
        """Le bout du doigt : le sommet de la main le plus loin dans l'axe de la dernière phalange."""
        p2 = self.bone_position('%sHandFinger0%s_02' % (side, f))
        p3 = self.bone_position('%sHandFinger0%s_03' % (side, f))
        axis = p3 - p2
        axis /= np.linalg.norm(axis)
        hand = self.slots['hands']
        names = umalib.bone_names(hand, self.skeleton)
        target = '%sHandFinger0%s_03' % (side, f)
        pts = self.pos['hands']
        mask = np.array([any(names[bi] == target and w > 0.5 for bi, w in ws) for ws in hand.weights])
        if not mask.any():
            return p3 + axis * np.linalg.norm(p3 - p2) * 0.8
        proj = (pts[mask] - p3) @ axis
        return p3 + axis * max(proj.max() - 0.004, 0.01)

    def game_weights(self, slot):
        """Poids du slot fusionnés sur les os du jeu : 4 influences, normalisées."""
        names = umalib.bone_names(slot, self.skeleton)
        game = [self.bone_index[game_bone(n, self.skeleton)] for n in names]
        n = len(slot.weights)
        ids = np.zeros((n, 4), dtype=np.int32)
        wts = np.zeros((n, 4), dtype=np.float32)
        pelvis = self.bone_index['Pelvis']
        for v, pairs in enumerate(slot.weights):
            acc = {}
            for bi, w in pairs:
                g = game[bi]
                acc[g] = acc.get(g, 0.0) + w
            items = sorted(acc.items(), key=lambda kv: -kv[1])[:4]
            total = sum(w for _, w in items)
            if total <= 1e-8:
                ids[v, 0] = pelvis
                wts[v, 0] = 1.0
                continue
            for k, (g, w) in enumerate(items):
                ids[v, k] = g
                wts[v, k] = w / total
        return ids, wts


# ---------------------------------------------------------------------- vêtements

def face_centroids(p, tris):
    return p[tris].mean(axis=1)


def sample_alpha(path, uvs, tris, rect):
    """Alpha d'une couche UMA au centre UV de chaque face (atlas 2048 du corps)."""
    im = Image.open(path).convert('RGBA')
    x, top, w, h = overlay_box(rect, 2048)
    a = np.asarray(im)[..., 3].astype(np.float32) / 255.0
    H, W = a.shape
    uv = uvs[tris].mean(axis=1)
    px = uv[:, 0] * 2048.0
    py = (1.0 - uv[:, 1]) * 2048.0
    u = (px - x) / w
    v = (py - top) / h
    inside = (u >= 0) & (u < 1) & (v >= 0) & (v < 1)
    out = np.zeros(len(tris))
    out[inside] = a[np.clip((v[inside] * H).astype(int), 0, H - 1), np.clip((u[inside] * W).astype(int), 0, W - 1)]
    return out


def overlay_uv(uvs, rect):
    """UV de l'atlas du corps -> UV de la texture de l'overlay (qui n'en couvre qu'un rectangle)."""
    x, top, w, h = overlay_box(rect, 2048)
    px = uvs[:, 0] * 2048.0
    py = (1.0 - uvs[:, 1]) * 2048.0
    return np.stack([(px - x) / w, 1.0 - (py - top) / h], axis=1)


def meters_uv(positions, uvs, tris):
    """UV de l'atlas étirés en mètres (pour un tissu procédural répété à sa taille réelle)."""
    e3 = np.linalg.norm(positions[tris[:, 1]] - positions[tris[:, 0]], axis=1)
    e2 = np.linalg.norm(uvs[tris[:, 1]] - uvs[tris[:, 0]], axis=1)
    ok = e2 > 1e-6
    k = np.median(e3[ok] / e2[ok]) if ok.any() else 1.0
    return uvs * k


def cut_faces(body, cut, p, tris, ids, wts):
    """Faces de peau d'un vêtement taillé (tee-shirt d'homme, haut à manches longues)."""
    names = [b.name for b in body.bones]
    dominant = np.array([names[ids[v, 0]] for v in range(len(ids))])
    neck = body.bone_position('Neck')
    chest_z = body.bone_position('Spine1')[2]
    hip_y = body.bone_position('LeftUpLeg')[1]
    c = face_centroids(p, tris)
    dom_face = dominant[tris[:, 0]]

    # Encolure : plus basse devant que derrière.
    collar = np.where(c[:, 2] > chest_z, neck[1] - 0.045 * body.scale, neck[1] - 0.012 * body.scale)
    keep = (c[:, 1] < collar) & (c[:, 1] > hip_y + 0.01)

    sleeve = 0.52 if cut == 'tshirt' else 1.02
    for side in ('Left', 'Right'):
        shoulder = body.bone_position(side + 'Arm')
        elbow = body.bone_position(side + 'ForeArm')
        wrist = body.bone_position(side + 'Hand')
        on_arm = np.isin(dom_face, [side + 'UpperArm', side + 'Forearm', side + 'ForearmTwist'])
        if cut == 'tshirt':
            axis = elbow - shoulder
            t = (c - shoulder) @ axis / (axis @ axis)
            keep &= ~on_arm | (t < sleeve)
        else:
            axis = wrist - elbow
            t = (c - elbow) @ axis / (axis @ axis)
            fore = np.isin(dom_face, [side + 'Forearm', side + 'ForearmTwist'])
            keep &= ~fore | (t < 0.93)
    # Rien de la main ni du cou-tête.
    keep &= ~np.isin(dom_face, ['Neck', 'Head'])
    return keep


def wrap_faces(body, part, p, tris, ids):
    """Bandes de boxe : paume, dos de la main, jointures, poignet ; pas les doigts."""
    names = [b.name for b in body.bones]
    dominant = np.array([names[ids[v, 0]] for v in range(len(ids))])
    dom_face = dominant[tris[:, 0]]
    c = face_centroids(p, tris)
    keep = np.zeros(len(tris), dtype=bool)
    for side in ('Left', 'Right'):
        wrist = body.bone_position(side + 'Hand')
        elbow = body.bone_position(side + 'ForeArm')
        if part == 'hands':
            keep |= dom_face == side + 'Palm'
            # Les jointures : le début de la première phalange de chaque doigt (pas le pouce).
            for code, finger in (('02', 'Index'), ('03', 'Majeur'), ('04', 'Annulaire'), ('05', 'Auriculaire')):
                base = body.bone_position('%sHandFinger%s_01' % (side, code))
                keep |= (dom_face == side + finger + '1') & (np.linalg.norm(c - base, axis=1) < 0.02 * body.scale)
        else:
            axis = wrist - elbow
            t = (c - elbow) @ axis / (axis @ axis)
            keep |= np.isin(dom_face, [side + 'Forearm', side + 'ForearmTwist', side + 'Palm']) & (t > 0.78)
    return keep


def wrap_uv(body, p, n):
    """UV en mètres autour de l'axe avant-bras -> main : le tissu s'enroule comme une vraie bande."""
    uv = np.zeros((len(p), 2))
    for side in ('Left', 'Right'):
        elbow = body.bone_position(side + 'ForeArm')
        tip = body.bone_position(side + 'HandFinger03_01')
        axis = tip - elbow
        length = np.linalg.norm(axis)
        axis /= length
        sel = (p[:, 0] < 0) if side == 'Left' else (p[:, 0] >= 0)
        rel = p[sel] - elbow
        along = rel @ axis
        radial = rel - np.outer(along, axis)
        ref = np.cross(axis, [0.0, 1.0, 0.0])
        ref /= np.linalg.norm(ref)
        ref2 = np.cross(axis, ref)
        ang = np.arctan2(radial @ ref2, radial @ ref)
        r = np.linalg.norm(radial, axis=1)
        uv[sel, 0] = ang * np.maximum(r, 0.025)
        uv[sel, 1] = along
    return uv


def cover_mask(skin_p, garment_p, max_dist):
    """Sommets de peau sous un vêtement : le tissu passe tout près."""
    d, _ = cKDTree(garment_p).query(skin_p, k=1)
    return d < max_dist


def erode(tris, covered, rings=1):
    """Une face n'est cachée que si ses sommets ET leurs voisins sont couverts : pas de trou au bord."""
    cov = covered.copy()
    for _ in range(rings):
        bad = ~cov[tris].all(axis=1)
        nxt = cov.copy()
        nxt[np.unique(tris[bad])] = False
        cov = nxt
    return cov[tris].all(axis=1)


def build(name, settings, crowd):
    body = Body(name, settings, crowd)
    sexe = body.sexe
    pieces = []
    masks = {}

    # La peau, morceau par morceau.
    skin = {}
    for role in ('face', 'torso', 'hands', 'legs', 'feet'):
        slot = body.slots[role]
        ids, wts = body.game_weights(slot)
        skin[role] = dict(p=body.pos[role], n=body.nor[role], uv=slot.uv, tris=slot.tris, ids=ids, w=wts)
        masks[role] = np.zeros(len(slot.tris), dtype=np.int32)
    masks['face'] |= TETE

    def add_piece(p, n, uv, tris, ids, w, tag):
        pieces.append(Piece(p, n, uv, tris, ids, w, [tag] * len(tris)))

    # Les yeux, les cils.
    for role, tag in (('eyes', 'Yeux'), ('lashes', 'Cils')):
        if role in body.slots:
            slot = body.slots[role]
            ids, wts = body.game_weights(slot)
            add_piece(body.pos[role], body.nor[role], slot.uv, slot.tris, ids, wts, tag)

    # Les vêtements.
    for gname, g in GARMENTS[sexe].items():
        bit = g['bit']
        if g['kind'] == 'slot':
            slot = body.garments[gname]
            ids, wts = body.game_weights(slot)
            key = 'g:' + gname
            add_piece(body.pos[key], body.nor[key], slot.uv, slot.tris, ids, wts, gname)
            if g.get('hides'):
                masks[g['hides']] |= bit
            if g.get('cover'):
                s = skin[g['cover']]
                covered = cover_mask(s['p'], body.pos[key], 0.05 * body.scale)
                # Le cou reste : il sort du col, et le cacher ouvrirait un trou dans l'encolure.
                names = np.array([b.name for b in body.bones])
                covered &= ~np.isin(names[s['ids'][:, 0]], ['Neck', 'Head'])
                masks[g['cover']] |= np.where(erode(s['tris'], covered, 1), bit, 0)
            continue

        for part in (g['on'],):
            s = skin[part]
            if g['kind'] == 'overlay':
                ov = umalib.Overlay(g['overlay'])
                sel = sample_alpha(ov.albedo, s['uv'], s['tris'], ov.rect) > 0.5
                uv = overlay_uv(s['uv'], ov.rect)
            else:
                sel = cut_faces(body, g['cut'], s['p'], s['tris'], s['ids'], s['w'])
                uv = meters_uv(s['p'], s['uv'], s['tris'])
            if not sel.any():
                continue
            masks[part] |= np.where(sel, bit, 0)
            sub = s['tris'][sel]
            used = np.unique(sub)
            remap = -np.ones(len(s['p']), dtype=np.int64)
            remap[used] = np.arange(len(used))
            offset = 0.0018 if gname != 'Jean' else 0.0025
            add_piece(s['p'][used] + s['n'][used] * offset, s['n'][used], uv[used], remap[sub],
                      s['ids'][used], s['w'][used], gname)

    # Les bandes de boxe (le joueur) : décalques des mains et des poignets.
    for part in ('hands', 'torso'):
        s = skin[part]
        sel = wrap_faces(body, part, s['p'], s['tris'], s['ids'])
        if not sel.any():
            continue
        masks[part] |= np.where(sel, BANDES, 0)
        sub = s['tris'][sel]
        used = np.unique(sub)
        remap = -np.ones(len(s['p']), dtype=np.int64)
        remap[used] = np.arange(len(used))
        p = s['p'][used] + s['n'][used] * 0.0022
        add_piece(p, s['n'][used], wrap_uv(body, p, s['n'][used]), remap[sub], s['ids'][used], s['w'][used], 'Bandes')

    # La peau, avec ses masques.
    for role in ('face', 'torso', 'hands', 'legs', 'feet'):
        s = skin[role]
        prefix = 'Visage.' if role == 'face' else 'Peau.'
        pieces.append(Piece(s['p'], s['n'], s['uv'], s['tris'], s['ids'], s['w'],
                            [prefix + str(int(m)) for m in masks[role]]))
    return body, pieces


def merge(pieces):
    positions, normals, uvs, ids, wts = [], [], [], [], []
    subs = {}
    offset = 0
    for pc in pieces:
        positions.append(pc.positions)
        normals.append(pc.normals)
        uvs.append(pc.uvs)
        ids.append(pc.ids)
        wts.append(pc.weights)
        for t, tag in zip(pc.tris, pc.tags):
            subs.setdefault(tag, []).extend(int(x) + offset for x in t)
        offset += len(pc.positions)
    return (np.concatenate(positions), np.concatenate(normals), np.concatenate(uvs),
            np.concatenate(ids), np.concatenate(wts), subs)


# ---------------------------------------------------------------------- export

def matrix_to_quaternion(m):
    m00, m01, m02 = m[0]
    m10, m11, m12 = m[1]
    m20, m21, m22 = m[2]
    trace = m00 + m11 + m22
    if trace > 0:
        s = np.sqrt(trace + 1.0) * 2
        w, x, y, z = 0.25 * s, (m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s
    elif m00 > m11 and m00 > m22:
        s = np.sqrt(1.0 + m00 - m11 - m22) * 2
        w, x, y, z = (m21 - m12) / s, 0.25 * s, (m01 + m10) / s, (m02 + m20) / s
    elif m11 > m22:
        s = np.sqrt(1.0 + m11 - m00 - m22) * 2
        w, x, y, z = (m02 - m20) / s, (m01 + m10) / s, 0.25 * s, (m12 + m21) / s
    else:
        s = np.sqrt(1.0 + m22 - m00 - m11) * 2
        w, x, y, z = (m10 - m01) / s, (m02 + m20) / s, (m12 + m21) / s, 0.25 * s
    q = np.array([x, y, z, w])
    return q / np.linalg.norm(q)


def write_binary(path, bones, bone_index, positions, normals, uvs, ids, weights, submeshes):
    """Même format que Tools/corps/fabrique.py (UBCORPS2), voir CorpsImporter.Parse."""
    out = bytearray()
    out += MAGIC
    out += struct.pack('<i', len(bones))
    for b in bones:
        name = b.name.encode('utf-8')
        out += struct.pack('<i', len(name)) + name
        out += struct.pack('<i', bone_index[b.parent] if b.parent else -1)
        out += struct.pack('<3f', *b.position)
        out += struct.pack('<4f', *matrix_to_quaternion(b.rotation))
    n = len(positions)
    out += struct.pack('<i', n)
    out += np.asarray(positions, dtype='<f4').tobytes()
    out += np.asarray(normals, dtype='<f4').tobytes()
    out += np.asarray(uvs, dtype='<f4').tobytes()
    out += np.asarray(ids, dtype=np.uint8).tobytes()
    out += np.asarray(weights, dtype='<f4').tobytes()
    out += struct.pack('<i', len(submeshes))
    for name in sorted(submeshes):
        tris = submeshes[name]
        nb = name.encode('utf-8')
        out += struct.pack('<i', len(nb)) + nb
        out += struct.pack('<i', len(tris))
        out += np.asarray(tris, dtype='<i4').tobytes()
    with gzip.open(path, 'wb', compresslevel=9) as f:
        f.write(bytes(out))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--uma', required=True, help='UMA2_For_UMA3_f5.unitypackage')
    parser.add_argument('--cache', default=os.path.join(HERE, '.cache'))
    parser.add_argument('--seulement', default=None)
    parser.add_argument('--sans-textures', action='store_true')
    args = parser.parse_args()

    umalib.extract(args.uma, args.cache)
    os.makedirs(OUT_DIR, exist_ok=True)

    textures = {}
    for sexe in ('H', 'F'):
        if args.seulement and SILHOUETTES[args.seulement]['sexe'] != sexe:
            continue
        if args.sans_textures:
            continue
        textures[sexe] = write_textures(sexe, GARMENTS[sexe])

    for name, settings in SILHOUETTES.items():
        if args.seulement and name != args.seulement:
            continue
        for suffix, crowd in (('', False), ('_Foule', True)):
            body, pieces = build(name, settings, crowd)
            P, N, UV, ids, w, subs = merge(pieces)
            path = os.path.join(OUT_DIR, 'Corps_%s%s.bytes' % (name, suffix))
            write_binary(path, body.bones, body.bone_index, P, N, UV, ids, w, subs)
            print('%-9s%-6s %6d sommets, %6d triangles, %2d sous-maillages, %.1f Mo' % (
                name, suffix, len(P), sum(len(t) for t in subs.values()) // 3, len(subs), os.path.getsize(path) / 1e6))

        sexe = settings['sexe']
        tex = textures.get(sexe)
        if tex is None:
            continue
        faces = [os.path.splitext(f)[0] for f in tex['visages']]
        keys = [k for k, _ in SEXES[sexe]['faces']]
        meta = {
            'version': 1,
            'sexe': sexe,
            'teint': list(settings['teint']),
            'visage': faces[keys.index(settings['visage'])],
            'visages': faces,
            'peau': os.path.splitext(tex['peau'])[0],
            'peauRelief': os.path.splitext(tex['peau_relief'])[0],
            'visageRelief': os.path.splitext(tex['visage_relief'])[0],
            'yeux': os.path.splitext(tex['yeux'])[0],
            'cils': os.path.splitext(tex.get('cils', ''))[0],
            'tenues': [{k: (os.path.splitext(v)[0] if k in ('albedo', 'relief') else v) for k, v in t.items()}
                       for t in tex['tenues']],
        }
        with open(os.path.join(OUT_DIR, 'Corps_%s.json' % name), 'w', encoding='utf-8') as f:
            json.dump(meta, f, ensure_ascii=False, indent=1)


if __name__ == '__main__':
    main()
