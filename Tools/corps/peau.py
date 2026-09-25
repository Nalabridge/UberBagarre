"""
Peinture procédurale de la peau, directement dans les UV MakeHuman.

Chaque texel de l'atlas est ramené à son point 3D sur le corps (rasterisation des faces
dans l'espace des UV). La couleur est alors une fonction de la position anatomique, et non
de l'image : les veines suivent l'avant-bras, les ongles sont au bout des doigts, le rouge
monte sur les jointures, la barbe de trois jours sur la mâchoire — sans aucune couture là
où l'atlas coupe le corps en morceaux.

Sorties : <prefixe>_Albedo.jpg (sRGB) et <prefixe>_Relief.jpg (carte de normales en espace
tangent, calculée depuis une carte de hauteur dans l'espace des UV — donc alignée sur les
tangentes qu'Unity recalcule à partir des mêmes UV).
"""
import math

import numpy as np
from PIL import Image

RES = 2048


# ---------------------------------------------------------------------- bruit

def _hash(ix, iy, iz, seed):
    h = (ix * 73856093) ^ (iy * 19349663) ^ (iz * 83492791) ^ (seed * 2654435761)
    h = h & 0xFFFFFFFF
    h = (h ^ (h >> 13)) * 1274126177 & 0xFFFFFFFF
    h = h ^ (h >> 16)
    return (h & 0xFFFF) / 65535.0


def noise(p, freq, seed=0):
    """Bruit de valeur trilinéaire lissé, p (n, 3) en mètres, fréquence en 1/m."""
    q = p * freq
    i = np.floor(q).astype(np.int64)
    f = q - i
    f = f * f * (3 - 2 * f)
    out = 0.0
    for dx in (0, 1):
        wx = f[:, 0] if dx else 1 - f[:, 0]
        for dy in (0, 1):
            wy = f[:, 1] if dy else 1 - f[:, 1]
            for dz in (0, 1):
                wz = f[:, 2] if dz else 1 - f[:, 2]
                out = out + wx * wy * wz * _hash(i[:, 0] + dx, i[:, 1] + dy, i[:, 2] + dz, seed)
    return out


def fbm(p, freq, octaves=4, seed=0):
    total = 0.0
    amp = 0.5
    norm = 0.0
    for o in range(octaves):
        total = total + noise(p, freq * (2.03 ** o), seed + o * 17) * amp
        norm += amp
        amp *= 0.5
    return total / norm


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------------- atlas

def rasterize(body, res=RES):
    """Pour chaque texel couvert : position 3D, normale, os dominant, creux (cavité)."""
    faces = [(vi, ti) for vi, ti, g in body.base.faces if g == "body"]
    P = body.coords
    normals = np.zeros_like(P)
    for vi, _ in faces:
        a = P[vi[0]]
        for k in range(1, len(vi) - 1):
            fn = np.cross(P[vi[k + 1]] - a, P[vi[k]] - a)
            for v in (vi[0], vi[k], vi[k + 1]):
                normals[v] += fn
    normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-12)

    # Cavité : le sommet est-il en creux par rapport à ses voisins ?
    nb_sum = np.zeros_like(P)
    nb_cnt = np.zeros(len(P))
    for vi, _ in faces:
        k = len(vi)
        for a in range(k):
            u, v = vi[a], vi[(a + 1) % k]
            nb_sum[u] += P[v]
            nb_sum[v] += P[u]
            nb_cnt[u] += 1
            nb_cnt[v] += 1
    lap = nb_sum / np.maximum(nb_cnt, 1)[:, None] - P
    cavity = np.einsum("ij,ij->i", lap, normals)

    pos = np.zeros((res, res, 3), dtype=np.float32)
    nrm = np.zeros((res, res, 3), dtype=np.float32)
    cav = np.zeros((res, res), dtype=np.float32)
    bone = np.full((res, res), -1, dtype=np.int32)
    valid = np.zeros((res, res), dtype=bool)
    uvs = body.base.uvs
    dominant = body.bone_ids[:, 0]

    for vi, ti in faces:
        for tri in ((0, 1, 2), (0, 2, 3)) if len(vi) == 4 else ((0, 1, 2),):
            v = [vi[k] for k in tri]
            t = [ti[k] for k in tri]
            uv = uvs[t] * res - 0.5
            lo = np.floor(uv.min(axis=0)).astype(int)
            hi = np.ceil(uv.max(axis=0)).astype(int)
            lo = np.clip(lo, 0, res - 1)
            hi = np.clip(hi, 0, res - 1)
            xs, ys = np.meshgrid(np.arange(lo[0], hi[0] + 1), np.arange(lo[1], hi[1] + 1))
            px = np.stack([xs.ravel(), ys.ravel()], axis=1).astype(np.float64)
            a, b, c = uv
            den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
            if abs(den) < 1e-12:
                continue
            w0 = ((b[1] - c[1]) * (px[:, 0] - c[0]) + (c[0] - b[0]) * (px[:, 1] - c[1])) / den
            w1 = ((c[1] - a[1]) * (px[:, 0] - c[0]) + (a[0] - c[0]) * (px[:, 1] - c[1])) / den
            w2 = 1 - w0 - w1
            inside = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
            if not inside.any():
                continue
            W = np.stack([w0, w1, w2], axis=1)[inside]
            ix = px[inside, 0].astype(int)
            iy = px[inside, 1].astype(int)
            pos[iy, ix] = W @ P[v]
            n = W @ normals[v]
            nrm[iy, ix] = n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-9)
            cav[iy, ix] = W @ cavity[v]
            best = np.argmax(W, axis=1)
            bone[iy, ix] = dominant[np.array(v)[best]]
            valid[iy, ix] = True
    return pos, nrm, cav, bone, valid


