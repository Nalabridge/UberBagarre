"""
Vêtements taillés dans le « collant » d'aide de MakeHuman.

Le maillage hm08 contient, en plus du corps, une combinaison moulante (helper-tights) du
cou aux chevilles, poignets compris, qui suit déjà toutes les cibles de morphologie : c'est
la base sur laquelle MakeHuman ajuste ses vêtements. On y découpe :

- un TEE-SHIRT (manches à mi-biceps : les avant-bras du joueur restent nus),
- une VESTE / un sweat (manches jusqu'au poignet, col plus haut, plus ample),
- un DÉBARDEUR (épaules nues, encolure échancrée),
- un JEAN (taille, jambes droites sous le genou, ceinture),
- des CHAUSSURES (semelle à plat, bout arrondi).

Chaque pièce est ensuite lissée (un tissu ne suit pas chaque abdominal), décollée de la peau
d'une épaisseur minimale, drapée (le tee-shirt tombe des pectoraux, le jean tombe droit du
genou) et ourlée : chaque bord ouvert reçoit une tranche d'épaisseur et un revers intérieur,
pour qu'une manche ou un col ne se lise pas comme une feuille de papier.

La peau cachée sous un vêtement est marquée (masque de bits) : l'import Unity ne garde que
la peau visible pour la tenue choisie — pas de peau qui traverse le tissu, et moins de
sommets à animer.
"""
import math

import numpy as np
from scipy.spatial import cKDTree

TSHIRT, VESTE, DEBARDEUR, JEAN, CHAUSSURES, TETE = 1, 2, 4, 8, 16, 32
TOPS = {"TShirt": TSHIRT, "Veste": VESTE, "Debardeur": DEBARDEUR}

ARM, TORSO, LEG, FOOT, HEAD = "bras", "torse", "jambe", "pied", "tete"

# Réglages de coupe (mètres, relatifs aux articulations du squelette).
TOP_CUT = {
    #            manche (m le long du bras), ourlet (/ bassin), col avant, col arrière, décollement, drapé
    "TShirt":    dict(sleeve=0.13, hem=-0.085, front=-0.050, back=-0.012, offset=0.007, drape=0.6, thick=0.0035),
    "Veste":     dict(sleeve=None, hem=-0.08, front=-0.012, back=0.018, offset=0.014, drape=0.95, thick=0.006),
    "Debardeur": dict(sleeve=-1.0, hem=-0.02, front=-0.105, back=-0.045, offset=0.004, drape=0.55, thick=0.0025),
}


def category(bone_name):
    if bone_name in ("Head",):
        return HEAD
    if any(k in bone_name for k in ("UpperArm", "Forearm", "Wrist", "Palm", "Pouce", "Index", "Majeur",
                                     "Annulaire", "Auriculaire", "Knuckles")):
        return ARM
    if any(k in bone_name for k in ("Hip", "Thigh", "Shin")):
        return LEG
    if any(k in bone_name for k in ("Ankle", "Toe")):
        return FOOT
    return TORSO


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


