"""Pistes des mains du joueur tirees des clips FS.

Position : la main par rapport a SA propre epaule, en ecart depuis le debut du geste (le buste
qui tourne de 90 degres et le pas de 40 cm s'en vont : en vue subjective la camera ne les suit
pas). Rotation : l'orientation absolue de la main, dans la convention de nos os (rig.py), dans le
repere du personnage face a sa cible. Tout en coordonnees Unity (x droite, y haut, z avant)."""
import json, math, sys, os, numpy as np
sys.path.insert(0, '/home/user/UberBagarre/Tools/corps')
from rig import frame, normalize
from fabrique import matrix_to_quaternion
d = json.load(open('brut.json'))
SCALE = 0.519 / 0.525
TABLE = [
    ("Left Jab", "A_Direct", "Left", False, 0.53),
    ("Right Cross", "A_Direct", "Right", False, 0.52),
    ("Left Hook", "A_Crochet", "Left", False, 0.28),
    ("lefthook 5", "A_Crochet", "Left", False, 0.37),
    ("Right Hook", "A_Crochet", "Right", False, 0.34),
]

def euler_of(m):
    # Quaternion.eulerAngles d'Unity (ZXY) depuis la matrice.
    x = math.degrees(math.asin(max(-1, min(1, -m[1, 2]))))
    y = math.degrees(math.atan2(m[0, 2], m[2, 2]))
    z = math.degrees(math.atan2(m[1, 0], m[1, 1]))
    return np.array([x, y, z])

def hand_frame(p, k, i, l, t, right_hand):
    along = k - p
    across = l - i
    dorsal = normalize(np.cross(along, across))
    h = frame(along, dorsal)
    if (np.dot(t - p, h[:, 0]) > 0) == right_hand:
        h = frame(along, -dorsal)
    return h

tracks = []; lines = []
for clip, attack, side, body, impact in TABLE:
    c = d[clip]; F = c["frames"]; n = len(F); fps = c["fps"]
    other = "Left" if side == "Right" else "Right"
    ki = int(round(impact * (n - 1)))
    eyes = np.array([f["eyes"] for f in F])
    fwd = np.array(F[ki][side]["p"]) - eyes[ki]; fwd[2] = 0; fwd = normalize(fwd)
    up = np.array([0.0, 0.0, 1.0]); right = np.cross(fwd, up)
    C = np.stack([right, up, fwd])
    def U(v): return C @ np.asarray(v)
    def series(s):
        # La main vue depuis les yeux du boxeur, regard tenu sur la cible : exactement la vue
        # subjective. Le pas en avant, c'est notre elan qui le fait.
        D = np.array([U(np.array(f[s]["p"]) - np.array(f["eyes"])) for f in F]) * SCALE
        R = [hand_frame(U(f[s]["p"]), U(f[s]["k"]), U(f[s]["i"]), U(f[s]["l"]), U(f[s]["t"]), s == "Right") for f in F]
        return D, R
    D, R = series(side); OD, OR = series(other)
    sp = np.linalg.norm(np.diff(D, axis=0), axis=1) * fps
    # Debut du geste : le poing arme, le point le plus en arriere juste avant la poussee.
    # L'arme lui-meme (parfois derriere la tete : c'est fait pour etre lu de l'exterieur) est
    # laisse au clip ; en vue subjective, notre garde en tient lieu.
    lo = max(0, ki - 10)
    s0 = lo + int(np.argmin(D[lo:ki - 1, 2]))
    e = n - 1
    for k in range(ki + 3, n):
        if np.linalg.norm(D[k] - D[s0]) < 0.05 and (k == n - 1 or sp[min(k, len(sp) - 1)] < 0.4): e = k; break
    rows = []; orows = []
    for k in range(s0, e + 1):
        rows += list(D[k] - D[s0]) + list(matrix_to_quaternion(R[k]))
        orows += list(OD[k] - OD[s0]) + list(matrix_to_quaternion(OR[k]))
    frames = e - s0 + 1
    tracks.append({"name": clip, "attack": attack, "side": 0 if side == "Left" else 1, "body": body, "fps": fps,
                   "impact": round((ki - s0) / float(frames - 1), 4), "frames": frames,
                   "strike": [round(float(x), 5) for x in rows], "off": [round(float(x), 5) for x in orows]})
    di = D[ki] - D[s0]
    lines.append("%-16s %-5s c%d f%d>%d>%d  ecart impact (%+.2f %+.2f %+.2f)  main depart %s  impact %s" %
                 (clip, side, body, s0, ki, e, di[0], di[1], di[2], euler_of(R[s0]).round(0), euler_of(R[ki]).round(0)))
json.dump({"version": 1, "source": "FS - Melee Combat System (Fantacode Studios), mains du joueur", "tracks": tracks},
          open('BrasCaptures.json', 'w'), separators=(',', ':'))
print("\n".join(lines)); print("%.1f Ko" % (os.path.getsize('BrasCaptures.json') / 1024.0))
print("garde du jeu : gauche (-50,18,72)  droite (-50,-16,-76) ; direct a l'impact (0,0,-4) ; crochet (0,-95,-10) ; uppercut (-55,-6,-150)")
