"""
Pose d'un corps fabriqué, pour les rendus de contrôle.

Le solveur de bras est la réplique exacte de celui du jeu (IkLimb, mode « charnière ») :
coude placé par la loi des cosinus vers le pôle, bras et avant-bras orientés autour de la
même charnière, moitié de la rotation du poignet reportée sur l'os de torsion de
l'avant-bras. Si un rendu est bon ici, il l'est dans le jeu.
"""
import math

import numpy as np


def euler_unity(x, y, z):
    """Quaternion.Euler(x, y, z) d'Unity, en matrice (ordre Z puis X puis Y, axes du monde)."""
    x, y, z = math.radians(x), math.radians(y), math.radians(z)
    cx, sx, cy, sy, cz, sz = math.cos(x), math.sin(x), math.cos(y), math.sin(y), math.cos(z), math.sin(z)
    rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return ry @ rx @ rz


def axis_angle(axis, degrees):
    a = np.asarray(axis, dtype=np.float64)
    a = a / np.linalg.norm(a)
    t = math.radians(degrees)
    c, s = math.cos(t), math.sin(t)
    x, y, z = a
    return np.array([
        [c + x * x * (1 - c), x * y * (1 - c) - z * s, x * z * (1 - c) + y * s],
        [y * x * (1 - c) + z * s, c + y * y * (1 - c), y * z * (1 - c) - x * s],
        [z * x * (1 - c) - y * s, z * y * (1 - c) + x * s, c + z * z * (1 - c)],
    ])


def norm(v):
    n = np.linalg.norm(v)
    return v / n if n > 1e-12 else v


class Pose:
    def __init__(self, bones):
        self.bones = bones
        self.index = {b.name: i for i, b in enumerate(bones)}
        self.bind_rot = [b.rotation for b in bones]
        self.bind_pos = [b.position for b in bones]
        self.world_rot = [r.copy() for r in self.bind_rot]
        self.world_pos = [p.copy() for p in self.bind_pos]

    def i(self, name):
        return self.index[name]

    def local_bind(self, name):
        b = self.bones[self.i(name)]
        if b.parent is None:
            return b.rotation, b.position
        p = self.i(b.parent)
        pr, pp = self.bind_rot[p], self.bind_pos[p]
        return pr.T @ b.rotation, pr.T @ (b.position - pp)

    def refresh_children(self, name, fixed=()):
        """Recalcule le monde des descendants de <name> à partir de leurs locaux de liaison."""
        for k, b in enumerate(self.bones):
            if b.parent is None or b.name in fixed:
                continue
            chain = b.parent
            under = False
            while chain is not None:
                if chain == name:
                    under = True
                    break
                chain = self.bones[self.i(chain)].parent
            if not under:
                continue
            lr, lp = self.local_bind(b.name)
            local_rot = getattr(self, "_extra", {}).get(b.name, np.eye(3))
            p = self.i(b.parent)
            self.world_rot[k] = self.world_rot[p] @ lr @ local_rot
            self.world_pos[k] = self.world_pos[p] + self.world_rot[p] @ lp

    def set_local_extra(self, name, rotation):
        if not hasattr(self, "_extra"):
            self._extra = {}
        self._extra[name] = rotation

    def solve_arm(self, side, target_pos, target_rot, pole, twist_share=0.5):
        up = self.i(side + "UpperArm")
        fo = self.i(side + "Forearm")
        wr = self.i(side + "Wrist")
        tw = self.i(side + "ForearmTwist")

        shoulder = self.world_pos[up]
        l1 = np.linalg.norm(self.bind_pos[fo] - self.bind_pos[up])
        l2 = np.linalg.norm(self.bind_pos[wr] - self.bind_pos[fo])

        to_target = target_pos - shoulder
        dist = np.clip(np.linalg.norm(to_target), 1e-4, (l1 + l2) * 0.9999)
        d = norm(to_target)
        reached = shoulder + d * dist

        pole = norm(pole - np.dot(pole, d) * d)
        cos_a = np.clip((l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist), -1, 1)
        a = math.acos(cos_a)
        elbow = shoulder + d * (math.cos(a) * l1) + pole * (math.sin(a) * l1)

        zu = norm(elbow - shoulder)
        zl = norm(reached - elbow)
        hinge = np.cross(zu, zl)
        if np.linalg.norm(hinge) < 1e-4:
            hinge = np.cross(pole, d)
        hinge = norm(hinge)

        def limb(z):
            x = norm(hinge - np.dot(hinge, z) * z)
            return np.stack([x, np.cross(z, x), z], axis=1)

        self.world_rot[up] = limb(zu)
        self.world_pos[fo] = elbow
        self.world_rot[fo] = limb(zl)

        # Torsion : l'angle, autour de l'avant-bras, entre son Y et le Y de la main — compté
        # à partir de celui de la pose de liaison (la main de liaison n'est pas dans l'axe Y
        # de l'avant-bras : paume tournée vers la cuisse).
        fy = self.world_rot[fo][:, 1]
        bind_offset = self.bind_rot[wr].T @ self.bind_rot[fo][:, 1]     # Y de l'avant-bras vu de la main
        hy = target_rot @ bind_offset
        hy = hy - np.dot(hy, zl) * zl
        tau = math.degrees(math.atan2(np.dot(np.cross(fy, hy), zl), np.dot(fy, hy)))
        _, lp = self.local_bind(side + "ForearmTwist")
        self.world_pos[tw] = elbow + self.world_rot[fo] @ lp
        self.world_rot[tw] = axis_angle(zl, tau * twist_share) @ self.world_rot[fo]

        self.world_pos[wr] = reached
        self.world_rot[wr] = target_rot
        self.refresh_children(side + "Wrist")

    def curl_hand(self, side, curls, thumb_extra=None):
        """curls : {doigt: (base, milieu, bout)} en degrés autour de l'axe X local."""
        for finger, (a, b, c) in curls.items():
            for k, angle in zip((1, 2, 3), (a, b, c)):
                rot = axis_angle([1, 0, 0], angle)
                if k == 1 and finger == "Pouce" and thumb_extra is not None:
                    rot = thumb_extra @ rot
                self.set_local_extra("%s%s%d" % (side, finger, k), rot)
        self.refresh_children(side + "Wrist")

    def skin(self, positions, ids, weights):
        out = np.zeros_like(positions)
        mats = []
        for k in range(len(self.bones)):
            r = self.world_rot[k] @ self.bind_rot[k].T
            t = self.world_pos[k] - r @ self.bind_pos[k]
            mats.append((r, t))
        R = np.stack([m[0] for m in mats])
        T = np.stack([m[1] for m in mats])
        for j in range(ids.shape[1]):
            w = weights[:, j:j + 1]
            r = R[ids[:, j]]
            t = T[ids[:, j]]
            out += w * (np.einsum("nij,nj->ni", r, positions) + t)
        return out
