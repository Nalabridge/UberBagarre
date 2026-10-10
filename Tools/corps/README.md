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
| `vetements.py` | tee-shirt, veste, débardeur, jean (coupe droite fuselée), ceinture, chaussures taillés dans le « collant » d'aide ; bandes de boxe (mains et poignets, sur la peau même) ; bords recalés sur leurs lignes de coupe ou lissés (Taubin) ; masques de peau cachée |
| `subdivision.py` | un niveau de Catmull-Clark (poids, UV et étiquettes suivent) |
| `peau.py` | texture de peau peinte dans les UV : teint et rougeurs (pommettes, nez, oreilles), cernes, veines, ongles, jointures rougies, sourcils, ligne des cils, lèvres et commissure, crâne rasé, barbe de trois jours, pores ; carte de normales |
| `pose.py` | pose un corps (même solveur de bras que le jeu) pour les rendus de contrôle |
| `fabrique.py` | tout enchaîne et écrit `Assets/UberBagarre/Art/Models/Corps/` |

Silhouettes : **Athlete** (le joueur — yeux à 1,62 m, pile sur la caméra), **Costaud**,
**Sec**, **Colosse** (1,90 m). Chacune a sa teinte de peau et ses réglages de musculature
(`SILHOUETTES` dans `fabrique.py`).

Le format `UBCORPS2` (gzip) est documenté dans `write_binary`. Côté Unity, `CorpsImporter`
assemble pour chaque tenue un maillage qui ne garde que la peau visible et les vêtements
portés, et `FighterBuilder` construit le squelette et câble IK, mains, hitbox et zones.

Côté Unity, chaque vêtement reçoit une matière de tissu (`CorpsImporter.Fabric`) : la couleur choisie
pour le personnage, posée sur une maille jersey (hauts), un sergé de denim (jean), un grain de cuir
(chaussures, ceinture), un caoutchouc (semelles), un coton enroulé (bandes). Les UV des vêtements
étant en mètres, la texture garde sa taille réelle sur toutes les silhouettes. Le joueur porte les
bandes de boxe (ce sont ses mains qu'on voit tout le temps) ; les autres personnages non.
