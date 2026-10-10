"""
Planche de contrôle des corps exportés (Blender en module Python, Cycles).

    python3 Tools/uma/apercu.py sortie.png [Athlete:TShirt:0.1,0.1,0.12:0.1,0.13,0.25 ...] [--garde] [--zoom]

Relit Corps_<nom>.bytes / .json EXACTEMENT comme Editor/CorpsImporter.cs (même tri des
sous-maillages par tenue), pour qu'une erreur d'export se voie ici avant d'ouvrir Unity.
--garde : bras en garde, poings fermés (même solveur que le jeu, Tools/corps/pose.py).
"""
import gzip
import json
import math
import os
import struct
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'corps'))
CORPS = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', 'UberBagarre', 'Art', 'Models', 'Corps'))

TOPS = {'TShirt': 1, 'Veste': 2, 'Debardeur': 4}
JEAN, CHAUSSURES, TETE, BANDES = 8, 16, 32, 64


class Bone:
    def __init__(self, name, parent, position, rotation):
        self.name, self.parent, self.position, self.rotation = name, parent, position, rotation


def quat_mat(x, y, z, w):
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def load(name):
    data = gzip.open(os.path.join(CORPS, 'Corps_%s.bytes' % name)).read()
    assert data[:8] == b'UBCORPS2'
    o = 8

    def i32():
        nonlocal o
        v = struct.unpack_from('<i', data, o)[0]
        o += 4
        return v

    def string():
        nonlocal o
        n = i32()
        s = data[o:o + n].decode('utf-8')
        o += n
        return s

    count = i32()
    raw = []
    for _ in range(count):
        nm = string()
        parent = i32()
        pos = np.array(struct.unpack_from('<3f', data, o)); o += 12
        q = struct.unpack_from('<4f', data, o); o += 16
        raw.append((nm, parent, pos, quat_mat(*q)))
    bones = [Bone(nm, raw[p][0] if p >= 0 else None, pos, rot) for nm, p, pos, rot in raw]
    n = i32()
    P = np.frombuffer(data, '<f4', n * 3, o).reshape(n, 3).astype(np.float64); o += n * 12
    N = np.frombuffer(data, '<f4', n * 3, o).reshape(n, 3).astype(np.float64); o += n * 12
    UV = np.frombuffer(data, '<f4', n * 2, o).reshape(n, 2).astype(np.float64); o += n * 8
    ids = np.frombuffer(data, np.uint8, n * 4, o).reshape(n, 4).astype(np.int64); o += n * 4
    w = np.frombuffer(data, '<f4', n * 4, o).reshape(n, 4).astype(np.float64); o += n * 16
    subs = {}
    for _ in range(i32()):
        nm = string()
        c = i32()
        subs[nm] = np.frombuffer(data, '<i4', c, o).reshape(-1, 3).astype(np.int64); o += c * 4
    meta = json.load(open(os.path.join(CORPS, 'Corps_%s.json' % name.replace('_Foule', '')), encoding='utf-8'))
    return bones, P, N, UV, ids, w, subs, meta


def outfit(subs, top, with_head=True, wraps=False):
    """Même règle que CorpsImporter.SlotOf : la peau visible et les vêtements portés."""
    worn = TOPS[top] | JEAN | CHAUSSURES | (0 if with_head else TETE) | (BANDES if wraps else 0)
    out = {}
    for name, tris in subs.items():
        slot = None
        if name.startswith('Peau.') or name.startswith('Visage.'):
            mask = int(name.split('.')[1])
            if mask & worn == 0:
                slot = name.split('.')[0]
        elif name in ('Yeux', 'Cils'):
            slot = name if with_head else None
        elif name == top:
            slot = 'Haut'
        elif name in ('Jean', 'Chaussures'):
            slot = name
        elif name == 'Bandes' and wraps:
            slot = name
        if slot:
            out.setdefault(slot, []).append(tris)
    return {k: np.concatenate(v) for k, v in out.items()}


