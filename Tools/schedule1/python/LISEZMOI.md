# Conversion de la carte (Schedule 1) → pack « Assets/Schedule1 »

Ces scripts transforment la scène exportée par AssetRipper en un pack propre pour Unity 6
(pipeline intégré) : une scène `CarteSchedule1.unity` sans les scripts du jeu d'origine, des
maillages découpés, des matériaux Standard, des textures réduites, le terrain et sa
végétation, plus `reseau.json` (rues, trottoirs, lieux de rendez-vous, logements, portes)
et la vue de dessus de la minicarte.

**Aucun contenu du jeu n'est dans ce dossier** : seulement le code. Le pack produit, lui,
n'entre jamais dans git (`Assets/Schedule1` est ignoré) — il se télécharge à part.

## Préparer

Dans un dossier de travail (hors du dépôt) :

- `Main.unity` : la scène exportée ;
- `src/` : les dossiers exportés `Material`, `Mesh`, `TerrainData`, `TerrainLayer`, `Texture2D` ;
- Python 3 avec `numpy`, `scipy` et `Pillow`.

Copier les scripts dans ce dossier de travail (ou lancer avec `PYTHONPATH`), puis les
lancer **dans l'ordre**, depuis ce dossier.

## Ordre

| Étape | Script | Produit |
|---|---|---|
| 1 | `parse.py` | `scene.pkl` : objets, transforms, maillages, rendus, lumières, colliders |
| 2 | `world.py` | `world.pkl` : matrices monde, hiérarchie |
| 3 | `deps.py` | `keep.pkl` : ce qu'on garde, guid → fichier |
| 4 | `scan.py`, `index.py` | `docs.pkl`, `index.pkl` : documents YAML de la scène, indexés |
| 5 | `pack.py` | `out/Schedule1` : scène, maillages (découpe des lots statiques), matériaux, shaders |
| 6 | `textures.py` | `out/Schedule1/Textures` (≤ 1024 px) |
| 7 | `packscene.py`, `verify_pack.py` | vérification : toutes les références du pack existent |
| 8 | `render_top.py 0.5` | `top_0_5.png/.npz` : vue de dessus (rasteriseur logiciel) |
| 9 | `roads.py`, `graph.py` | réseau des rues (graphe, circuit) |
| 10 | `points.py` | `points.pkl` : spawns, parkings, distributeurs, points de livraison |
| 11 | `reseau.py`, `reseau_portes.py` | `reseau.json`, `CarteDessus.png` (minicarte) |

`room.py X0 X1 Z0 Z1 YMAX YMIN image.png` rend une pièce vue de dessus avec une grille d'un
mètre : c'est ainsi qu'ont été placés les meubles du motel, du bungalow et du manoir.

## Installer le pack

Copier `out/Schedule1` dans `Assets/Schedule1` du projet, laisser Unity importer, puis
**Uber Bagarre → 3b - Construire le MONDE OUVERT** : le constructeur voit la ville et bâtit
le monde ouvert dessus (sinon il retombe sur la ville procédurale).

Les shaders de la carte (`../shaders`) sont copiés dans le pack par `pack.py`.
