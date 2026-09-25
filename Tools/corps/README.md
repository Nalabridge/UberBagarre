# Tools/corps — les corps du jeu

Les personnages d'Über Bagarre (le joueur, Moretti, les frères Kovač, le champion, le public)
sont de vrais corps humains fabriqués à partir de **MakeHuman** (licence **CC0** : utilisable
librement, y compris dans un jeu commercial, sans attribution obligatoire).

```
python3 Tools/corps/fabrique.py              # les quatre silhouettes, ~6 minutes
python3 Tools/corps/fabrique.py --seulement Colosse
python3 Tools/corps/fabrique.py --sans-peau  # garde les textures de peau existantes
```

Prérequis : `pip install numpy scipy pillow`, et une copie de
`https://github.com/makehumancommunity/makehuman` (option `--makehuman` vers `makehuman/data`).

| Fichier | Rôle |
|---|---|
| `makehuman.py` | lecture du maillage hm08, des cibles de morphologie, du squelette et des poids |
| `rig.py` | le squelette du jeu (Pelvis, Spine, Chest, Neck, Head, Yeux, bras, mains, jambes) posé sur les articulations MakeHuman ; fusion et lissage des poids |
| `vetements.py` | tee-shirt, veste, débardeur, jean, ceinture, chaussures taillés dans le « collant » d'aide ; masques de peau cachée |
| `subdivision.py` | un niveau de Catmull-Clark (poids, UV et étiquettes suivent) |
| `peau.py` | texture de peau peinte dans les UV : teint, veines, ongles, jointures rougies, sourcils, crâne rasé, barbe de trois jours, pores ; carte de normales |
| `pose.py` | pose un corps (même solveur de bras que le jeu) pour les rendus de contrôle |
| `fabrique.py` | tout enchaîne et écrit `Assets/UberBagarre/Art/Models/Corps/` |

Silhouettes : **Athlete** (le joueur — yeux à 1,62 m, pile sur la caméra), **Costaud**,
**Sec**, **Colosse** (1,90 m). Chacune a sa teinte de peau et ses réglages de musculature
(`SILHOUETTES` dans `fabrique.py`).

Le format `UBCORPS2` (gzip) est documenté dans `write_binary`. Côté Unity, `CorpsImporter`
assemble pour chaque tenue un maillage qui ne garde que la peau visible et les vêtements
portés, et `FighterBuilder` construit le squelette et câble IK, mains, hitbox et zones.
