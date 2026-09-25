"""
Fabrique des corps du jeu à partir de MakeHuman (CC0).

    python3 Tools/corps/fabrique.py [--makehuman CHEMIN] [--seulement Athlete] [--sans-peau]

Pour chaque silhouette (le joueur, les adversaires), le script :
1. applique les cibles de morphologie (homme, âge, muscle, poids, taille, origines) et des
   cibles locales de musculature (biceps, avant-bras, épaules, pectoraux, dorsaux, cou) ;
2. convertit en mètres et dans les axes d'Unity (X miroir : MakeHuman est en repère direct),
   à l'échelle qui place les yeux à la hauteur voulue (1,62 m pour le joueur = la caméra) ;
3. pose le squelette du jeu sur les articulations (voir rig.py), fusionne et lisse les poids ;
4. taille les vêtements dans le collant d'aide de MakeHuman (voir vetements.py) et marque la
   peau que chaque vêtement cache ;
5. subdivise une fois (Catmull-Clark, voir subdivision.py) : doigts et jointures arrondis ;
6. peint la peau (voir peau.py) ;
7. écrit Corps_<nom>.bytes (et Corps_<nom>_Foule.bytes, sans subdivision, pour le public),
   lus par l'éditeur Unity (Editor/CorpsImporter.cs).

Dépendances : numpy, scipy, pillow. Les données MakeHuman ne sont pas dans le dépôt :
cloner https://github.com/makehumancommunity/makehuman et passer --makehuman vers son dossier
makehuman/data (par défaut : /home/user/makehumancommunity/makehuman/makehuman/data).
"""
import argparse
import gzip
import os
import shutil
import struct
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import makehuman as mh  # noqa: E402
import rig  # noqa: E402
import subdivision  # noqa: E402
import vetements  # noqa: E402

EYE_HEIGHT = 1.62
OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "UberBagarre", "Art", "Models", "Corps")

# Silhouettes : réglages MakeHuman (0..1) et teinte de peau.
def _muscles(arms, forearms, shoulders, chest, back, vshape, abs_, neck):
    """Cibles locales : ce qui fait des bras de bagarreur et pas des bras de mannequin."""
    out = []
    for side in ("l", "r"):
        out += [("armslegs/%s-upperarm-muscle-incr.target" % side, arms),
                ("armslegs/%s-lowerarm-muscle-incr.target" % side, forearms),
                ("armslegs/%s-upperarm-shoulder-muscle-incr.target" % side, shoulders)]
    out += [("torso/torso-muscle-pectoral-incr.target", chest),
            ("torso/torso-muscle-dorsi-incr.target", back),
            ("torso/torso-vshape-incr.target", vshape),
            ("stomach/stomach-tone-incr.target", abs_),
            ("neck/neck-scale-horiz-incr.target", neck)]
    return out


# Silhouettes : réglages MakeHuman (0..1), cibles locales, teinte de peau et hauteur des yeux.
# Le joueur (Athlete) a les yeux exactement à la hauteur de la caméra ; les adversaires ont
# leur propre stature — le Colosse regarde le joueur de haut.
SILHOUETTES = {
    "Athlete": dict(muscle=0.92, weight=0.55, height=0.6, proportions=0.85,
                    african=0.15, asian=0.15, caucasian=0.7, tone=(0.80, 0.60, 0.48), eyes=1.62,
                    extra=_muscles(1.0, 1.0, 0.8, 0.7, 0.6, 0.45, 0.6, 0.35)),
    "Costaud": dict(muscle=0.8, weight=0.75, height=0.62, proportions=0.7,
                    african=0.1, asian=0.1, caucasian=0.8, tone=(0.76, 0.56, 0.45), eyes=1.66,
                    extra=_muscles(0.8, 0.9, 0.7, 0.6, 0.5, 0.2, 0.0, 0.6)),
    "Sec": dict(muscle=0.8, weight=0.38, height=0.55, proportions=0.8,
                african=0.35, asian=0.25, caucasian=0.4, tone=(0.62, 0.44, 0.33), eyes=1.64,
                extra=_muscles(0.7, 1.0, 0.5, 0.4, 0.5, 0.5, 0.8, 0.1)),
    "Colosse": dict(muscle=1.0, weight=0.82, height=0.85, proportions=0.9,
                    african=0.55, asian=0.05, caucasian=0.4, tone=(0.47, 0.32, 0.24), eyes=1.79,
                    extra=_muscles(1.0, 1.0, 1.0, 1.0, 1.0, 0.6, 0.3, 0.8)),
}


