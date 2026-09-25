# Brancher un vrai modèle 3D

> **Depuis le lot 7, les corps par défaut sont de vrais corps humains** (MakeHuman, CC0),
> fabriqués par `Tools/corps/fabrique.py` et importés par `Editor/CorpsImporter.cs` — voir
> [Tools/corps/README.md](../Tools/corps/README.md). Ce qui suit reste valable pour remplacer
> un personnage par un modèle de ton choix (Mixamo, asset store, modèle maison).

Ce document explique comment brancher un autre modèle. **Aucun code n'est à modifier.**

---

## Pourquoi ça marche sans rien casser

Le système d'animation ne connaît pas la géométrie : il connaît des **os**.

```
FirstPersonHands  →  « le poing va ici »
      ↓
IkLimb            →  place trois os pour y arriver
      ↓
   os 1, os 2, os 3   ← ces trois références sont les SEULES choses à changer
```

Brancher un modèle consiste donc uniquement à faire pointer `IkLimb`, `HandRig` et `BodyRig`
vers les os du modèle au lieu des primitives. Tous les réglages — garde, coups, timings,
dégâts, locomotion — sont conservés.

L'IK **mesure elle-même** l'orientation et la longueur réelles de chaque os au démarrage
(`IkLimb.CaptureBindPose`). Elle n'impose donc aucune convention au modèle : c'est elle qui
s'adapte, pas l'inverse. Sans ça, il faudrait que chaque os du modèle ait son axe +Z aligné
sur l'os, ce qu'aucun rig du commerce ne fait.

---

## Procédure : un personnage Mixamo (gratuit)

Mixamo est le chemin le plus court : personnages complets, riggés, avec les doigts, et
directement compatibles Humanoid.

### 1. Télécharger