class Anatomy:
    """Repères tirés du squelette de liaison (repère Unity, mètres)."""

    def __init__(self, body):
        self.body = body
        J = {b.name: b.position for b in body.bones}
        self.J = J
        self.neck = J["Neck"]
        self.pelvis = J["Pelvis"]
        self.spine = J["Spine"]
        self.shoulder_y = (J["LeftUpperArm"][1] + J["RightUpperArm"][1]) * 0.5
        self.pec_y = self.shoulder_y - 0.115
        self.waist_y = self.pelvis[1] + 0.04
        self.ankle_y = (J["LeftAnkle"][1] + J["RightAnkle"][1]) * 0.5
        names = [b.name for b in body.bones]
        self.vertex_bone = [names[i] for i in body.bone_ids[:, 0]]
        self.vertex_cat = np.array([category(n) for n in self.vertex_bone])

    def side(self, p):
        return "Left" if p[0] < 0 else "Right"

    def arm_s(self, p):
        """Distance le long du bras depuis l'épaule (négative côté torse)."""
        s = self.side(p)
        a, b, c = self.J[s + "UpperArm"], self.J[s + "Forearm"], self.J[s + "Wrist"]
        l1 = np.linalg.norm(b - a)
        t = np.dot(p - a, b - a) / (l1 * l1)
        if t <= 1.0:
            return t * l1, l1, np.linalg.norm(c - b)
        l2 = np.linalg.norm(c - b)
        t2 = np.dot(p - b, c - b) / (l2 * l2)
        return l1 + t2 * l2, l1, l2

    def collar_y(self, p, front, back):
        rear = smoothstep(self.neck[2] + 0.05, self.neck[2] - 0.05, p[2])
        return self.neck[1] + front + (back - front) * rear

    def face_category(self, corners):
        cats = [self.vertex_cat[v] for v in corners]
        return max(set(cats), key=cats.count)

    # ------------------------------------------------------------------ zones

    def in_top(self, name, p, cat, m=0.0):
        cut = TOP_CUT[name]
        if cat == HEAD or cat == FOOT:
            return False
        if cat == LEG:
            return p[1] > self.pelvis[1] + cut["hem"] + m
        if p[1] < self.pelvis[1] + cut["hem"] + m:
            return False
        if cat == ARM:
            s, l1, l2 = self.arm_s(p)
            limit = cut["sleeve"] if cut["sleeve"] is not None else l1 + l2 * 0.955
            return s < limit - m
        # torse
        if name == "Debardeur":
            # Proportionné à la carrure : encolure échancrée entre les bretelles, bretelles
            # sur le haut des épaules près du cou, emmanchure sous le deltoïde seulement (le
            # deltoïde lui-même est déjà « bras », donc nu). Des bornes fixes découpaient les
            # flancs d'un gros gabarit jusqu'à la taille : un tablier, pas un débardeur.
            sh_x = abs(self.J[self.side(p) + "UpperArm"][0])
            if abs(p[0]) < sh_x * 0.40 + m and p[1] > self.collar_y(p, cut["front"], cut["back"]) - m:
                return False
            if abs(p[0]) > sh_x * 0.62 - m and p[1] > self.shoulder_y - 0.03 - m:
                return False
            if abs(p[0]) > sh_x - 0.035 - m and p[1] > self.shoulder_y - 0.16 - m:
                return False
            if p[1] > self.neck[1] - 0.004 - m:
                return False
            return True
        if p[1] > self.collar_y(p, cut["front"], cut["back"]) - m:
            return False
        return True

    def cut_levels(self, name):
        """Lignes de coupe d'une pièce : (type, valeur) pour aligner ses bords."""
        if name in TOP_CUT:
            c = TOP_CUT[name]
            return c
        return None

    def in_jean(self, p, cat, m=0.0):
        if cat not in (TORSO, LEG):
            return False
        if cat == TORSO and self.vertex_like_arm(p):
            return False
        return (p[1] < self.waist_y - m) and (p[1] > self.ankle_y - 0.012 + m)

    def vertex_like_arm(self, p):
        return abs(p[0]) > 0.24

    def in_shoes(self, p, cat, m=0.0):
        if cat not in (FOOT, LEG):
            return False
        return p[1] < self.ankle_y + 0.048 - m


# ---------------------------------------------------------------------- géométrie

def vertex_normals(positions, faces):
    """Normales sortantes (repère Unity) pour des faces dans l'ordre MakeHuman."""
    n = np.zeros_like(positions)
    for f in faces:
        a = positions[f[0]]
        for k in range(1, len(f) - 1):
            b, c = positions[f[k]], positions[f[k + 1]]
            fn = np.cross(c - a, b - a)      # ordre MakeHuman + repère miroir = inversé
            for v in (f[0], f[k], f[k + 1]):
                n[v] += fn
    length = np.linalg.norm(n, axis=1, keepdims=True)
    length[length < 1e-12] = 1.0
    return n / length


def adjacency(count, faces):
    nb = [set() for _ in range(count)]
    for f in faces:
        k = len(f)
        for i in range(k):
            a, b = f[i], f[(i + 1) % k]
            nb[a].add(b)
            nb[b].add(a)
    return nb


def boundary_edges(faces):
    """Arêtes de bord, orientées comme dans leur face."""
    count = {}
    for f in faces:
        k = len(f)
        for i in range(k):
            a, b = f[i], f[(i + 1) % k]
            key = (min(a, b), max(a, b))
            count[key] = count.get(key, 0) + 1
    out = []
    for f in faces:
        k = len(f)
        for i in range(k):
            a, b = f[i], f[(i + 1) % k]
            if count[(min(a, b), max(a, b))] == 1:
                out.append((a, b))
    return out