class Body:
    """Un corps prêt à exporter : sommets déjà dans l'espace Unity."""

    def __init__(self, data_dir, name, settings):
        self.name = name
        self.settings = settings
        self.base = mh.BaseMesh(os.path.join(data_dir, "3dobjs", "base.obj"))
        self.skeleton = mh.Skeleton(data_dir)

        morph = mh.Morphology(data_dir, gender=1.0, age=0.5,
                              muscle=settings["muscle"], weight=settings["weight"],
                              height=settings["height"], proportions=settings["proportions"],
                              african=settings["african"], asian=settings["asian"],
                              caucasian=settings["caucasian"])
        targets = morph.targets()
        for relative, weight in settings.get("extra", []):
            targets.append((os.path.join(data_dir, "targets", relative), weight))
        raw = mh.apply_targets(self.base.coords, targets)

        body_vertices = self.base.group_vertices("body")
        floor = raw[body_vertices, 1].min()
        eye_l = self.skeleton.head(raw, "eye.L")
        eye_r = self.skeleton.head(raw, "eye.R")
        eye_y = (eye_l[1] + eye_r[1]) * 0.5

        # Echelle choisie pour que les yeux soient a la hauteur voulue : celle de la camera
        # pour le joueur, la stature du personnage pour les autres.
        self.eye_height = settings.get("eyes", EYE_HEIGHT)
        self.scale = self.eye_height / (eye_y - floor)
        self.floor = floor
        self.coords = self.to_unity(raw)
        self.raw = raw

        def joint(bone, end):
            p = self.skeleton.head(raw, bone) if end == "head" else self.skeleton.tail(raw, bone)
            return self.to_unity(p[None, :])[0]

        self.bones = rig.build(joint, self.eye_height)
        self.bone_index = {b.name: i for i, b in enumerate(self.bones)}

        count = len(self.base.coords)
        self.bone_ids, self.bone_weights = rig.merge_weights(self.skeleton, count, self.bone_index)
        hand = [i for b, i in self.bone_index.items()
                if any(k in b for k in ("Palm", "Pouce", "Index", "Majeur", "Annulaire", "Auriculaire"))]
        self.bone_ids, self.bone_weights = rig.smooth_weights(
            self.bone_ids, self.bone_weights, [f[0] for f in self.base.faces],
            len(self.bones), hand, iterations=3, strength=0.5)

    def to_unity(self, points):
        p = np.array(points, dtype=np.float64)
        p[:, 1] -= self.floor
        p *= self.scale
        p[:, 0] = -p[:, 0]
        return p

    def height(self):
        v = self.base.group_vertices("body")
        return self.coords[v, 1].max()




# ---------------------------------------------------------------------- assemblage

class Assembly:
    """
    Tout le personnage dans une seule topologie : sommets indexés (une position = un sommet,
    même sur une couture de texture), faces dans l'ordre MakeHuman, UV par coin, étiquette
    par face (= sous-maillage), poids de peau denses (un canal par os).
    """

    def __init__(self, bone_count):
        self.bone_count = bone_count
        self.positions = []
        self.weights = []
        self.faces = []
        self.uvs = []
        self.tags = []
        self.count = 0

    def add_vertices(self, positions, ids, weights):
        n = len(positions)
        dense = np.zeros((n, self.bone_count))
        np.put_along_axis(dense, np.asarray(ids, dtype=np.int64), np.asarray(weights, dtype=np.float64), axis=1)
        self.positions.append(np.asarray(positions, dtype=np.float64))
        self.weights.append(dense)
        offset = self.count
        self.count += n
        return offset

    def add_face(self, corners, uvs, tag):
        self.faces.append(tuple(int(c) for c in corners))
        self.uvs.append([tuple(map(float, u)) for u in uvs])
        self.tags.append(tag)

    def arrays(self):
        return np.concatenate(self.positions), np.concatenate(self.weights)


def eye_proxy(data_dir):
    folder = os.path.join(data_dir, "eyes", "low-poly")
    refs = []
    with open(os.path.join(folder, "low-poly.mhclo"), encoding="utf-8") as f:
        started = False
        for line in f:
            s = line.strip()
            if s.startswith("verts"):
                started = True
                continue
            if started and s and s[0].isdigit():
                refs.append(int(s.split()[0]))
    return refs, mh.BaseMesh(os.path.join(folder, "low-poly.obj"))


def assemble(body, data_dir, pieces, masks):
    asm = Assembly(len(body.bones))

    body_faces = [(vi, ti) for vi, ti, g in body.base.faces if g == "body"]
    used = sorted({v for vi, _ in body_faces for v in vi})
    remap = {v: i for i, v in enumerate(used)}
    asm.add_vertices(body.coords[used], body.bone_ids[used], body.bone_weights[used])
    for (vi, ti), mask in zip(body_faces, masks):
        uvs = [body.base.uvs[t] if t >= 0 else (0.0, 0.0) for t in ti]
        asm.add_face([remap[v] for v in vi], uvs, "Peau.%d" % mask)

    refs, eyes = eye_proxy(data_dir)
    src = np.array(refs)
    off = asm.add_vertices(body.coords[src], body.bone_ids[src], body.bone_weights[src])
    for vi, ti, _ in eyes.faces:
        asm.add_face([off + v for v in vi], [eyes.uvs[t] for t in ti], "Yeux")

    for g in pieces.values():
        off = asm.add_vertices(g.positions, g.ids, g.weights)
        for f, uv, tag in zip(g.faces, g.uvs, g.tags):
            asm.add_face([off + v for v in f], uv, tag)
    return asm


