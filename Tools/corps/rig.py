"""
Le squelette du jeu, posé sur un corps MakeHuman.

Les noms et la hiérarchie sont ceux que le reste du code attend (FighterBuilder, IkLimb,
HandRig, BodyRig, ProceduralLocomotion) : Pelvis > Spine > Chest > Neck > Head, les bras
(Clavicle > Shoulder > UpperArm > Forearm > ForearmTwist / Wrist > Palm > doigts) et les
jambes (Hip > Thigh > Shin > Ankle > Toe).

Conventions d'orientation, les mêmes que l'ancien squelette procédural :
- pour un os de membre, +Z suit l'os (vers l'articulation suivante) ;
- pour la main et les doigts, +Y est le DOS de la main, donc fermer un doigt = tourner
  autour de +X (le +Z part vers -Y, côté paume) ;
- pour le bras et l'avant-bras, +X est l'axe de charnière du coude : le solveur du jeu
  peut ainsi orienter les deux os sans torsion parasite ;
- le bassin, la colonne, le cou et la tête gardent les axes du corps (X droite, Y haut,
  Z avant).
"""
import numpy as np


def normalize(v):
    n = np.linalg.norm(v)
    return v / n if n > 1e-12 else v


def frame(z, up):
    """Base orthonormée : Z donné, Y au plus près de <up>, X = Y x Z (repère Unity)."""
    z = normalize(z)
    y = up - np.dot(up, z) * z
    if np.linalg.norm(y) < 1e-6:
        y = np.array([0.0, 1.0, 0.0]) - z[1] * z
    y = normalize(y)
    x = np.cross(y, z)
    return np.stack([x, y, z], axis=1)   # colonnes = axes


IDENTITY = np.eye(3)

# Os MakeHuman -> os du jeu. Tout ce qui n'est pas listé suit son parent le plus proche listé.
MAP = {
    "root": "Pelvis", "pelvis.L": "Pelvis", "pelvis.R": "Pelvis",
    "spine05": "Spine", "spine04": "Spine",
    "spine03": "Chest", "spine02": "Chest", "spine01": "Chest", "breast.L": "Chest", "breast.R": "Chest",
    "neck01": "Neck", "neck02": "Neck", "neck03": "Neck",
    "head": "Head", "jaw": "Head", "eye.L": "Head", "eye.R": "Head",
}

SIDES = (("L", "Left"), ("R", "Right"))
FINGERS = (("finger1", "Pouce"), ("finger2", "Index"), ("finger3", "Majeur"),
           ("finger4", "Annulaire"), ("finger5", "Auriculaire"))

for mh, game in SIDES:
    MAP.update({
        "clavicle." + mh: game + "Clavicle",
        "shoulder01." + mh: game + "UpperArm",
        "upperarm01." + mh: game + "UpperArm",
        "upperarm02." + mh: game + "UpperArm",
        "lowerarm01." + mh: game + "Forearm",
        "lowerarm02." + mh: game + "ForearmTwist",
        "wrist." + mh: game + "Wrist",
        "metacarpal1." + mh: game + "Palm", "metacarpal2." + mh: game + "Palm",
        "metacarpal3." + mh: game + "Palm", "metacarpal4." + mh: game + "Palm",
        "upperleg01." + mh: game + "Thigh", "upperleg02." + mh: game + "Thigh",
        "lowerleg01." + mh: game + "Shin", "lowerleg02." + mh: game + "Shin",
        "foot." + mh: game + "Ankle",
    })
    for f, name in FINGERS:
        for k in (1, 2, 3):
            MAP["%s-%d.%s" % (f, k, mh)] = "%s%s%d" % (game, name, k)
    for t in range(1, 6):
        for k in (1, 2, 3):
            MAP["toe%d-%d.%s" % (t, k, mh)] = game + "Toe"


def resolve(skeleton, mh_bone):
    """Os du jeu pour un os MakeHuman quelconque (les muscles du visage vont à la tête, etc.)."""
    b = mh_bone
    while b is not None:
        if b in MAP:
            return MAP[b]
        b = skeleton.bones[b]["parent"]
    return "Pelvis"


class Bone:
    def __init__(self, name, parent, position, rotation):
        self.name = name
        self.parent = parent
        self.position = position          # position monde de liaison (mètres, repère Unity)
        self.rotation = rotation          # 3x3, colonnes = axes X, Y, Z monde


