# Über Bagarre — prototype de combat FPS

Prototype de combat aux poings en vue première personne, qui servira plus tard de base au jeu
« Über Bagarre » (commander un bagarreur comme on commande un Uber).
**Pour l'instant, ce dépôt ne contient QUE le prototype de combat.** Pas d'app Uber, pas de missions,
pas d'économie, pas de ville.

---

## Démarrage (5 minutes)

### 1. Ouvrir le projet

1. Clone ou mets à jour le dépôt, branche `claude/uber-bagarre-combat-fps-0n0m4w`.
2. Unity Hub → **Add** → **Add project from disk** → sélectionne le dossier racine (celui qui contient `Assets/`).
3. Ouvre-le avec ton Unity **6.x**.

> ⚠️ **Sélectionne bien le dossier RACINE** : celui qui contient `Assets/`, `Packages/` et `ProjectSettings/`.
> Si tu sélectionnes un sous-dossier (`Docs/` par exemple), Unity en fera un projet vide et aucun script
> ne sera chargé — symptôme : le menu `Uber Bagarre` n'apparaît pas et `Assets` est vide.
>
> `ProjectSettings/ProjectVersion.txt` cible `6000.6.0f1`. Si ta version installée diffère, Unity Hub
> proposera de l'ouvrir avec la tienne — accepte, ou édite cette ligne.
> Le premier import prend 1 à 3 minutes (Unity génère `Library/`, les `.meta` et les ProjectSettings manquants).
>
> Le `manifest.json` est volontairement minimal. Si tu veux l'intégration Visual Studio / Rider
> (autocomplétion, ouverture des scripts) : **Window → Package Manager → Unity Registry →
> `Visual Studio Editor` (ou `JetBrains Rider Editor`) → Install**.

### 2. Vérifier la configuration

Menu **Uber Bagarre → 1 - Verifier la configuration du projet**.