def finish(asm, subdivide_levels, pieces):
    """Subdivision, re-décollage des vêtements, normales, poids à 4 influences."""
    positions, dense = asm.arrays()
    faces, uvs, tags = asm.faces, [np.array(u) for u in asm.uvs], list(asm.tags)

    for _ in range(subdivide_levels):
        positions, quads, quad_uv, (dense,), parent = subdivision.subdivide(positions, faces, uvs, [dense])
        faces = [tuple(q) for q in quads]
        uvs = [q for q in quad_uv]
        tags = [tags[p] for p in parent]

    garment_tags = set(TAG_MATERIAL) - {"Peau", "Yeux"}
    is_garment = np.zeros(len(positions), dtype=bool)
    is_skin = np.zeros(len(positions), dtype=bool)
    for f, t in zip(faces, tags):
        base = t.split(".")[0]
        if base in garment_tags:
            is_garment[list(f)] = True
        elif base == "Peau":
            is_skin[list(f)] = True

    normals = vetements.vertex_normals(positions, faces)

    # La subdivision rétracte peau et tissu un peu différemment : on redécolle le tissu.
    skin_idx = np.where(is_skin)[0]
    surface = vetements.Surface(positions[skin_idx], normals[skin_idx])
    g_idx = np.where(is_garment)[0]
    positions[g_idx], _ = surface.push_out(positions[g_idx], 0.0035)
    normals = vetements.vertex_normals(positions, faces)

    order = np.argsort(-dense, axis=1)[:, :4]
    w = np.take_along_axis(dense, order, axis=1)
    total = w.sum(axis=1, keepdims=True)
    total[total < 1e-9] = 1.0
    w = w / total
    return positions, normals, faces, uvs, tags, order.astype(np.int32), w.astype(np.float32)


# Étiquette -> famille de matière (le nom de sous-maillage garde le détail, ex. Peau.9).
TAG_MATERIAL = {"Peau": 0, "Yeux": 1, "TShirt": 2, "Veste": 3, "Debardeur": 4, "Jean": 5,
                "Ceinture": 6, "Chaussures": 7, "Semelle": 8}


def split_for_unity(positions, normals, faces, uvs, tags, ids, weights):
    """Un sommet Unity par couple (position, UV) ; quads coupés en deux triangles, ordre inversé."""
    key_index = {}
    out_v = []
    out_uv = []
    submeshes = {}
    for f, uv, tag in zip(faces, uvs, tags):
        corners = []
        for v, u in zip(f, uv):
            key = (int(v), round(float(u[0]), 6), round(float(u[1]), 6))
            i = key_index.get(key)
            if i is None:
                i = len(out_v)
                key_index[key] = i
                out_v.append(int(v))
                out_uv.append((float(u[0]), float(u[1])))
            corners.append(i)
        tris = submeshes.setdefault(tag, [])
        if len(corners) == 4:
            a, b, c, d = corners
            tris += [a, c, b, a, d, c]
        else:
            a = corners[0]
            for k in range(1, len(corners) - 1):
                tris += [a, corners[k + 1], corners[k]]
    src = np.array(out_v)
    return (positions[src], normals[src], np.array(out_uv), ids[src], weights[src], submeshes)


# ---------------------------------------------------------------------- export

MAGIC = b"UBCORPS2"