class Surface:
    """La peau, pour décoller les vêtements : sommets + normales + arbre de recherche."""

    def __init__(self, positions, normals):
        self.positions = positions
        self.normals = normals
        self.tree = cKDTree(positions)

    def push_out(self, points, offset, k=6, max_distance=0.05):
        """Écarte chaque point pour qu'il soit au moins à <offset> devant la surface."""
        d, idx = self.tree.query(points, k=k)
        w = 1.0 / np.maximum(d, 1e-5)
        w /= w.sum(axis=1, keepdims=True)
        q = np.einsum("nk,nkj->nj", w, self.positions[idx])
        nrm = np.einsum("nk,nkj->nj", w, self.normals[idx])
        nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-9)
        signed = np.einsum("nj,nj->n", points - q, nrm)
        off = np.broadcast_to(np.asarray(offset, dtype=np.float64), signed.shape)
        push = np.clip(off - signed, 0.0, None)
        # Seulement au contact : un point loin de cette surface (une manche au-dessus de la
        # ceinture du jean) n'a rien à en craindre, et la normale du voisin le plus proche
        # n'a alors plus de sens.
        push[d[:, 0] > max_distance] = 0.0
        return points + nrm * push[:, None], signed


class Garment:
    """Une pièce : sommets propres, faces (ordre MakeHuman), UV par coin, étiquettes, poids."""

    def __init__(self, name):
        self.name = name
        self.positions = None
        self.ids = None
        self.weights = None
        self.cats = None
        self.faces = []
        self.uvs = []
        self.tags = []


def laplacian(points, nb, fixed, iterations, strength=0.5, boundary_nb=None):
    p = points.copy()
    for _ in range(iterations):
        q = p.copy()
        for i, ns in enumerate(nb):
            if fixed is not None and fixed[i]:
                continue
            use = ns
            if boundary_nb is not None and boundary_nb[i]:
                use = boundary_nb[i]
            if not use:
                continue
            m = p[list(use)].mean(axis=0)
            q[i] = p[i] + (m - p[i]) * strength
        p = q
    return p


def cut(body, anatomy, name, test):
    """Faces du collant retenues par <test(p, cat)>, en indices MakeHuman."""
    out = []
    for vi, _, g in body.base.faces:
        if g != "helper-tights":
            continue
        p = body.coords[vi].mean(axis=0)
        cat = anatomy.face_category(vi)
        if test(p, cat):
            out.append(tuple(vi))
    return out


def make_garment(body, anatomy, skin, name, faces_mh, offset, smooth, thick, shape=None, under=None):
    g = Garment(name)
    verts = sorted({v for f in faces_mh for v in f})
    local = {v: i for i, v in enumerate(verts)}
    faces = [tuple(local[v] for v in f) for f in faces_mh]
    P = body.coords[verts].copy()
    g.ids = body.bone_ids[verts].copy()
    g.weights = body.bone_weights[verts].copy()
    g.cats = anatomy.vertex_cat[verts].copy()

    nb = adjacency(len(P), faces)
    bedges = boundary_edges(faces)
    bnb = [set() for _ in range(len(P))]
    for a, b in bedges:
        bnb[a].add(b)
        bnb[b].add(a)

    offset = np.broadcast_to(np.asarray(offset, dtype=np.float64), (len(P),)).copy()

    P = snap_edges(P, sorted({v for e in bedges for v in e}), g.cats, anatomy, name)

    for it in range(3):
        P = laplacian(P, nb, None, smooth, 0.5, bnb)
        if shape is not None:
            P = shape(P, g)
        P, _ = skin.push_out(P, offset)
        if under is not None:
            P, _ = under.push_out(P, offset * 0.0 + 0.004)

    g.positions = P
    g.faces = faces
    add_hems(g, bedges, thick)
    return g