def main():
    import rendu
    import bpy
    import pose as ps

    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    guard = '--garde' in sys.argv
    zoom = '--zoom' in sys.argv
    out = args[0]
    cast = args[1:] or ['Athlete:TShirt:0.08,0.08,0.09:0.10,0.14,0.26', 'Costaud:Veste:0.35,0.06,0.05:0.12,0.12,0.14',
                        'Sec:Debardeur:0.85,0.85,0.82:0.20,0.22,0.30', 'Colosse:Veste:0.06,0.07,0.06:0.08,0.08,0.09',
                        'Femme:TShirt:0.75,0.30,0.35:0.15,0.20,0.35', 'Sportive:Debardeur:0.15,0.45,0.45:0.08,0.08,0.10']
    sc = rendu.reset()
    sc.cycles.samples = 64
    tex = lambda n: os.path.join(CORPS, n + ('.png' if os.path.exists(os.path.join(CORPS, n + '.png')) else '.jpg'))
    spacing = 0.85
    for k, spec in enumerate(cast):
        name, top, shirt, pants = spec.split(':')
        shirt = tuple(float(v) for v in shirt.split(','))
        pants = tuple(float(v) for v in pants.split(','))
        bones, P, N, UV, ids, w, subs, meta = load(name)
        teint = tuple(meta['teint'])
        slots = outfit(subs, top, True, name == 'Athlete')

        Q = P
        if guard:
            pz = ps.Pose(bones)
            eye = [b for b in bones if b.name == 'Yeux'][0].position
            for side in ('Right', 'Left'):
                s = 1 if side == 'Right' else -1
                pz.solve_arm(side, eye + np.array([s * 0.13, -0.19, 0.24]), ps.euler_unity(-50, -16 * s, -76 * s),
                             np.array([s * 0.25, -1, -0.35]))
                for finger, (a, b, c) in {'Index': (70, 98, 62), 'Majeur': (70, 100, 64), 'Annulaire': (70, 100, 64),
                                          'Auriculaire': (68, 98, 60), 'Pouce': (0, 16, 64)}.items():
                    for j, ang in zip((1, 2, 3), (a, b, c)):
                        pz.set_local_extra('%s%s%d' % (side, finger, j), ps.axis_angle([1, 0, 0], ang))
                pz.refresh_children(side + 'Wrist')
            Q = pz.skin(P, ids, w)
        Q = Q + np.array([(k - (len(cast) - 1) / 2.0) * spacing, 0, 0])

        mats = {}

        def skin_mat(key, image, normal):
            m = rendu.material(key, (1, 1, 1), 0.5, 0.2, image=image, normal=normal)
            nt = m.node_tree
            t = [n for n in nt.nodes if n.type == 'TEX_IMAGE'][0]
            mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'
            mix.inputs['Factor'].default_value = 1.0
            mix.inputs[7].default_value = (*teint, 1)
            nt.links.new(t.outputs['Color'], mix.inputs[6])
            nt.links.new(mix.outputs[2], nt.nodes['Principled BSDF'].inputs['Base Color'])
            return m

        def cloth(key, entry, color):
            if entry and entry.get('albedo'):
                m = rendu.material(key, (1, 1, 1), 0.8, image=tex(entry['albedo']),
                                   normal=tex(entry['relief']) if entry.get('relief') else None)
                nt = m.node_tree
                t = [n for n in nt.nodes if n.type == 'TEX_IMAGE'][0]
                mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'
                mix.inputs['Factor'].default_value = 1.0
                mix.inputs[7].default_value = (*color, 1)
                nt.links.new(t.outputs['Color'], mix.inputs[6])
                nt.links.new(mix.outputs[2], nt.nodes['Principled BSDF'].inputs['Base Color'])
                return m
            return rendu.material(key, color, 0.85)

        tenues = {t['nom']: t for t in meta['tenues']}
        mats['Peau'] = skin_mat('peau%d' % k, tex(meta['peau']), tex(meta['peauRelief']))
        mats['Visage'] = skin_mat('visage%d' % k, tex(meta['visage']), tex(meta['visageRelief']))
        mats['Yeux'] = rendu.material('yeux%d' % k, (1, 1, 1), 0.08, image=tex(meta['yeux']))
        if meta.get('cils'):
            m = rendu.material('cils%d' % k, (1, 1, 1), 0.6, image=tex(meta['cils']))
            nt = m.node_tree
            t = [n for n in nt.nodes if n.type == 'TEX_IMAGE'][0]
            nt.links.new(t.outputs['Alpha'], nt.nodes['Principled BSDF'].inputs['Alpha'])
            mats['Cils'] = m
        mats['Haut'] = cloth('haut%d' % k, tenues.get(top), shirt)
        mats['Jean'] = cloth('jean%d' % k, tenues.get('Jean'), pants)
        mats['Chaussures'] = cloth('ch%d' % k, tenues.get('Chaussures'), (0.9, 0.9, 0.9))
        mats['Bandes'] = rendu.material('bandes%d' % k, (0.85, 0.83, 0.78), 0.9)
        mats['*'] = rendu.material('x%d' % k, (1, 0, 1), 0.5)
        rendu.add_mesh('c%d' % k, Q, UV, {s: t.tolist() for s, t in slots.items()}, mats)

    me = bpy.data.meshes.new('sol')
    me.from_pydata([(-8, -8, 0), (8, -8, 0), (8, 8, 0), (-8, 8, 0)], [], [(0, 1, 2, 3)])
    ob = bpy.data.objects.new('sol', me)
    bpy.context.scene.collection.objects.link(ob)
    ob.data.materials.append(rendu.material('sol', (0.05, 0.05, 0.055), 0.6))
    rendu.light('AREA', (1.5, 3.2, 3.5), 700, size=2.5, look_u=(0, 1, 0))
    rendu.light('AREA', (-3, 2.0, 1.0), 260, color=(0.55, 0.65, 1), size=2, look_u=(0, 1.2, 0))
    rendu.light('AREA', (2.5, 2.2, -2.5), 380, color=(1, 0.6, 0.35), size=2, look_u=(0, 1.3, 0))
    sc.world.node_tree.nodes['Background'].inputs[0].default_value = (0.18, 0.19, 0.22, 1)
    width = len(cast) * spacing
    if zoom:
        rendu.camera((0.15, 1.62, 0.75), (0, 1.56, 0), 32)
        rendu.render(out, 900, 900)
    else:
        rendu.camera((0, 1.0, 1.5 + width * 1.05), (0, 0.92, 0), 36)
        rendu.render(out, 1800, 1000)


if __name__ == '__main__':
    main()
