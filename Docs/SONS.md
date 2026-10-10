# Les sons du jeu : lesquels remplacer, et comment

Tous les bruitages du jeu sont **fabriqués par le code** (synthèse), sauf les coups du paquet
FS Melee. La synthèse dépanne, mais rien ne vaut un vrai enregistrement. Chaque son a donc un
**nom** : un fichier posé sous ce nom le remplace, sans rien changer d'autre.

## Où poser les fichiers

```
Assets/UberBagarre/Resources/Sons/<Catégorie>/<nom>.wav   (ou .ogg, .mp3)
```

Exemple : `Assets/UberBagarre/Resources/Sons/Voitures/klaxon.wav`.

- Le nom du fichier sans l'extension doit être **exactement** celui du tableau (minuscules,
  sans accent, `_` à la place des espaces).
- **Plusieurs variantes** : à la place du fichier, un dossier du même nom rempli de fichiers
  (`Sons/Voitures/choc_fort/choc1.wav`, `choc2.wav`…) — le jeu en tire une au hasard.
- Les **boucles** doivent boucler proprement (début et fin raccordés, pas de silence au bout).
- Dans Unity, pour les boucles longues : *Load Type* = *Streaming* ou *Compressed In Memory*.

Sans fichier, le jeu garde son son fabriqué : rien ne casse s'il en manque.

## Où les trouver (gratuits et utilisables dans un jeu)

| Source | Licence | Remarque |
|---|---|---|
| **Freesound.org** | filtrer sur **CC0** (« Creative Commons 0 ») | la plus grande banque ; vérifier la licence de chaque son |
| **Sonniss — GDC Game Audio Bundle** (sonniss.com/gameaudiogdc) | libre de droits, usage commercial permis | des dizaines de Go de sons pros (voitures, villes, foules, chocs) |
| **Kenney.nl** (Audio) | CC0 | interface, impacts, petits bruits |
| **OpenGameArt.org** | filtrer sur CC0 | boucles d'ambiance, musiques |

À éviter : la banque de la BBC (usage personnel et éducatif seulement) et les sons « gratuits »
qui exigent une mention que le jeu n'affiche pas.

## Par ordre d'importance

### 1. Voitures (`Sons/Voitures/`) — ce qui fait le plus « jeu vidéo » aujourd'hui

| Nom | Ce que c'est | Type | Conseils |
|---|---|---|---|
| `moteur_bas_charge` | moteur 4 cylindres **pied sur l'accélérateur**, régime bas | boucle 1–3 s | enregistré vers **1 600 tr/min** (le jeu change la hauteur selon le régime) ; chercher « car engine loop on load low rpm » |
| `moteur_haut_charge` | même moteur, pied sur l'accélérateur, haut régime | boucle 1–3 s | vers **4 600 tr/min** |
| `moteur_bas_relache` | pied levé (le moteur retient), bas régime | boucle 1–3 s | « off load / decel » |
| `moteur_haut_relache` | pied levé, haut régime | boucle 1–3 s | |
| `klaxon` | klaxon de berline (deux tons) | boucle 0,5–1 s | **le son qui choque le plus** : prendre un vrai klaxon, coupé pour boucler |
| `crissement` | pneus qui crissent (dérapage) | boucle 1–2 s | « tire squeal loop » |
| `roulement` | bruit des pneus sur l'asphalte | boucle 2–4 s | « tire road noise loop », grave et neutre |
| `choc_leger`, `choc_moyen`, `choc_fort` | carrosserie qui cogne / tôle froissée / gros crash avec verre | ponctuel | idéalement un **dossier** de 3–4 variantes chacun |
| `portiere` | ouverture + claquement de portière | ponctuel | |

### 2. Ville et ambiances

| Nom | Ce que c'est | Type |
|---|---|---|
| `Ambiance/rue` | rumeur de ville de jour (circulation lointaine, gens) | boucle 20–60 s |
| `Ambiance/maison` | intérieur calme (frigo, rue étouffée) | boucle |
| `Ambiance/parking` | parking extérieur, vent, ville au loin | boucle |
| `Ambiance/neon` | grésillement de néon | boucle |
| `Ambiance/frigo` | ronronnement de réfrigérateur | boucle |
| `Ambiance/horloge` | tic-tac | boucle |
| `Ambiance/basseduclub` | basses étouffées d'une boîte de nuit à travers le mur | boucle |
| `Ambiance/voiture`, `voiture_2`, `voiture_au_loin`, `voiture_dehors`, `voiture_dehors_2`, `scooter` | une voiture (ou un scooter) qui passe | ponctuel |
| `Ambiance/sirene`, `sirene_au_loin`, `sirene_lointaine` | sirène au loin | ponctuel |
| `Ambiance/klaxon` | klaxon au loin | ponctuel |
| `Ambiance/goutte`, `goutte_2`, `goutte_3` | gouttes (salle de bains, gouttière) | ponctuel |
| `Meteo/pluie` | pluie continue | boucle 10–30 s |
| `Meteo/tonnerre` | coup de tonnerre | ponctuel (dossier de variantes conseillé) |
| `Foule/brouhaha` | foule qui discute (salle, club) | boucle |
| `Foule/clameur`, `Foule/oooh`, `Foule/ovation` | foule qui crie / « oooh » / ovation (un K.O.) | ponctuel |
| `Rue/conversation`, `Rue/engueulade` | deux personnes qui parlent / se disputent | boucle |
| `Rue/bombe_peinture` | bombe de peinture (tag) | ponctuel |

### 3. Police, portes, vol

| Nom | Ce que c'est | Type |
|---|---|---|
| `Police/sirene` | sirène de police (wail) | boucle 2–4 s |
| `Police/radio` | radio de police (grésillement, voix) | ponctuel |
| `Portes/grincement` | porte qu'on ouvre (poignée + gond) | ponctuel |
| `Portes/verrouillee` | poignée secouée, porte fermée à clé | ponctuel |
| `Portes/toc` | on frappe à une porte | ponctuel |
| `Vol/crochet_grattement`, `crochet_goupille`, `crochet_casse`, `serrure_ouverte` | crochetage : grattement, goupille qui saute, crochet qui casse, serrure qui cède | ponctuel |

### 4. Téléphone, interface, courrier

| Nom | Ce que c'est |
|---|---|
| `Telephone/sonnerie` | sonnerie (boucle) |
| `Telephone/vibreur` | vibreur |
| `Telephone/appareil_photo` | déclencheur photo |
| `Telephone/tel_ouvre`, `tel_retour`, `tel_tic` | ouvrir une appli, retour, défilement |
| `Interface/menu_deplacement`, `menu_validation`, `menu_transition`, `menu_voiture_qui_passe` | sons du menu principal |
| `Interface/ecran_deplacement`, `ecran_validation`, `ecran_refus`, `caisse` | écrans (magasins, ordinateur), tiroir-caisse |
| `Courrier/depliage`, `dechirure`, `glissement`, `tampon` | la lettre qu'on ouvre et qu'on lit |

### 5. Musique et combat

| Nom | Ce que c'est |
|---|---|
| `Musique/menu` | musique du menu principal (boucle) |
| `Musique/club` | musique du Vertigo (boucle, électro / hip-hop) |
| `Cinematique/coupe`, `coup_sourd` | souffle et coup sourd de l'entrée en combat |
| `Combat/fracture` | os qui craque (consigne « casser une jambe ») |

Les coups de poing, la garde, les chutes viennent déjà du paquet FS Melee (de vrais
enregistrements). Les voix restent un babillage fabriqué : de vraies voix demanderaient un
enregistrement par réplique.
