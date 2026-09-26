"""Pistes des mains du joueur tirees des clips FS.

Entree : brut2.json (coups, extrait_fs.py) et brut3.json (garde, marche, blocage, avec les pieds).
Tout est lu depuis les YEUX du boxeur, regard tenu vers l'avant : exactement la vue subjective.
Coordonnees Unity (x droite, y haut, z avant). Rotation : orientation absolue de la main dans la
convention de nos os (rig.py).

Trois familles :
- les coups a geste court (directs, crochets) : ecart depuis le poing arme, amplitude calee a
  l'execution sur les poses ecrites ;
- les coups a grand armement (uppercut, coups au corps) : le poing part de sous la hanche, le
  buste tourne de 90 degres. On ne garde que la POUSSEE (du point le plus bas, ou le plus en
  arriere, jusqu'a l'impact) ; a l'execution la courbe est tournee et mise a l'echelle pour aller
  de notre garde a l'impact ecrit (fit), le retour est un simple rappel vers la garde ;
- les boucles de garde : la marche (balancement des poings cale sur le pas), le blocage
  (la garde fermee qui respire et se cale) et l'encaisse (les avant-bras qui absorbent un coup)."""
import json, math, sys, os, numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'corps'))
from rig import frame, normalize
from fabrique import matrix_to_quaternion

ATTACKS = json.load(open('brut2.json'))
LOOPS = json.load(open('brut3.json'))
# Longueur de bras : les deux squelettes du paquet ont le meme bras (28,5 + 24 cm) ; la mesure au
# repos d'un des deux tombe a 37 cm (os de torsion), on ne s'y fie donc pas.
SCALE = 0.519 / 0.525

# clip, attaque, main, au corps, impact (fraction), fit, depart ('z' = le plus en arriere, 'y' = le plus bas),
# rotation ecrite a l'impact (main droite) pour borner l'orientation capturee
TABLE = [
    ("Left Jab", "A_Direct", "Left", False, 0.53, False, 'z', None),
    ("Right Cross", "A_Direct", "Right", False, 0.52, False, 'z', None),
    ("Left Hook", "A_Crochet", "Left", False, 0.28, False, 'z', None),
    ("lefthook 5", "A_Crochet", "Left", False, 0.37, False, 'z', None),
    ("Right Hook", "A_Crochet", "Right", False, 0.34, False, 'z', None),
    ("Left Body Punch", "A_Direct", "Left", True, 0.60, True, 'z', (12, -2, -6)),
    ("Right Body Punch", "A_Direct", "Right", True, 0.48, True, 'z', (12, -2, -6)),
    ("Left Body Punch", "A_Crochet", "Left", True, 0.60, True, 'z', (22, -92, -12)),
    ("Right Body Punch", "A_Crochet", "Right", True, 0.48, True, 'z', (22, -92, -12)),
    ("Uppercut", "A_Uppercut", "Left", False, 0.40, True, 'y', (-55, -6, -150)),
    ("Right Overhand", "A_Crochet", "Right", False, 0.40, True, 'z', (0, -95, -10)),
]
MAX_TURN = 35.0      # ecart maximal (degres) entre le poing capture et le poing ecrit, a l'impact
RETURN_FRAMES = 9


def euler_of(m):
    # Quaternion.eulerAngles d'Unity (ZXY) depuis la matrice.
    x = math.degrees(math.asin(max(-1, min(1, -m[1, 2]))))
    y = math.degrees(math.atan2(m[0, 2], m[2, 2]))
    z = math.degrees(math.atan2(m[1, 0], m[1, 1]))
    return np.array([x, y, z])


def unity_matrix(e):
    x, y, z = [math.radians(a) for a in e]
    rx = np.array([[1, 0, 0], [0, math.cos(x), -math.sin(x)], [0, math.sin(x), math.cos(x)]])
    ry = np.array([[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]])
    rz = np.array([[math.cos(z), -math.sin(z), 0], [math.sin(z), math.cos(z), 0], [0, 0, 1]])
    return ry @ rx @ rz