def dilate(img, valid, iterations=12):
    """Étend les couleurs au-delà des bords des îlots (pas de liseré au mipmapping)."""
    img = img.copy()
    mask = valid.copy()
    for _ in range(iterations):
        acc = np.zeros_like(img)
        cnt = np.zeros(mask.shape)
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, 1), (-1, 1), (1, -1)):
            m = np.roll(np.roll(mask, dy, 0), dx, 1)
            acc += np.roll(np.roll(img, dy, 0), dx, 1) * m[..., None]
            cnt += m
        grow = (~mask) & (cnt > 0)
        img[grow] = acc[grow] / cnt[grow][:, None]
        mask = mask | grow
    return img


# ---------------------------------------------------------------------- peinture

def paint(body, prefix, tone, res=RES, seed=7):
    pos, nrm, cav, bone, valid = rasterize(body, res)
    idx = np.where(valid)
    p = pos[idx].astype(np.float64)
    n = nrm[idx].astype(np.float64)
    c = cav[idx].astype(np.float64)
    b = bone[idx]
    names = [x.name for x in body.bones]
    B = {x.name: x for x in body.bones}
    tone = np.array(tone, dtype=np.float64)

    def bone_is(*keys):
        ids = [i for i, nm in enumerate(names) if any(k in nm for k in keys)]
        return np.isin(b, ids)

    # --- teint de base : marbrures larges + grain moyen
    low = fbm(p, 7.0, 3, seed)
    mid = fbm(p, 45.0, 3, seed + 5)
    albedo = tone[None, :] * (1.0 + 0.07 * (low - 0.5)[:, None] + 0.035 * (mid - 0.5)[:, None])

    red = np.zeros(len(p))
    height = np.zeros(len(p))

    # --- mains : plus rouges, jointures, paume claire, ongles
    hand = bone_is("Palm", "Wrist", "Pouce", "Index", "Majeur", "Annulaire", "Auriculaire")
    red += hand * 0.18
    for side in ("Left", "Right"):
        palm = B[side + "Palm"]
        dorsal = palm.rotation[:, 1]
        palm_side = hand & (np.einsum("ij,j->i", n, dorsal) < -0.35) & ((p[:, 0] < 0) == (side == "Left"))
        light = np.clip(tone ** 0.75 * np.array([1.0, 0.9, 0.87]), 0, 1)
        albedo[palm_side] = albedo[palm_side] * 0.45 + light * 0.55

        for finger in ("Pouce", "Index", "Majeur", "Annulaire", "Auriculaire"):
            for k in (1, 2, 3):
                joint = B["%s%s%d" % (side, finger, k)]
                d = np.linalg.norm(p - joint.position, axis=1)
                on_back = np.einsum("ij,j->i", n, joint.rotation[:, 1]) > 0.2
                strength = 0.55 if k == 1 else 0.4
                red += np.exp(-(d / 0.011) ** 2) * strength * on_back
                if k >= 2:
                    # plis de peau sur le dos des articulations des doigts
                    q = (p - joint.position) @ joint.rotation
                    wrinkle = np.sin(q[:, 2] * 2 * math.pi / 0.0016) * np.exp(-(d / 0.008) ** 2) * on_back
                    height += wrinkle * 0.12

            # ongle sur la dernière phalange, côté dos
            tip = B["%s%s3" % (side, finger)]
            q = (p - tip.position) @ tip.rotation
            length = 0.022 if finger != "Pouce" else 0.026
            width = 0.0058 if finger != "Pouce" else 0.0072
            if finger == "Auriculaire":
                width = 0.0047
                length = 0.018
            along = q[:, 2] / length
            nail = ((along > 0.28) & (along < 1.06) & (q[:, 1] > 0.0) &
                    (np.abs(q[:, 0]) < width * (1.0 - 0.25 * np.clip((along - 0.8) / 0.26, 0, 1))))
            nail_soft = nail.astype(float)
            nail_color = np.clip(tone * np.array([1.12, 0.98, 0.98]) + 0.12, 0, 1)
            edge = nail & (along > 0.93)
            albedo[nail] = albedo[nail] * 0.25 + nail_color * 0.75
            albedo[edge] = np.array([0.93, 0.9, 0.84])
            lunula = nail & (along < 0.40)
            albedo[lunula] = albedo[lunula] * 0.6 + np.array([0.95, 0.9, 0.88]) * 0.4
            height += nail_soft * 0.45
            red += nail_soft * 0.25

    # --- avant-bras et dos des mains : veines
    arm = bone_is("Forearm", "ForearmTwist", "Wrist", "Palm")
    for side in ("Left", "Right"):
        elbow = B[side + "Forearm"].position
        wrist = B[side + "Wrist"].position
        axis = (wrist - elbow) / np.linalg.norm(wrist - elbow)
        sel = arm & ((p[:, 0] < 0) == (side == "Left"))
        d = p - elbow
        t = d @ axis
        radial = d - np.outer(t, axis)
        ref = np.array([0.0, 1.0, 0.0]) - axis[1] * axis
        ref /= np.linalg.norm(ref)
        ang = np.arctan2(radial @ np.cross(axis, ref), radial @ ref)
        coords = np.stack([t * 9.0, np.cos(ang) * 0.6, np.sin(ang) * 0.6], axis=1)
        v1 = noise(coords, 2.2, seed + 31) + 0.5 * noise(coords, 5.0, seed + 37)
        ridge = 1.0 - np.abs(2.0 * (v1 / 1.5) - 1.0)
        vein = smoothstep(0.91, 0.985, ridge) * sel * smoothstep(0.05, 0.14, t)
        vein_color = tone * np.array([0.78, 0.86, 0.98])
        albedo = albedo * (1 - 0.13 * vein[:, None]) + vein_color[None, :] * 0.13 * vein[:, None]
        height += vein * 0.22

        # coude : peau plus épaisse et rougie à l'arrière
        red += np.exp(-(np.linalg.norm(p - elbow, axis=1) / 0.035) ** 2) * 0.25

    # --- genoux
    for side in ("Left", "Right"):
        red += np.exp(-(np.linalg.norm(p - B[side + "Shin"].position, axis=1) / 0.05) ** 2) * 0.2

    # --- visage
    head = bone_is("Head", "Neck")
    eyes = B["Yeux"].position
    r = p - eyes      # repère de la tête = axes du monde (tête droite en liaison)
    face_front = head & (r[:, 2] > -0.04)

    # joues, nez, oreilles
    for sx in (-1, 1):
        cheek = eyes + np.array([sx * 0.042, -0.035, 0.0])
        red += np.exp(-(np.linalg.norm(p - cheek, axis=1) / 0.025) ** 2) * 0.35 * face_front
    nose = eyes + np.array([0.0, -0.03, 0.035])
    red += np.exp(-(np.linalg.norm((p - nose) * np.array([1, 1, 0.6]), axis=1) / 0.018) ** 2) * 0.3
    ears = head & (np.abs(r[:, 0]) > 0.072) & (r[:, 1] > -0.06) & (r[:, 1] < 0.025) & (r[:, 2] < -0.05)
    red += ears * 0.3

    # lèvres
    mouth = eyes + np.array([0.0, -0.072, 0.0])
    lip_front = head & (r[:, 2] > 0.0)
    ly = (p[:, 1] - mouth[1]) / 0.0095
    lx = np.abs(r[:, 0]) / 0.025
    lips = lip_front & (lx < 1.0) & (np.abs(ly) < np.sqrt(np.clip(1.0 - lx ** 2, 0, 1)))
    lip_soft = lips * smoothstep(1.0, 0.7, lx)
    lip_color = tone * np.array([0.86, 0.58, 0.57])
    albedo = albedo * (1 - 0.45 * lip_soft[:, None]) + lip_color[None, :] * 0.45 * lip_soft[:, None]
    height += lip_soft * np.sin(r[:, 0] * 2 * math.pi / 0.0022) * 0.2

    # sourcils : deux arcs effilés, épais côté nez, fins vers la tempe
    hair_color = np.array([0.055, 0.045, 0.04])
    strands = noise(p * np.array([1.0, 7.0, 3.0]), 1100.0, seed + 71)
    for sx in (-1, 1):
        cx = eyes[0] + sx * 0.031
        x = (p[:, 0] - cx) * sx               # < 0 côté nez, > 0 côté tempe
        u = np.clip((x + 0.024) / 0.052, 0.0, 1.0)
        arc = eyes[1] + 0.021 + 0.005 * np.sin(u * math.pi * 0.9)
        half = 0.0056 * (1.0 - 0.55 * u)
        inside = face_front & (x > -0.024) & (x < 0.028)
        edge = smoothstep(half, half * 0.45, np.abs(p[:, 1] - arc))
        ends = smoothstep(-0.024, -0.019, x) * smoothstep(0.028, 0.018, x)
        density = inside * edge * ends * (0.55 + 0.45 * strands)
        albedo = albedo * (1 - 0.9 * density[:, None]) + hair_color[None, :] * 0.9 * density[:, None]
        height += density * 0.15

    # crâne rasé (buzz cut) : une ombre régulière et un grain très fin, pas des graviers
    hairline = 0.062 + (-0.075 - 0.062) * smoothstep(0.02, -0.12, r[:, 2])
    ear_zone = smoothstep(0.066, 0.078, np.abs(r[:, 0])) * smoothstep(0.035, 0.015, r[:, 1]) * \
        smoothstep(-0.06, -0.035, r[:, 1]) * smoothstep(-0.03, -0.05, r[:, 2]) * smoothstep(-0.13, -0.11, r[:, 2])
    scalp = head * smoothstep(-0.004, 0.01, r[:, 1] - hairline) * (1.0 - ear_zone)
    grain = noise(p, 2600.0, seed + 91)
    coverage = scalp * (0.5 + 0.22 * grain)
    albedo = albedo * (1 - coverage[:, None]) + (hair_color * 1.4)[None, :] * coverage[:, None]
    height += scalp * (grain - 0.5) * 0.1

    # barbe de trois jours : mâchoire, menton, moustache, fondue vers le cou et les joues
    beard = head * smoothstep(-0.026, -0.042, r[:, 1]) * smoothstep(-0.155, -0.115, r[:, 1]) * \
        smoothstep(-0.085, -0.055, r[:, 2]) * (1.0 - lips)
    cheek_clear = 1.0 - (smoothstep(-0.056, -0.034, r[:, 1]) * smoothstep(0.018, 0.042, np.abs(r[:, 0])))
    fine = noise(p, 3000.0, seed + 97)
    beard_amount = beard * cheek_clear * (0.30 + 0.25 * fine)
    beard_color = np.array([0.09, 0.08, 0.075])
    albedo = albedo * (1 - beard_amount[:, None]) + beard_color[None, :] * beard_amount[:, None]

    # contour des yeux légèrement plus sombre
    for sx in (-1, 1):
        e = eyes + np.array([sx * 0.032, 0.0, 0.0])
        ring = np.exp(-(np.linalg.norm(p - e, axis=1) / 0.018) ** 2) * face_front
        albedo *= (1 - 0.12 * ring)[:, None]

    # --- rougeur, creux, pores
    red = np.clip(red, 0, 1)
    red_tint = np.array([1.06, 0.80, 0.78])
    albedo = albedo * (1 - red[:, None] * 0.55) + albedo * red_tint[None, :] * red[:, None] * 0.55
    albedo *= (1.0 - np.clip(c * 60.0, 0.0, 0.28))[:, None]
    pores = noise(p, 1100.0, seed + 131)
    height += (pores - 0.5) * 0.10
    height += (fbm(p, 300.0, 2, seed + 137) - 0.5) * 0.12

    albedo = np.clip(albedo, 0, 1)

    # --- images
    img = np.zeros((res, res, 3))
    img[idx] = albedo
    img = dilate(img, valid, 16)
    hmap = np.zeros((res, res, 1))
    hmap[idx] = height[:, None]
    hmap = dilate(hmap, valid, 4)[..., 0]

    # Normales en espace tangent : T = +u, B = +v (Unity, convention OpenGL).
    strength = 0.9
    dhdu = (np.roll(hmap, -1, 1) - np.roll(hmap, 1, 1)) * 0.5
    dhdv = (np.roll(hmap, -1, 0) - np.roll(hmap, 1, 0)) * 0.5
    nx = -dhdu * strength
    ny = -dhdv * strength
    nz = np.ones_like(hmap)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack([nx / length, ny / length, nz / length], axis=2)
    normal_img = (normal * 0.5 + 0.5)

    # ligne 0 de l'image = haut de la texture = v proche de 1
    Image.fromarray((np.flipud(img) * 255 + 0.5).astype(np.uint8)).save(prefix + "_Albedo.jpg", quality=90)
    Image.fromarray((np.flipud(normal_img) * 255 + 0.5).astype(np.uint8)).save(prefix + "_Relief.jpg", quality=92)
    return prefix + "_Albedo.jpg", prefix + "_Relief.jpg"