def build(joint, eye_height):
    """
    <joint(mh_bone, 'head'|'tail')> renvoie une position déjà convertie (Unity, mètres).
    Renvoie la liste ordonnée des os (parents avant enfants).
    """
    bones = []

    def add(name, parent, position, rotation):
        bones.append(Bone(name, parent, np.asarray(position, dtype=np.float64), rotation))

    up = np.array([0.0, 1.0, 0.0])
    forward = np.array([0.0, 0.0, 1.0])

    add("Pelvis", None, joint("root", "head"), IDENTITY)
    add("Spine", "Pelvis", joint("spine05", "head"), IDENTITY)
    add("Chest", "Spine", joint("spine03", "head"), IDENTITY)
    add("Neck", "Chest", joint("neck01", "head"), IDENTITY)
    add("Head", "Neck", joint("head", "head"), IDENTITY)
    # Le milieu des yeux : la caméra du joueur s'y place, les poses de garde s'y rapportent.
    add("Yeux", "Head", (joint("eye.L", "head") + joint("eye.R", "head")) * 0.5, IDENTITY)

    for mh, game in SIDES:
        shoulder = joint("upperarm01." + mh, "head")
        elbow = joint("lowerarm01." + mh, "head")
        twist = joint("lowerarm02." + mh, "head")
        wrist = joint("wrist." + mh, "head")

        knuckle = {}
        for f, name in FINGERS:
            knuckle[name] = joint("%s-1.%s" % (f, mh), "head")

        # Dos de la main : normale au plan (poignet, index, auriculaire), orientée vers le dos.
        # Le dos est du côté opposé au pouce quand on regarde la paume : on oriente la normale
        # pour que le pouce soit à -X sur une main DROITE et à +X sur une main GAUCHE, comme
        # dans le squelette procédural (paume vers le bas, doigts vers l'avant).
        across = knuckle["Auriculaire"] - knuckle["Index"]
        along = knuckle["Majeur"] - wrist
        dorsal = normalize(np.cross(along, across))
        hand = frame(along, dorsal)
        thumb_side = np.dot(knuckle["Pouce"] - wrist, hand[:, 0])
        right_hand = game == "Right"
        if (thumb_side > 0) == right_hand:
            dorsal = -dorsal
            hand = frame(along, dorsal)

        # Charnière du coude : perpendiculaire au plan bras / avant-bras.
        hinge = normalize(np.cross(elbow - shoulder, wrist - elbow))

        def limb(z, x):
            z = normalize(z)
            x = normalize(x - np.dot(x, z) * z)
            y = np.cross(z, x)
            return np.stack([x, y, z], axis=1)

        clavicle = joint("clavicle." + mh, "head")
        add(game + "Clavicle", "Chest", clavicle, frame(shoulder - clavicle, up))
        add(game + "Shoulder", game + "Clavicle", shoulder, IDENTITY)
        add(game + "UpperArm", game + "Shoulder", shoulder, limb(elbow - shoulder, hinge))
        add(game + "Forearm", game + "UpperArm", elbow, limb(wrist - elbow, hinge))
        add(game + "ForearmTwist", game + "Forearm", twist, limb(wrist - elbow, hinge))
        add(game + "Wrist", game + "Forearm", wrist, hand)
        palm = wrist + (knuckle["Majeur"] - wrist) * 0.35
        add(game + "Palm", game + "Wrist", palm, hand)

        palm_centre = (knuckle["Index"] + knuckle["Auriculaire"] + wrist * 2) / 4.0

        for f, name in FINGERS:
            points = [joint("%s-%d.%s" % (f, k, mh), "head") for k in (1, 2, 3)]
            points.append(joint("%s-3.%s" % (f, mh), "tail"))
            parent = game + "Palm"
            for k in range(3):
                z = points[k + 1] - points[k]
                if name == "Pouce":
                    # Le pouce se ferme vers le centre de la paume : son -Y pointe vers elle.
                    to_palm = palm_centre - points[k]
                    to_palm = to_palm - np.dot(to_palm, normalize(z)) * normalize(z)
                    rot = frame(z, -normalize(to_palm))
                else:
                    rot = frame(z, dorsal)
                bone_name = "%s%s%d" % (game, name, k + 1)
                add(bone_name, parent, points[k], rot)
                parent = bone_name

        # Point d'impact du poing (les jointures de l'index et du majeur, côté dos + avant).
        add(game + "Knuckles", game + "Palm",
            (knuckle["Index"] + knuckle["Majeur"]) * 0.5 + hand[:, 2] * 0.012 + hand[:, 1] * 0.004, hand)

        hip = joint("upperleg01." + mh, "head")
        knee = joint("lowerleg01." + mh, "head")
        ankle = joint("foot." + mh, "head")
        toe = joint("toe3-1." + mh, "head")
        add(game + "Hip", "Pelvis", hip, IDENTITY)
        add(game + "Thigh", game + "Hip", hip, frame(knee - hip, forward))
        add(game + "Shin", game + "Thigh", knee, frame(ankle - knee, forward))
        # Le pied est orienté à plat (Z horizontal vers l'avant, Y vers le haut) : la
        # locomotion pose les pieds avec l'orientation du corps, et un pied de liaison
        # incliné vers les orteils se relèverait de 18 degrés au premier pas.
        flat = toe - ankle
        flat[1] = 0.0
        add(game + "Ankle", game + "Shin", ankle, frame(flat, up))
        add(game + "Toe", game + "Ankle", toe, frame(flat, up))

    return bones


