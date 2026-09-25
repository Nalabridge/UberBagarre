"""
Lecture des données MakeHuman (CC0) : maillage de base hm08, cibles de morphologie,
squelette par défaut et poids de peau.

Les fichiers viennent du dépôt public github.com/makehumancommunity/makehuman
(dossier makehuman/data), publié sous licence CC0 : utilisables librement, y compris
dans un jeu commercial, sans attribution obligatoire.
"""
import json
import os

import numpy as np


class BaseMesh:
    """Le maillage hm08 : sommets, coordonnées de texture, faces (quads) par groupe."""

    def __init__(self, path):
        verts = []
        uvs = []
        faces = []          # (indices sommets, indices uv, groupe)
        group = None
        with open(path, encoding="utf-8") as f:
            for line in f:
                if line.startswith("v "):
                    verts.append([float(x) for x in line.split()[1:4]])
                elif line.startswith("vt "):
                    uvs.append([float(x) for x in line.split()[1:3]])
                elif line.startswith("g "):
                    group = line.split(None, 1)[1].strip()
                elif line.startswith("f "):
                    vi = []
                    ti = []
                    for token in line.split()[1:]:
                        parts = token.split("/")
                        vi.append(int(parts[0]) - 1)
                        ti.append(int(parts[1]) - 1 if len(parts) > 1 and parts[1] else -1)
                    faces.append((vi, ti, group))
        self.coords = np.array(verts, dtype=np.float64)
        self.uvs = np.array(uvs, dtype=np.float64)
        self.faces = faces

    def group_faces(self, name):
        return [f for f in self.faces if f[2] == name]

    def group_vertices(self, name):
        idx = set()
        for vi, _, g in self.faces:
            if g == name:
                idx.update(vi)
        return sorted(idx)


def load_target(path):
    """Une cible : décalages (indice de sommet, dx, dy, dz)."""
    idx = []
    delta = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            if not line.strip() or line.startswith("#"):
                continue
            p = line.split()
            idx.append(int(p[0]))
            delta.append([float(p[1]), float(p[2]), float(p[3])])
    return np.array(idx, dtype=np.int64), np.array(delta, dtype=np.float64).reshape(-1, 3)


class Morphology:
    """
    Les réglages « macro » de MakeHuman, recalculés comme le fait apps/human.py :
    genre, âge, muscle, poids, taille, proportions, et mélange d'origines.
    """

    def __init__(self, data_dir, gender=1.0, age=0.5, muscle=0.5, weight=0.5, height=0.5,
                 proportions=0.5, african=1 / 3, asian=1 / 3, caucasian=1 / 3):
        self.dir = data_dir
        self.gender = gender
        self.age = age
        self.muscle = muscle
        self.weight = weight
        self.height = height
        self.proportions = proportions
        total = african + asian + caucasian
        self.races = {"african": african / total, "asian": asian / total, "caucasian": caucasian / total}

    def _gender(self):
        return {"female": 1.0 - self.gender, "male": self.gender}

    def _age(self):
        a = self.age
        if a < 0.5:
            old = 0.0
            baby = max(0.0, 1 - a * 5.333)
            young = max(0.0, (a - 0.1875) * 3.2)
            child = max(0.0, min(1.0, 5.333 * a) - young)
        else:
            child = baby = 0.0
            old = max(0.0, a * 2 - 1)
            young = 1 - old
        return {"baby": baby, "child": child, "young": young, "old": old}

    @staticmethod
    def _tri(v, lo, mid, hi):
        mx = max(0.0, v * 2 - 1)
        mn = max(0.0, 1 - v * 2)
        return {lo: mn, mid: 1 - (mx + mn), hi: mx}

    def _height(self):
        mx = max(0.0, self.height * 2 - 1)
        mn = max(0.0, 1 - self.height * 2)
        return {"minheight": mn, "maxheight": mx}

    def targets(self):
        """(chemin, poids) de toutes les cibles macro à appliquer."""
        out = []
        m = os.path.join(self.dir, "targets", "macrodetails")
        genders = self._gender()
        ages = self._age()
        muscles = self._tri(self.muscle, "minmuscle", "averagemuscle", "maxmuscle")
        weights = self._tri(self.weight, "minweight", "averageweight", "maxweight")

        for race, rw in self.races.items():
            for g, gw in genders.items():
                for a, aw in ages.items():
                    w = rw * gw * aw
                    if w > 1e-6:
                        out.append((os.path.join(m, "%s-%s-%s.target" % (race, g, a)), w))

        for g, gw in genders.items():
            for a, aw in ages.items():
                for mu, mw in muscles.items():
                    for we, ww in weights.items():
                        w = gw * aw * mw * ww
                        if w > 1e-6:
                            out.append((os.path.join(m, "universal-%s-%s-%s-%s.target" % (g, a, mu, we)), w))
                        for h, hw in self._height().items():
                            wh = w * hw
                            if wh > 1e-6:
                                out.append((os.path.join(m, "height", "%s-%s-%s-%s-%s.target" % (g, a, mu, we, h)), wh))
                        if self.proportions != 0.5:
                            p = "idealproportions" if self.proportions > 0.5 else "uncommonproportions"
                            pw = w * abs(self.proportions - 0.5) * 2
                            if pw > 1e-6:
                                out.append((os.path.join(m, "proportions", "%s-%s-%s-%s-%s.target" % (g, a, mu, we, p)), pw))
        return out


def apply_targets(coords, targets):
    result = coords.copy()
    for path, w in targets:
        if not os.path.exists(path):
            continue
        idx, delta = load_target(path)
        result[idx] += delta * w
    return result


class Skeleton:
    """Le squelette par défaut (163 os) et ses poids de peau."""

    def __init__(self, data_dir):
        rig = os.path.join(data_dir, "rigs")
        s = json.load(open(os.path.join(rig, "default.mhskel"), encoding="utf-8"))
        self.bones = s["bones"]
        self.joints = s["joints"]
        w = json.load(open(os.path.join(rig, "default_weights.mhw"), encoding="utf-8"))
        self.weights = w["weights"]

    def joint(self, coords, name):
        return coords[self.joints[name]].mean(axis=0)

    def head(self, coords, bone):
        return self.joint(coords, self.bones[bone]["head"])

    def tail(self, coords, bone):
        return self.joint(coords, self.bones[bone]["tail"])