def snap_edges(P, edge_vertices, cats, anatomy, name):
    """
    Les faces sont retenues entières : un bord suit donc l'escalier des quads du collant.
    On ramène chaque sommet de bord sur sa ligne de coupe (ourlet, bout de manche, col,
    taille, cheville) : un ourlet droit au lieu d'une dent de scie.
    """
    P = P.copy()
    J = anatomy.J
    lines = []
    if name in TOP_CUT:
        c = TOP_CUT[name]
        lines.append(("y", anatomy.pelvis[1] + c["hem"]))
        if name != "Debardeur":
            lines.append(("collar", c))
            if c["sleeve"] is not None:
                lines.append(("arm", c["sleeve"]))
            else:
                lines.append(("arm", None))
    elif name == "Jean":
        lines += [("y", anatomy.waist_y), ("y", anatomy.ankle_y - 0.012)]
    elif name == "Chaussures":
        lines += [("y", anatomy.ankle_y + 0.048)]

    for v in edge_vertices:
        p = P[v]
        best = None
        for kind, value in lines:
            if kind == "y":
                d = value - p[1]
                cand = p + np.array([0.0, d, 0.0])
            elif kind == "collar":
                d = anatomy.collar_y(p, value["front"], value["back"]) - p[1]
                if abs(p[0]) > 0.12:
                    continue
                cand = p + np.array([0.0, d, 0.0])
            else:
                if cats[v] != ARM:
                    continue
                s, l1, l2 = anatomy.arm_s(p)
                limit = value if value is not None else l1 + l2 * 0.955
                side = anatomy.side(p)
                if s < l1:
                    a, b = J[side + "UpperArm"], J[side + "Forearm"]
                else:
                    a, b = J[side + "Forearm"], J[side + "Wrist"]
                axis = (b - a) / np.linalg.norm(b - a)
                d = limit - s
                cand = p + axis * d
            if abs(d) < 0.035 and (best is None or abs(d) < best[0]):
                best = (abs(d), cand)
        if best is not None:
            P[v] = best[1]
    return P


def add_hems(g, bedges, thick):
    """Tranche d'épaisseur + revers intérieur sur chaque bord ouvert."""
    if not bedges:
        return
    P = g.positions
    normals = vertex_normals(P, g.faces)
    nb = adjacency(len(P), g.faces)
    edge_vertices = sorted({v for e in bedges for v in e})
    on_edge = set(edge_vertices)

    # Direction « vers l'intérieur du vêtement » : moyenne des voisins qui ne sont pas au bord.
    inward = {}
    for v in edge_vertices:
        inner = [u for u in nb[v] if u not in on_edge]
        d = (P[inner].mean(axis=0) - P[v]) if inner else -normals[v] * 0.01
        d = d - np.dot(d, normals[v]) * normals[v]
        n = np.linalg.norm(d)
        inward[v] = d / n if n > 1e-9 else np.zeros(3)

    base = len(P)
    lip = {}
    fold = {}
    new_p = []
    new_ids = []
    new_w = []
    new_c = []
    for k, v in enumerate(edge_vertices):
        lip[v] = base + 2 * k
        fold[v] = base + 2 * k + 1
        new_p.append(P[v] - normals[v] * thick)
        new_p.append(P[v] - normals[v] * thick + inward[v] * 0.022)
        for _ in range(2):
            new_ids.append(g.ids[v])
            new_w.append(g.weights[v])
            new_c.append(g.cats[v])

    g.positions = np.concatenate([P, np.array(new_p)])
    g.ids = np.concatenate([g.ids, np.array(new_ids)])
    g.weights = np.concatenate([g.weights, np.array(new_w)])
    g.cats = np.concatenate([g.cats, np.array(new_c)])
    for a, b in bedges:
        g.faces.append((b, a, lip[a], lip[b]))
        g.faces.append((lip[b], lip[a], fold[a], fold[b]))


# ---------------------------------------------------------------------- UV