def merge_weights(skeleton, vertex_count, bone_index, max_influences=4):
    """
    Poids MakeHuman (par os MakeHuman) fusionnés sur les os du jeu, 4 influences par sommet,
    normalisés. Renvoie (indices [n,4], poids [n,4]).
    """
    acc = [dict() for _ in range(vertex_count)]
    for mh_bone, pairs in skeleton.weights.items():
        game = resolve(skeleton, mh_bone)
        gi = bone_index[game]
        for v, w in pairs:
            if v < vertex_count:
                acc[v][gi] = acc[v].get(gi, 0.0) + w

    idx = np.zeros((vertex_count, max_influences), dtype=np.int32)
    wts = np.zeros((vertex_count, max_influences), dtype=np.float32)
    pelvis = bone_index["Pelvis"]
    for v in range(vertex_count):
        items = sorted(acc[v].items(), key=lambda kv: -kv[1])[:max_influences]
        total = sum(w for _, w in items)
        if total <= 1e-8:
            idx[v, 0] = pelvis
            wts[v, 0] = 1.0
            continue
        for k, (b, w) in enumerate(items):
            idx[v, k] = b
            wts[v, k] = w / total
    return idx, wts


def smooth_weights(idx, wts, faces, bone_count, bones, iterations=3, strength=0.5):
    """
    Lissage laplacien des poids sur les sommets influencés par <bones> (la main) : supprime
    les plis en lame au creux du pouce et entre les doigts quand le poing se ferme.
    <faces> : listes d'indices de sommets (quads ou triangles).
    """
    from scipy import sparse

    n = len(idx)
    rows, cols = [], []
    for face in faces:
        k = len(face)
        for a in range(k):
            rows.append(face[a])
            cols.append(face[(a + 1) % k])
            rows.append(face[(a + 1) % k])
            cols.append(face[a])
    adj = sparse.csr_matrix((np.ones(len(rows)), (rows, cols)), shape=(n, n))
    adj.data[:] = 1.0
    degree = np.asarray(adj.sum(axis=1)).ravel()
    degree[degree == 0] = 1.0
    average = sparse.diags(1.0 / degree) @ adj

    dense = np.zeros((n, bone_count))
    np.put_along_axis(dense, idx, wts, axis=1)
    selected = np.isin(idx, list(bones)) & (wts > 0.02)
    mask = selected.any(axis=1)
    for _ in range(iterations):
        smoothed = average @ dense
        dense[mask] = dense[mask] * (1 - strength) + smoothed[mask] * strength

    order = np.argsort(-dense, axis=1)[:, :idx.shape[1]]
    new_w = np.take_along_axis(dense, order, axis=1)
    total = new_w.sum(axis=1, keepdims=True)
    total[total < 1e-8] = 1.0
    new_w /= total
    idx = np.where(mask[:, None], order, idx).astype(np.int32)
    wts = np.where(mask[:, None], new_w, wts).astype(np.float32)
    return idx, wts
