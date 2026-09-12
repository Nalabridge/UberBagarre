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
   Cette commande crée aussi les **nouveaux coups** manquants et affine les maillages déjà
   présents, sans toucher aux coups que tu as modifiés.

> Les **nouvelles touches** (F, V) arrivent toutes seules : un champ ajouté dans le code prend sa
> valeur par défaut sur un asset existant. La commande 3 n'est utile que si j'ai **changé** une
> touche déjà présente.

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
| **F** | **Coup de pied de face** — lourd, lent, il repousse franchement |
| **V** | **Coup de pied bas** — peu de dégâts, mais c'est lui qui fait **tomber** |
| **Ctrl gauche** (maintenu) | **Garde** : absorbe 72 % des dégâts, coûte de l'endurance à chaque coup |
| **Ctrl gauche** (tapé au bon moment) | **Parade** : les 0,26 s qui suivent la levée de garde annulent le coup *et* déséquilibrent l'attaquant |
| **Alt gauche** | **Esquive** (direction donnée par WASD, arrière par défaut) |
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
| 11 | Zones de frappe (jambes / corps / tête), chute et relevé, bleus | ✅ |
| 12 | Coups de pied, garde et parade, recul qui se voit, glissade payante | ✅ |
| 13 | Soleil chaud + reflets d'objectif, antialiasing, vignette progressive, maillages affinés | ✅ |
| 14 | Nettoyage, documentation, préparation des stats | 🔄 en continu |

---

## Structure

```
Assets/UberBagarre/
  Scripts/
    Core/        input (backend-agnostique), interfaces partagées
    Player/      déplacement, visée, curseur, head bob, pilotage des mains
    View/        squelette, IK deux os, cycle de marche, mains articulées, marques de coup
    Combat/      combattant, états, attaques, hitbox/hurtbox par zone, vie, stamina, stats,
                 esquive, garde et parade, chute et relevé
    Enemy/       moteur, IA, répertoire de coups, garde réactive, séquence de test
    Feedback/    camera shake, recul, arrêt sur impact, vignette progressive, reflets du
                 soleil, réglages de rendu, sons générés
    UI/          HUD de combat, indicateur de garde, overlay de debug (F1)
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

## Comment se battre

Le combat n'est pas « cliquer jusqu'à ce que la barre descende ». Chaque pièce a une conséquence,
et c'est là tout l'intérêt :

- **Les zones comptent.** La tête encaisse × 1,6, le corps × 1, les jambes × 0,55. Mais un coup
  dans les jambes a une chance sur deux de **faire tomber** — et un adversaire au sol ne fait rien
  pendant plus de deux secondes. Frapper bas rapporte donc plus que ses dégâts.
- **Le coup de pied bas est l'ouvre-boîte.** 11 dégâts seulement, mais 55 % de chances de chute,
  cumulées avec la chance propre à la zone « jambes ». C'est le coup qui crée l'occasion.
- **La garde n'est pas gratuite.** Elle absorbe, mais chaque coup bloqué coûte 11 d'endurance.
  Garde vide = garde brisée, et un étourdissement de 0,7 s.
- **La parade est la récompense du timing.** Lève la garde dans les 0,26 s avant l'impact : le
  coup est annulé, l'attaquant est étourdi 0,6 s et repoussé, et tu récupères 16 d'endurance.
  Les crochets autour du réticule se resserrent pendant la fenêtre, et un « PARADE ! » confirme.
- **Tout coûte de l'endurance** : les coups, le sprint (13/s), la glissade (18 par départ).
  La glissade ne se spamme plus — sans endurance, elle est refusée avant de partir.
- **L'adversaire se défend aussi.** Il esquive, et sinon il **bloque** : ses poings se collent au
  menton juste avant. Matraquer la même touche finit par ne plus rien donner.
- **Ce que tu lui fais se voit.** Il recule réellement (25 cm sur un direct, un bon demi-mètre sur
  un crochet, plus d'un mètre sur un coup de pied), il tombe, il se relève, et les **bleus**
  restent là où tu as frappé.
- **Ta vie se voit aussi** : plus elle descend, plus les bords de l'écran se referment. À 20 % de
  vie, tu ne vois plus que le centre.

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
| Chute **procédurale** et non ragdoll physique | Le squelette est piloté en permanence par l'IK et le cycle de marche : un ragdoll se battrait avec eux à chaque image, et il faudrait désactiver puis resynchroniser toute la chaîne. La chute procédurale est déterministe, donc réglable au degré près. | Ragdoll sur un vrai rig importé, avec un mélange de sortie |
| Anticrénelage et brume réglés **par code** (`VisualQuality`) | Un projet Unity neuf démarre sans MSAA, avec des ombres courtes et un filtrage minimal. Le générateur de scène ne touche à aucun réglage global du projet. | Quality Settings du projet, ou un volume de post-process |
| Reflets d'objectif dessinés en `OnGUI` (`SunFlare`) | Le composant de flare d'Unity dépend du render pipeline (asset `Flare` en Built-in, composant différent en URP/HDRP). Dessiner les halos soi-même donne le même rendu partout. | Lens flare natif du pipeline choisi |