Ça imprime dans la Console : version Unity, render pipeline détecté, shader utilisé, et surtout
l'état du système d'input. Si aucun backend d'input n'est actif, l'outil te le dit et propose de corriger
(c'est la panne n°1 sur un projet neuf : le joueur ne bouge pas, sans aucune erreur affichée).

### 3. Construire la scène de test

Menu **Uber Bagarre → 2 - Construire la scene Combat Sandbox**.

Ça génère `Assets/UberBagarre/Scenes/CombatSandbox.unity` : arène, murs, lumières, rig joueur complet,
points de spawn. La scène s'ouvre automatiquement. Appuie sur **Play**.

> Cet outil est **re-jouable** : relance-le après chaque phase pour récupérer les nouveautés.
> Il demande confirmation, car il **remplace** la scène (tes assets — matériaux, réglages, données
> d'attaque — ne sont jamais touchés).

### 4. Après chaque mise à jour du code

1. Récupère le code (`Fetch origin` → `Pull origin` dans GitHub Desktop), puis reviens dans Unity :
   il recompile tout seul dès que la fenêtre reprend le focus.
2. Menu **Uber Bagarre → 3 - Reinitialiser les touches par defaut** — *uniquement si j'ai changé
   des touches.* Un asset garde les valeurs du jour de sa création : une nouvelle touche par défaut
   dans le code ne met **pas** à jour un asset existant.
3. Menu **Uber Bagarre → 2 - Construire la scene Combat Sandbox** — pour appliquer les nouveaux
   réglages par défaut des composants (vitesses, head bob…) et récupérer les nouveaux objets.

> ⚠️ Ces deux commandes **écrasent** l'asset de touches et la scène. Si tu as personnalisé des
> valeurs que tu veux garder, note-les avant.

---

## Commandes

| Touche | Action |
|---|---|
| **W A S D** (ou Z Q S D en AZERTY, selon ton clavier) | Déplacement |
| **Souris** | Regarder |
| **Maj gauche** (maintenu) | Sprint (uniquement vers l'avant) |
| **C** (maintenu) | S'accroupir |
| **C** pendant une course | **Glissade** |
| **Clic gauche** | **Direct** (alterne gauche / droite) |
| **Clic droit** | **Crochet** |
| **Clic molette** | **Uppercut** |
| **Ctrl gauche** (maintenu) | Garde serrée (les poings remontent vers le visage) |
| **Alt gauche** | **Esquive** (direction donnée par WASD, arrière par défaut) |
| **Ctrl gauche** (maintenu) | Garde |
| **Espace** | Saut |
| **F1** | Overlay de debug (états, cooldowns, distances) |
| **R** | Relancer le combat (tout le monde à plein, retour au spawn) |
| **Échap** | Libérer le curseur (pour revenir à l'éditeur) |
| Clic dans la vue | Recapturer le curseur |

Toutes les touches sont dans un seul asset : `Assets/UberBagarre/Settings/InputBindings.asset`.
Tu peux les changer sans toucher à une ligne de code.

---

## Avancement

| Phase | Contenu | État |
|---|---|---|
| 1 | Structure du projet, scène sandbox, rig joueur, déplacement FPS, visée, spawn | ✅ |
| 2 | Mains FPS, position de garde, respiration | ✅ |
| 2b | Corps complet, cycle de marche avec appui des pieds, accroupi, glissade | ✅ |
| 3 | Architecture de combat, données d'attaque, hitbox, dégâts | ✅ |
| 4 | Direct, crochet, uppercut, timings différenciés, variantes | ✅ |
| 4b | IK agnostique au squelette + branchement d'un modèle 3D en un clic | ✅ |
| 5 | Maillages générés (segments coniques, boîtes adoucies) au lieu des primitives | ✅ |
| 6 | Vie, statistiques, calcul de dégâts centralisé, réactions aux coups | ✅ |
| 7 | Camera shake, vignette rouge, arrêt sur impact, sons générés | ✅ |
| 8 | Esquive avec fenêtre d'invulnérabilité, stamina | ✅ |
| 9 | Ennemi complet : IA, répertoire de coups configurable, séquence scriptée | ✅ |
| 10 | Esquive de l'ennemi, machine à états, HUD, overlay de debug | ✅ |
| 11 | Nettoyage, documentation, préparation des stats | 🔄 en continu |

---

## Structure

```
Assets/UberBagarre/
  Scripts/
    Core/        input (backend-agnostique), interfaces partagées
    Player/      déplacement, visée, curseur, head bob, pilotage des mains
    View/        squelette, IK deux os, cycle de marche, mains articulées
    Combat/      combattant, états, attaques, hitbox/hurtbox, vie, stamina, stats, esquive
    Enemy/       moteur, IA, répertoire de coups, séquence de test
    Feedback/    camera shake, recul, arrêt sur impact, vignette, sons générés
    UI/          HUD de combat, overlay de debug (F1)
    Sandbox/     points de spawn, directeur de spawn
  Editor/        outils de génération (scène, validation)  -- non inclus dans le build
  Scenes/        CombatSandbox.unity  (généré)
  Settings/      InputBindings.asset  (généré)
  Art/           matériaux et textures placeholder (générés)
Docs/
  ARCHITECTURE.md   pourquoi le code est organisé comme ça + comment l'étendre
```

Voir **[Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md)** pour les décisions de conception,
et **[Docs/MODELE_3D.md](Docs/MODELE_3D.md)** pour remplacer les primitives par un vrai modèle.

---

## Choix assumés pour le prototype

Ces choix existent pour que le projet **compile et tourne du premier coup dans un projet vierge**,
sans dépendre d'un asset payant ni d'un package optionnel. Chacun est remplaçable plus tard sans
réécrire le combat.

| Choix | Pourquoi | Remplacement prévu |
|---|---|---|
| Aucun `asmdef` | Les scripts vivent dans `Assembly-CSharp`, qui référence automatiquement tous les packages. Zéro problème de référence manquante. | À ajouter quand le projet grossira |
| Input abstrait derrière `IInputProvider` | Fonctionne avec l'ancien Input Manager **et** le nouveau Input System | Asset `.inputactions` si besoin de manettes/rebinding runtime |
| Animations **procédurales** pilotées par données | Le projet n'a aucun clip ni rig : impossible de livrer de « vraies » animations. Les poses-clés sont éditables dans l'Inspector. | `ICombatAnimator` → implémentation Animator/Mecanim |
| HUD et vignette en `OnGUI` | Pas de TextMeshPro, pas de police, pas de post-processing requis. Paramétrable dans l'Inspector. | Canvas uGUI / UI Toolkit |
| Sons générés par code | Aucun fichier audio dans le projet | Vrais samples |
| Primitives Unity pour le corps et les mains | Aucun modèle 3D disponible | **Un clic** : `Uber Bagarre → 5 - Brancher le modele 3D`, voir [Docs/MODELE_3D.md](Docs/MODELE_3D.md) |