def write_binary(path, bones, bone_index, positions, normals, uvs, ids, weights, submeshes):
    """
    Fichier compressé gzip. Contenu (petit-boutiste) :
      8 octets "UBCORPS2"
      int32 nombre d'os ; par os : nom (int32 + UTF-8), parent (int32, -1 = racine),
            position de liaison (3 float32, monde), rotation (quaternion x y z w)
      int32 nombre de sommets n ; puis, en blocs :
            positions n*3 float32, normales n*3 float32, uv n*2 float32,
            os n*4 uint8, poids n*4 float32
      int32 nombre de sous-maillages ; par sous-maillage : nom, int32 nombre d'indices, indices int32
    Les positions sont celles de la pose de liaison : ce sont aussi les « positions de repos »
    qui servent aux bleus (voir BruiseSystem).
    """
    out = bytearray()
    out += MAGIC
    out += struct.pack("<i", len(bones))
    for b in bones:
        name = b.name.encode("utf-8")
        out += struct.pack("<i", len(name)) + name
        out += struct.pack("<i", bone_index[b.parent] if b.parent else -1)
        out += struct.pack("<3f", *b.position)
        out += struct.pack("<4f", *matrix_to_quaternion(b.rotation))
    n = len(positions)
    out += struct.pack("<i", n)
    out += np.asarray(positions, dtype="<f4").tobytes()
    out += np.asarray(normals, dtype="<f4").tobytes()
    out += np.asarray(uvs, dtype="<f4").tobytes()
    out += np.asarray(ids, dtype=np.uint8).tobytes()
    out += np.asarray(weights, dtype="<f4").tobytes()
    out += struct.pack("<i", len(submeshes))
    for name in sorted(submeshes):
        tris = submeshes[name]
        nb = name.encode("utf-8")
        out += struct.pack("<i", len(nb)) + nb
        out += struct.pack("<i", len(tris))
        out += np.asarray(tris, dtype="<i4").tobytes()
    with gzip.open(path, "wb", compresslevel=9) as f:
        f.write(bytes(out))


def matrix_to_quaternion(m):
    """Matrice de rotation (colonnes = axes) -> quaternion (x, y, z, w), convention Unity."""
    m00, m01, m02 = m[0]
    m10, m11, m12 = m[1]
    m20, m21, m22 = m[2]
    trace = m00 + m11 + m22
    if trace > 0:
        s = np.sqrt(trace + 1.0) * 2
        w = 0.25 * s
        x = (m21 - m12) / s
        y = (m02 - m20) / s
        z = (m10 - m01) / s
    elif m00 > m11 and m00 > m22:
        s = np.sqrt(1.0 + m00 - m11 - m22) * 2
        w = (m21 - m12) / s
        x = 0.25 * s
        y = (m01 + m10) / s
        z = (m02 + m20) / s
    elif m11 > m22:
        s = np.sqrt(1.0 + m11 - m00 - m22) * 2
        w = (m02 - m20) / s
        x = (m01 + m10) / s
        y = 0.25 * s
        z = (m12 + m21) / s
    else:
        s = np.sqrt(1.0 + m22 - m00 - m11) * 2
        w = (m10 - m01) / s
        x = (m02 + m20) / s
        y = (m12 + m21) / s
        z = 0.25 * s
    q = np.array([x, y, z, w])
    return q / np.linalg.norm(q)


def build(data_dir, name, settings, levels):
    body = Body(data_dir, name, settings)
    anatomy = vetements.Anatomy(body)
    skin_vertices = body.base.group_vertices("body")
    body_faces = [f[0] for f in body.base.faces if f[2] == "body"]
    normals = vetements.vertex_normals(body.coords, body_faces)
    surface = vetements.Surface(body.coords[skin_vertices], normals[skin_vertices])
    pieces = vetements.dress(body, anatomy, surface)
    masks = vetements.skin_masks(body, anatomy)
    asm = assemble(body, data_dir, pieces, masks)
    result = finish(asm, levels, pieces)
    return body, pieces, result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--makehuman", default="/home/user/makehumancommunity/makehuman/makehuman/data")
    parser.add_argument("--seulement", default=None)
    parser.add_argument("--sans-peau", action="store_true", help="ne pas repeindre les textures de peau")
    args = parser.parse_args()

    os.makedirs(OUT_DIR, exist_ok=True)

    for name, settings in SILHOUETTES.items():
        if args.seulement and name != args.seulement:
            continue

        for suffix, levels in (("", 1), ("_Foule", 0)):
            body, pieces, (P, N, F, UV, T, ids, w) = build(args.makehuman, name, settings, levels)
            P2, N2, UV2, ids2, w2, subs = split_for_unity(P, N, F, UV, T, ids, w)
            out = os.path.join(OUT_DIR, "Corps_%s%s.bytes" % (name, suffix))
            write_binary(out, body.bones, body.bone_index, P2, N2, UV2, ids2, w2, subs)
            print("%-8s%-6s taille %.2f m, %6d sommets, %6d triangles, %d sous-maillages, %.1f Mo" % (
                name, suffix, body.height(), len(P2), sum(len(t) for t in subs.values()) // 3, len(subs),
                os.path.getsize(out) / 1e6))

        if not args.sans_peau:
            import peau
            peau.paint(body, os.path.join(OUT_DIR, "Peau_%s" % name), settings["tone"])

    eye_src = os.path.join(args.makehuman, "eyes", "materials", "brown_eye.png")
    if os.path.exists(eye_src):
        shutil.copyfile(eye_src, os.path.join(OUT_DIR, "Yeux_Marron.png"))


if __name__ == "__main__":
    main()