def axis_angle(axis, deg):
    axis = normalize(axis); a = math.radians(deg); c, s = math.cos(a), math.sin(a); x, y, z = axis
    return np.array([[c + x * x * (1 - c), x * y * (1 - c) - z * s, x * z * (1 - c) + y * s],
                     [y * x * (1 - c) + z * s, c + y * y * (1 - c), y * z * (1 - c) - x * s],
                     [z * x * (1 - c) - y * s, z * y * (1 - c) + x * s, c + z * z * (1 - c)]])


def angle_between(a, b):
    r = a.T @ b
    return math.degrees(math.acos(max(-1, min(1, (np.trace(r) - 1) / 2))))


def rotation_axis(r):
    ang = math.acos(max(-1, min(1, (np.trace(r) - 1) / 2)))
    if ang < 1e-6: return np.array([0, 1.0, 0]), 0.0
    return np.array([r[2, 1] - r[1, 2], r[0, 2] - r[2, 0], r[1, 0] - r[0, 1]]) / (2 * math.sin(ang)), math.degrees(ang)


def hand_frame(p, k, i, l, t, right_hand):
    along = k - p
    across = l - i
    dorsal = normalize(np.cross(along, across))
    h = frame(along, dorsal)
    if (np.dot(t - p, h[:, 0]) > 0) == right_hand:
        h = frame(along, -dorsal)
    return h


def basis(fwd):
    fwd = np.array(fwd, dtype=float); fwd[2] = 0; fwd = normalize(fwd)
    up = np.array([0.0, 0.0, 1.0])
    return np.stack([np.cross(fwd, up), up, fwd])


def series(F, C, s, scale):
    D = np.array([C @ (np.array(f[s]["p"]) - np.array(f["eyes"])) for f in F]) * scale
    R = [hand_frame(C @ np.array(f[s]["p"]), C @ np.array(f[s]["k"]), C @ np.array(f[s]["i"]),
                    C @ np.array(f[s]["l"]), C @ np.array(f[s]["t"]), s == "Right") for f in F]
    return D, R


def q(m):
    return [float(x) for x in matrix_to_quaternion(m)]


tracks = []; lines = []
for clip, attack, side, body, impact, fit, start, design in TABLE:
    c = ATTACKS[clip]; F = c["frames"]; n = len(F); fps = c["fps"]
    scale = SCALE
    other = "Left" if side == "Right" else "Right"
    ki = int(round(impact * (n - 1)))
    fwd = np.array(F[ki][side]["p"]) - np.array(F[ki]["eyes"])
    C = basis(fwd)
    D, R = series(F, C, side, scale); OD, OR = series(F, C, other, scale)
    sp = np.linalg.norm(np.diff(D, axis=0), axis=1) * fps
    # Debut du geste : le poing arme, juste avant la poussee. L'arme lui-meme (parfois derriere la
    # tete : c'est fait pour etre lu de l'exterieur) est laisse au clip ; notre garde en tient lieu.
    lo = max(0, ki - (6 if fit else 10))
    axis = 1 if start == 'y' else 2
    s0 = lo + int(np.argmin(D[lo:ki - 1, axis]))

    # Orientation : bornee autour de celle du coup ecrit (une main capturee sur un buste qui tourne
    # de 90 degres n'a pas a se retrouver telle quelle devant la camera).
    fix = np.eye(3)
    if design is not None:
        want = unity_matrix(design)
        if side == "Left":
            M = np.diag([-1.0, 1, 1]); want = M @ want @ M
        gap = angle_between(R[ki], want)
        if gap > MAX_TURN:
            ax, ang = rotation_axis(want @ R[ki].T)
            fix = axis_angle(ax, ang * (gap - MAX_TURN) / gap)

    rows = []; orows = []
    if fit:
        last = min(n - 1, ki + 2)
        keep = list(range(s0, last + 1))
        for k in keep:
            rows += list(D[k] - D[s0]) + q(fix @ R[k])
            orows += list(OD[k] - OD[s0]) + q(OR[k])
        # Le retour : un rappel vers la garde (le suivi capture repart derriere la tete).
        for j in range(1, RETURN_FRAMES + 1):
            u = j / float(RETURN_FRAMES); u = u * u * (3 - 2 * u)
            rows += list((D[last] - D[s0]) * (1 - u)) + q(fix @ R[last])
            orows += list((OD[last] - OD[s0]) * (1 - u)) + q(OR[last])
        frames = len(keep) + RETURN_FRAMES
    else:
        e = n - 1
        for k in range(ki + 3, n):
            if np.linalg.norm(D[k] - D[s0]) < 0.05 and (k == n - 1 or sp[min(k, len(sp) - 1)] < 0.4): e = k; break
        for k in range(s0, e + 1):
            rows += list(D[k] - D[s0]) + q(R[k])
            orows += list(OD[k] - OD[s0]) + q(OR[k])
        frames = e - s0 + 1

    tracks.append({"name": clip, "attack": attack, "side": 0 if side == "Left" else 1, "body": body, "fit": fit,
                   "fps": fps, "impact": round((ki - s0) / float(frames - 1), 4), "frames": frames,
                   "strike": [round(float(x), 5) for x in rows], "off": [round(float(x), 5) for x in orows]})
    di = D[ki] - D[s0]
    lines.append("%-17s %-10s %-5s c%d fit%d f%d>%d  poussee (%+.2f %+.2f %+.2f)  impact %s" %
                 (clip, attack, side, body, fit, s0, ki, di[0], di[1], di[2], euler_of(fix @ R[ki]).round(0)))


