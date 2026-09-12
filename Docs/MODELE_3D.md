# Brancher un vrai modèle 3D

Les bras, les mains et les jambes du prototype sont des primitives Unity. Elles sont là pour
que le gameplay soit jouable sans attendre d'assets — pas pour rester.

Ce document explique comment les remplacer. **Aucun code n'est à modifier.**

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