1. [mixamo.com](https://www.mixamo.com) → connexion avec un compte Adobe (gratuit)
2. Onglet **Characters** → choisis un personnage
3. **Download** avec ces options :
   - **Format : FBX for Unity (.fbx)**
   - **Pose : T-pose**
   - *(pas besoin d'animation : le jeu anime le squelette lui-même)*

### 2. Importer dans Unity

1. Glisse le `.fbx` dans `Assets/UberBagarre/Art/Models/` (crée le dossier si besoin)
2. Sélectionne le fichier importé → Inspector → onglet **Rig**
3. **Animation Type = Humanoid** → **Apply**
4. Vérifie : un bouton **Configure...** apparaît, et une coche verte confirme que le squelette
   est reconnu. Si Unity se plaint, clique sur **Configure** et corrige les os en rouge.

> C'est l'étape **Humanoid** qui fait tout le travail : elle permet à Unity de dire
> « ce transform est le coude gauche », sans que le code ait à deviner des noms d'os qui
> changent d'un logiciel de modélisation à l'autre.

### 3. Brancher

1. Ouvre la scène `CombatSandbox`
2. Glisse le modèle **depuis le Project vers la Hierarchy** (n'importe où)
3. Le modèle étant **sélectionné**, lance :
   **Uber Bagarre → 5 - Brancher le modele 3D selectionne**

La Console affiche le rapport : os trouvés, longueurs mesurées, doigts branchés, hauteur de
cheville. Les primitives sont **masquées, pas détruites** — pour revenir en arrière, il suffit
de recocher les `Mesh Renderer` du `Body`.

### 4. Ajuster

| Symptôme | Où régler |
|---|---|
| Le corps flotte ou s'enfonce | `Body ▸ Procedural Locomotion ▸ Ankle Height` |
| Les coudes / genoux partent du mauvais côté | `Pole Direction` sur le `IkLimb` concerné |
| La main est tournée de travers | `End Rotation Offset` sur le `IkLimb` du bras |
| Les doigts se ferment à l'envers | `Curl Axis` du doigt concerné dans `Hand Rig` (inverse le signe) |
| La garde est mal cadrée | `Player ▸ HandsAimAnchor ▸ Guard Pose` |

---

## Si tu n'as que des mains (sans corps)

Même procédure si le modèle est **Humanoid**. Sinon, branche à la main : sélectionne le
`IkLimb` du bras et glisse les trois os du modèle dans `Upper`, `Lower`, `End`, puis les
phalanges dans le `Hand Rig`. C'est exactement ce que fait l'outil, en manuel.

---

## Si le modèle n'a pas de squelette du tout

Un simple mesh se place en enfant du poignet : les doigts ne bougeront plus, mais la main
suivra le bras. Dans ce cas, mets `Hand Rig` en désactivé et prévois deux modèles
(poing fermé / main ouverte) à basculer selon `HandRig.CurrentGrip`.

---

## Les adversaires, pas seulement le joueur

L'outil **5 - Brancher le modele 3D selectionne** habille n'importe quel combattant de la scène : s'il y
en a plusieurs, il demande lequel (Bruno Moretti, un des frères Kovac, le Taureau, l'adversaire du bac à
sable…). Même rig Humanoid, même procédure. Les **bleus** suivent : au démarrage, chaque matière du
modèle est convertie vers le shader `UberBagarre/Peau` (propriétés recopiées). Nomme la matière de
peau avec *Skin* ou *Peau* pour qu'elle bleuisse ; les autres (vêtements) prennent une trace sombre.

> Limite actuelle : sur un modèle à **maillage déformé** (SkinnedMeshRenderer, le cas de Mixamo), la
> marque est placée dans l'espace du personnage entier, pas dans celui du membre : sur un bras qui
> bouge beaucoup, elle glisse un peu. Si ça gêne, la correction consiste à passer la position de la
> marque dans l'espace de l'os touché et à la transmettre au shader avec les poids de peau — faisable,
> mais à faire le jour où un vrai modèle est branché.

## Les objets du décor (voiture, meubles, maison)

Tout le décor est généré par code : la voiture, le canapé, la télé… sont des assemblages de boîtes
construits par `HouseBuilder`, `NightStreetBuilder`, `ParkingBuilder`, `ClubInteriorBuilder`. Pour un
objet **rigide** (voiture, meuble, poubelle), un modèle se branche sans squelette :

1. Glisse le `.fbx` dans `Assets/UberBagarre/Art/Models/`.
2. Dans la scène, dépose-le **en enfant** de l'objet généré (par exemple `Voiture rouillee`), mets sa
   position à zéro, ajuste rotation et échelle.
3. Décoche les `Mesh Renderer` des boîtes d'origine (garde leurs colliders : ils servent aux collisions
   et à l'interaction).

## Important : les scènes sont régénérées

Les scènes sortent des menus **2** et **3**. Un branchement fait à la main dans la scène **disparaît**
à la prochaine génération. Pour qu'un modèle reste, il faut que le générateur le pose lui-même : c'est
une modification de code de quelques lignes par modèle (charger le `.fbx` à un chemin connu, l'instancier
à la place des boîtes, appeler `HumanoidModelBinder.Bind` pour un personnage). Donne-moi les fichiers
(poussés dans le dépôt, dans `Assets/UberBagarre/Art/Models/`) et dis-moi ce qu'ils remplacent : je
fais le branchement dans les générateurs, pour que chaque reconstruction de scène les remette en place.

Ce qui aide :

- **Personnages** : FBX **Humanoid** en T-pose (Mixamo convient), un seul maillage ou quelques-uns,
  matières nommées (peau / vêtements). Pas besoin d'animations : le jeu anime le squelette lui-même.
- **Objets** : FBX à l'échelle réelle (1 unité = 1 mètre), l'avant vers +Z, pivot au sol.
- **Textures** : à côté du FBX, ou intégrées. Albédo, normales et rugosité suffisent.

Je ne vois pas l'image du jeu : je vérifie par le code (tailles, orientation, hiérarchie des os) et tu
me dis ce qui cloche à l'écran. C'est la même boucle que pour tout le reste du prototype.