# ------------------------------------------------------------------ boucles de garde
def loop(name, clip, phase_from_feet, relative_to_start, share, max_turn):
    c = LOOPS[clip]; F = c["frames"]; n = len(F); scale = SCALE
    hips = np.array([f["hips"] for f in F]); move = hips[-1] - hips[0]; move[2] = 0
    if np.linalg.norm(move) > 0.3:
        fwd = move                                  # la marche : le sens du deplacement
    else:
        # garde fixe : les poings sont devant le visage
        mid = np.mean([(np.array(f["Left"]["p"]) + np.array(f["Right"]["p"])) * 0.5 - np.array(f["eyes"]) for f in F], axis=0)
        fwd = mid
    C = basis(fwd)
    out = {"name": name, "fps": c["fps"], "frames": n, "phase": 0.0}
    for s in ("Left", "Right"):
        D, R = series(F, C, s, scale)
        ref_p = D[0] if relative_to_start else D.mean(axis=0)
        ref_r = R[0] if relative_to_start else R[n // 2]
        rows = []; worst = 0.0
        for k in range(n):
            dr = R[k] @ ref_r.T                     # ecart d'orientation, dans l'espace des yeux
            ax, ang = rotation_axis(dr)
            if ang > max_turn: dr = axis_angle(ax, max_turn); ang = max_turn
            worst = max(worst, ang)
            rows += list((D[k] - ref_p) * share) + q(dr)
        out[s.lower()] = [round(float(x), 5) for x in rows]
        amp = np.ptp(D, axis=0) * share
        lines.append("%-10s %-5s amplitude (%.3f %.3f %.3f) rotation max %.0f" % (name, s, amp[0], amp[1], amp[2], worst))
    if phase_from_feet:
        # Notre pas : la phase 0 est la pose du pied DROIT (ProceduralLocomotion). Ici : l'image ou le
        # pied droit s'arrete.
        fr = np.array([C @ np.array(f["fr"]) for f in F])
        v = np.linalg.norm(np.diff(fr, axis=0), axis=1) * c["fps"]
        land = next(k for k in range(1, len(v)) if v[k - 1] >= 0.8 and v[k] < 0.8)
        out["phase"] = round(land / float(n - 1), 4)
        lines.append("%-10s pose du pied droit a l'image %d (phase %.3f)" % (name, land, out["phase"]))
    return out


loops = [
    loop("marche", "Combat Walk Fwd", True, False, 1.0, 14.0),
    loop("garde", "Block", False, False, 0.8, 10.0),
    loop("encaisse", "Block Hit", False, True, 0.55, 25.0),
]

json.dump({"version": 2, "source": "FS - Melee Combat System (Fantacode Studios), mains du joueur",
           "tracks": tracks, "loops": loops},
          open('BrasCaptures.json', 'w'), separators=(',', ':'))
print("\n".join(lines)); print("%.1f Ko" % (os.path.getsize('BrasCaptures.json') / 1024.0))