def garment_uvs(g, anatomy):
    """UV en mètres : cylindres autour du tronc, des bras et des jambes."""
    J = anatomy.J
    P = g.positions
    uv = np.zeros((len(P), 2))
    part = np.zeros(len(P), dtype=np.int32)
    angle = np.zeros(len(P))
    zc = anatomy.spine[2]
    for i, p in enumerate(P):
        cat = g.cats[i]
        s = anatomy.side(p)
        if cat == ARM:
            a, c = J[s + "UpperArm"], J[s + "Wrist"]
            axis = (c - a) / np.linalg.norm(c - a)
            ref = np.array([0.0, 0.0, 1.0])
            ref = ref - np.dot(ref, axis) * axis
            ref /= np.linalg.norm(ref)
            ortho = np.cross(axis, ref)
            d = p - a
            ang = math.atan2(np.dot(d, ortho), np.dot(d, ref))
            uv[i] = (ang * 0.05, np.dot(d, axis))
            part[i] = 1 if s == "Left" else 2
            angle[i] = ang
        elif cat in (LEG, FOOT) and p[1] < anatomy.pelvis[1] - 0.06:
            a, c = J[s + "Thigh"], J[s + "Ankle"]
            axis = (c - a) / np.linalg.norm(c - a)
            ref = np.array([0.0, 0.0, 1.0])
            ref = ref - np.dot(ref, axis) * axis
            ref /= np.linalg.norm(ref)
            ortho = np.cross(axis, ref)
            d = p - a
            ang = math.atan2(np.dot(d, ortho), np.dot(d, ref))
            uv[i] = (ang * 0.08, -np.dot(d, axis))
            part[i] = 3 if s == "Left" else 4
            angle[i] = ang
        else:
            ang = math.atan2(p[0], p[2] - zc)
            uv[i] = (ang * 0.16, p[1])
            part[i] = 0
            angle[i] = ang

    radius = {0: 0.16, 1: 0.05, 2: 0.05, 3: 0.08, 4: 0.08}
    g.uvs = []
    for f in g.faces:
        corners = [uv[v].copy() for v in f]
        parts = {part[v] for v in f}
        if len(parts) == 1:
            angs = [angle[v] for v in f]
            if max(angs) - min(angs) > math.pi:
                r = radius[part[f[0]]]
                for k, v in enumerate(f):
                    if angle[v] < 0:
                        corners[k][0] += 2 * math.pi * r
        g.uvs.append(corners)


# ---------------------------------------------------------------------- façons

def drape_top(anatomy, strength):
    """Le tissu tombe des pectoraux et des omoplates au lieu de coller au ventre."""
    zc = anatomy.spine[2]
    top_y = anatomy.pec_y

    def shape(P, g):
        torso = (g.cats == TORSO) | (g.cats == LEG)
        x, y, z = P[:, 0], P[:, 1], P[:, 2] - zc
        ang = np.arctan2(x, z)
        r = np.hypot(x, z)
        bins = np.clip(((ang + math.pi) / (2 * math.pi) * 48).astype(int), 0, 47)
        band = torso & (np.abs(y - top_y) < 0.035)
        ref = np.zeros(48)
        for b in range(48):
            sel = band & (bins == b)
            if sel.any():
                ref[b] = r[sel].max()
        # Trous éventuels : on reprend le voisin.
        for b in range(48):
            if ref[b] == 0:
                ref[b] = max(ref[(b - 1) % 48], ref[(b + 1) % 48])
        below = torso & (y < top_y)
        front = smoothstep(-0.02, 0.06, z)
        w = strength * (0.35 + 0.65 * front) * smoothstep(top_y + 0.01, top_y - 0.10, y)
        target = np.maximum(r, ref[bins] * 0.965)
        new_r = np.where(below, r + (target - r) * w, r)

        # Sous la taille, le tissu tombe droit : il ne suit ni les fessiers ni leur pli.
        waist = anatomy.waist_y + 0.02
        band = torso & (np.abs(y - waist) < 0.03)
        ref2 = np.zeros(48)
        for b in range(48):
            sel = band & (bins == b)
            if sel.any():
                ref2[b] = new_r[sel].max()
        for b in range(48):
            if ref2[b] == 0:
                ref2[b] = max(ref2[(b - 1) % 48], ref2[(b + 1) % 48])
        hang = torso & (y < waist) & (ref2[bins] > 0)
        new_r = np.where(hang, np.maximum(new_r, ref2[bins] * 0.99), new_r)

        scale = np.where(r > 1e-6, new_r / np.maximum(r, 1e-6), 1.0)
        out = P.copy()
        out[:, 0] = x * scale
        out[:, 2] = z * scale + zc
        return out

    return shape


def straight_legs(anatomy, loose=0.012):
    """Jambe droite : sous le genou, le jean garde la largeur du genou jusqu'à l'ourlet."""
    J = anatomy.J

    def shape(P, g):
        out = P.copy()
        for side in ("Left", "Right"):
            knee, ankle, hip = J[side + "Shin"], J[side + "Ankle"], J[side + "Thigh"]
            sel = np.where(((g.cats == LEG) | (g.cats == FOOT)) &
                           ((P[:, 0] < 0) == (side == "Left")) & (P[:, 1] < hip[1] - 0.05))[0]
            if len(sel) == 0:
                continue
            axis = (ankle - knee) / np.linalg.norm(ankle - knee)
            length = np.linalg.norm(ankle - knee)
            ref = np.array([0.0, 0.0, 1.0]) - axis[2] * axis
            ref /= np.linalg.norm(ref)
            ortho = np.cross(axis, ref)
            d = P[sel] - knee
            t = d @ axis
            radial = d - t[:, None] * axis
            r = np.linalg.norm(radial, axis=1)
            ang = np.arctan2(radial @ ortho, radial @ ref)
            bins = np.clip(((ang + math.pi) / (2 * math.pi) * 24).astype(int), 0, 23)
            knee_band = np.abs(t) < 0.04
            ref_r = np.zeros(24)
            for b in range(24):
                m = knee_band & (bins == b)
                if m.any():
                    ref_r[b] = r[m].max()
            for b in range(24):
                if ref_r[b] == 0:
                    ref_r[b] = max(ref_r[(b - 1) % 24], ref_r[(b + 1) % 24])
            u = np.clip(t / length, 0.0, 1.0)
            target = np.where(t > 0, ref_r[bins] * (1.0 - 0.05 * u) + loose, r + loose * 0.5)
            new_r = np.maximum(r, target)
            scale = np.where(r > 1e-6, new_r / np.maximum(r, 1e-6), 1.0)
            out[sel] = knee + t[:, None] * axis + radial * scale[:, None]
        return out

    return shape


def shoe_shape(anatomy):
    def shape(P, g):
        out = P.copy()
        low = out[:, 1] < 0.018
        out[low, 1] = np.minimum(out[low, 1], -0.004)
        return out

    return shape


# ---------------------------------------------------------------------- entrée

def dress(body, anatomy, skin_surface):
    """Toutes les pièces pour ce corps, prêtes à assembler. Renvoie {nom: Garment}."""
    pieces = {}

    jean_faces = cut(body, anatomy, "Jean", lambda p, c: anatomy.in_jean(p, c))
    jean = make_garment(body, anatomy, skin_surface, "Jean", jean_faces, 0.006, 4, 0.004,
                        shape=straight_legs(anatomy))
    pieces["Jean"] = jean
    jean_surface = Surface(jean.positions[:len(jean.positions)], vertex_normals(jean.positions, jean.faces))

    shoe_faces = cut(body, anatomy, "Chaussures", lambda p, c: anatomy.in_shoes(p, c))
    pieces["Chaussures"] = make_garment(body, anatomy, skin_surface, "Chaussures", shoe_faces,
                                        0.010, 3, 0.006, shape=shoe_shape(anatomy))

    for name, c in TOP_CUT.items():
        faces = cut(body, anatomy, name, lambda p, cat, n=name: anatomy.in_top(n, p, cat))
        under = jean_surface if name != "Debardeur" else None
        pieces[name] = make_garment(body, anatomy, skin_surface, name, faces, c["offset"], 4, c["thick"],
                                    shape=drape_top(anatomy, c["drape"]), under=under)

    for g in pieces.values():
        garment_uvs(g, anatomy)
        tag_details(g, anatomy)
    return pieces


def tag_details(g, anatomy):
    """Semelle et ceinture : des morceaux du même vêtement, avec leur propre matière."""
    g.tags = []
    for f in g.faces:
        p = g.positions[list(f)].mean(axis=0)
        tag = g.name
        if g.name == "Chaussures" and p[1] < 0.026:
            tag = "Semelle"
        elif g.name == "Jean" and p[1] > anatomy.waist_y - 0.038:
            tag = "Ceinture"
        g.tags.append(tag)


def skin_masks(body, anatomy):
    """Pour chaque face de peau : bits des vêtements qui la cachent (+ TETE pour la tête)."""
    masks = []
    names = [b.name for b in body.bones]
    head_ids = {names.index("Head"), names.index("Neck")}
    for vi, _, g in body.base.faces:
        if g != "body":
            continue
        p = body.coords[vi].mean(axis=0)
        cat = anatomy.face_category(vi)
        m = 0
        for name, bit in TOPS.items():
            if anatomy.in_top(name, p, cat, m=0.03):
                m |= bit
        if anatomy.in_jean(p, cat, m=0.03):
            m |= JEAN
        if anatomy.in_shoes(p, cat, m=0.012):
            m |= CHAUSSURES
        head = 0.0
        for v in vi:
            for j in range(body.bone_ids.shape[1]):
                if body.bone_ids[v, j] in head_ids:
                    head += body.bone_weights[v, j]
        if head / len(vi) > 0.5:
            m |= TETE
        masks.append(m)
    return masks
