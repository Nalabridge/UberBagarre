# Über Bagarre — prototype de combat FPS

Prototype de combat aux poings en vue première personne, qui servira plus tard de base au jeu
« Über Bagarre » (commander un bagarreur comme on commande un Uber).
Le dépôt contient maintenant **deux scènes** :

- **`Prologue.unity`** — le début du jeu tel que le dossier le décrit : la planque insalubre, l'appel
  de Sami, l'application illégale qu'on installe quand même, la course à une étoile, le trajet, le
  groupe devant le club, la première bagarre, la photo pour valider, le retour.
- **`CombatSandbox.unity`** — l'établi de combat : pas de narration, tous les réglages sous la main.

Pas encore d'économie, pas de progression, pas de ville ouverte.

Le décor de test est en revanche celui du premier combat décrit dans le dossier : **la rue devant
la boîte de nuit, la nuit, sous la bruine**. Néons, bitume mouillé qui reflète réellement la scène,
cône de lumière des lampadaires, et la vieille voiture rouillée garée devant l'entrée.

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
2. Menu **Uber Bagarre → 6 - Reinitialiser les touches par defaut** — *uniquement si j'ai changé
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

### 5. Si « ça ne fait pas comme un jeu » (on ne bouge pas, la souris sort de la fenêtre)

Dans l'ordre :

1. **Une scène du jeu est-elle ouverte ?** Les scènes ne sont pas dans le dépôt, elles sont
   générées. Sur une nouvelle machine, lance **Uber Bagarre → 3 - Construire la scene Prologue**
   (ou **2 - … Combat Sandbox**). Play dans la scène vide par défaut n'affiche qu'un ciel.
2. **L'onglet Game est-il devant ?** Si c'est l'onglet *Scene* qui s'affiche pendant Play, tu
   regardes l'éditeur, pas le jeu. Dans l'onglet Game, menu déroulant en haut : choisis
   **Play Focused** (ou *Play Maximized*).
3. **Clique une fois dans l'image.** Tant que le jeu n'a pas la main, il affiche
   « CLIQUE DANS LA FENÊTRE POUR JOUER ». Échap rend la souris.
4. **Linux : ouvre une session X11 (Xorg), pas Wayland.** L'éditeur Unity sous Linux est prévu
   pour X11 ; sous Wayland la capture de la souris peut échouer et le curseur sort de la fenêtre.
   Sur l'écran de connexion (roue dentée), choisis par exemple « Ubuntu sur Xorg ». Le jeu
   redemande la capture tout seul si le système la rend, et les déplacements marchent même sans
   capture — mais la visée, elle, a besoin de la souris dans la fenêtre.

---

## Commandes

| Touche | Action |
|---|---|
| **Z Q S D** ou **W A S D** (les deux marchent) | Déplacement |
| **Souris** | Regarder |
| **Maj gauche** (maintenu) | Sprint (uniquement vers l'avant) |
| **C** (maintenu) | S'accroupir |
| **C** pendant une course | **Glissade** |
| **Clic gauche** | **Direct** (alterne gauche / droite) |
| **Clic droit** | **Crochet** |
| **Clic molette** | **Uppercut** |
| **F** | **Coup de pied de face** — lourd, lent, il repousse franchement |
| **V** | **Coup de pied bas** — peu de dégâts, mais c'est lui qui fait **tomber** |
| **Maintenir** molette / F / V | **CHARGE** le coup lourd : jusqu'à ×2,2 dégâts, ×2,8 recul, +45 % de chute |
| **Maintenir** clic gauche / droit | Enchaîne tout seul — les coups rapides se répètent, les lourds se chargent |
| **En sprintant** + attaque | **Charge d'épaule** — 14 de force d'impact, 45 % de chute |
| **En l'air** + attaque | **Coup plongeant** — 24 dégâts, 75 % de chute |
| **En glissade** + attaque | **Balayage** — 85 % de chute |
| **Cible au sol** + coup de pied | **COUP DE GRÂCE** — 28 dégâts |
| **G** | **Coup de tête** — de tout près, 21 dégâts, sonne. *Dans l'histoire, il se débloque au niveau 2* |
| **X** | **Bousculer** — deux paumes dans le torse : peu de dégâts, gros recul, fait de la place à un contre deux |
| **E** (rien de visé) | **Ramasser** l'objet léger devant soi (bouteille, cône, caisse, sac…) — **E** à nouveau pour le lâcher |
| **Clic gauche** (objet en main) | **Lancer** l'objet : il blesse qui il touche, une bouteille éclate |
| **Ctrl gauche** (maintenu) | **Garde** : absorbe 72 % des dégâts, coûte de l'endurance à chaque coup |
| **Ctrl gauche** (tapé au bon moment) | **Parade** : les 0,26 s qui suivent la levée de garde annulent le coup *et* déséquilibrent l'attaquant |
| **Alt gauche** | **Esquive** (direction donnée par WASD, arrière par défaut) |
| **Espace** | Saut |
| **E** | **Interagir** : répondre au téléphone, lire le courrier, monter en voiture, valider un écran |
| **T** | **Sortir / ranger le téléphone** (on peut marcher en le regardant, pas frapper) |
| **Tab** | **Menu de bac à sable** (sandbox uniquement), 5 onglets : Combat (PV, dégâts, nervosité, profils), Vagues, Statistiques, **Graphismes**, Commandes |
| **F1** | Overlay de debug (états, zones, cadence réelle en coups/s, tampon d'entrée, distances) |
| **F3** | **Caméra d'observation** — orbite autour de toi, et le combat continue |
| **+** / **-** | Zoom de la caméra d'observation |
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
| 14 | Zone décidée par la visée, enchaînements, ragdoll à la mort, tête en un seul maillage | ✅ |
| 15 | Menu de bac à sable (PV / dégâts / apparitions), matières texturées, refonte audio | ✅ |
| 16 | Charge, 4 coups contextuels, riposte, jauge d'étourdissement, caméra d'observation | ✅ |
| 17 | Mode vagues, profils d'adversaire, statistiques de combat, réglages sauvegardés | ✅ |
| 18 | Rue de nuit générée de A à Z, bloom et tonemap maison, reflet planaire, cycle jour / nuit | ✅ |
| 19 | Prologue jouable : maison, téléphone en main, application, identification, tutoriel, photo | ✅ |
| 20 | Physique des coups par os, décor qui bouge, coup de tête, bousculade, objets à lancer | ✅ |
| 21 | Progression (argent, XP, niveaux, avis), chapitre 1 : les frères Kovac au parking | ✅ |
| 22 | Nettoyage, documentation, préparation des stats | 🔄 en continu |

---

## Structure

```
Assets/UberBagarre/
  Scripts/
    Core/        input (backend-agnostique), interfaces partagées
    Player/      déplacement, visée, curseur, head bob, pilotage des mains
    View/        squelette, IK deux os, cycle de marche, mains articulées, marques de coup,
                 physique des coups par os (BodyImpactPhysics)
    Combat/      combattant, états, attaques, hitbox/hurtbox par zone, vie, stamina, stats,
                 esquive, garde et parade, chute et relevé
    Enemy/       moteur, IA, répertoire de coups, garde réactive, séquence de test
    Feedback/    camera shake, recul, arrêt sur impact, vignette progressive, reflets du
                 soleil, réglages de rendu, sons générés
    UI/          HUD de combat, indicateur de garde, overlay de debug (F1)
    Sandbox/     points de spawn, directeur de spawn, menu de réglage, vagues, statistiques
    Story/       moteur d'étapes, sous-titres, objectifs, fondu, tutoriel, scénario du prologue
                 et du chapitre 1, progression (argent, XP, niveaux, avis)
    Phone/       le téléphone tenu en main, ses écrans, son appareil photo
    World/       interaction (E), lieux, groupe devant le club, identification de la cible,
                 objets physiques (frapper, pousser, ramasser, lancer)
  Editor/        outils de génération (scène, décor, matériaux, coups)  -- non inclus dans le build
  Scenes/        CombatSandbox.unity  (généré)
  Settings/      InputBindings.asset  (généré)
  Art/
    Shaders/     UberPost (bloom + tonemap), UberNeon, UberGlow, UberWetGround, UberRain
    Materials/   matériaux générés
    Textures/    textures générées (bitume, flaques, briques, grilles de fenêtres)
    Meshes/      maillages générés (corps, tête, cônes de lumière)
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
  dans les jambes a **70 % de chances de faire tomber** — et un adversaire au sol ne fait rien
  pendant plus de deux secondes. Frapper bas rapporte donc plus que ses dégâts.
- **Le coup de pied bas est l'ouvre-boîte.** 11 dégâts seulement, mais c'est la façon la plus
  fiable d'atteindre les jambes, donc d'enclencher ces 70 %.

### Viser décide la zone

**Tu touches là où tu vises**, pas là où ton poing se trouve. Le réticule annonce la zone ciblée
et son multiplicateur **avant** que tu frappes, et la zone est écrite sous le chiffre de dégâts
après (TETE / CORPS / JAMBES / BLOQUE).

| Ce que tu vises | Zone | Dégâts |
|---|---|---|
| Droit devant | **Tête** | × 1,6 |
| Légèrement sous l'horizon | **Corps** | × 1,0 |
| Franchement vers le bas | **Jambes** | × 0,55 — mais 70 % de chute |

C'était d'abord la position du poing qui décidait, et ça ne pouvait pas marcher : un poing part de
la tête et ne descend pas sous 1,27 m quand on est debout, borné par la longueur du bras. Aucune
position du poing ne pouvait donc atteindre une zone qui s'arrête à 0,92 m. Le détail est dans
[Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md).

### Enchaîner

Chaque coup a une **fenêtre d'enchaînement** qui s'ouvre juste après sa fenêtre d'impact (vers
60-70 % du coup). Relancer dans cette fenêtre interrompt le coup en cours **sans temps de repos** :
un direct peut repartir à 0,14 s au lieu d'attendre les 0,24 s + repos. C'est ça qui fait la
nervosité, pas la durée des coups.

Ce qui te limite, c'est l'**endurance** — et quand elle est vide, le HUD le dit en rouge
clignotant, parce que c'est la première cause de « je ne peux plus rien faire ».

### Les chances de chute, exactement

Elles ne se cumulent pas : c'est la **plus haute** qui s'applique.

| Situation | Chance |
|---|---|
| N'importe quel coup dans les **jambes** | 70 % |
| Coup de pied bas (ailleurs que les jambes) | 55 % |
| Coup de pied de face | 25 % |
| Uppercut / crochet | 12 % / 6 % |
| Coup lourd quand la vie est sous 35 % | 40 % |
| Coup **bloqué** | jamais |

Et jamais deux chutes à moins de 3 secondes d'écart, sinon on ne se relève plus.
- **La garde n'est pas gratuite.** Elle absorbe, mais chaque coup bloqué coûte 11 d'endurance.
  Garde vide = garde brisée, et un étourdissement de 0,7 s.
- **La parade est la récompense du timing.** Lève la garde dans les 0,26 s avant l'impact : le
  coup est annulé, l'attaquant est étourdi 0,6 s et repoussé, et tu récupères 16 d'endurance.
  Les crochets autour du réticule se resserrent pendant la fenêtre, et un « PARADE ! » confirme.
- **Et la parade ouvre une RIPOSTE.** Ton coup suivant, dans la seconde, fait **×2,2**. Sans ça,
  parer ne ferait que ne pas perdre de vie : la récompense resterait passive, et risquer une
  fenêtre de 0,26 s n'en vaudrait pas la peine.
- **Matraquer finit par sonner.** Une jauge d'étourdissement se remplit sous la barre de vie — deux
  fois plus vite sur un coup à la tête. Pleine, l'adversaire est sonné 1,7 s : une ouverture que tu
  as construite, pas un coup de chance. Elle se vide après un répit, donc elle punit le matraquage
  continu sans punir le combat normal.
- **Le sol n'est pas qu'un temps d'attente.** Un adversaire à terre prend un **coup de grâce** à
  28 dégâts. C'est ce qui donne une raison de le faire tomber.
- **Charger change le coup.** Maintiens un coup lourd : jusqu'à ×2,2 dégâts et ×2,8 de recul. Les
  coups rapides, eux, se répètent au maintien — chaque touche garde le comportement qui découle du
  coup lui-même.
- **Tout coûte de l'endurance** : les coups, le sprint (13/s), la glissade (18 par départ).
  La glissade ne se spamme plus — sans endurance, elle est refusée avant de partir.
- **L'adversaire se défend aussi.** Il esquive, et sinon il **bloque** : ses poings se collent au
  menton juste avant. Matraquer la même touche finit par ne plus rien donner.
- **Ce que tu lui fais se voit.** Il recule réellement (25 cm sur un direct, un bon demi-mètre sur
  un crochet, plus d'un mètre sur un coup de pied), il tombe, il se relève en deux temps, et les
  **bleus** restent là où tu as frappé.
- **Et quand il meurt, il s'effondre pour de vrai.** Un ragdoll physique articulé se construit à
  l'instant du K.O. — 11 segments, articulations, masses. Le coup fatal marque la zone qu'il a
  touchée, donc aucune mort ne ressemble à la précédente sans une seule animation.
- **Ta vie se voit aussi** : plus elle descend, plus les bords de l'écran se referment. À 20 % de
  vie, tu ne vois plus que le centre.

---

## Le prologue

`Uber Bagarre → 3 - Construire la scene Prologue`, puis Play.

### Ce qui se passe

| | |
|---|---|
| **02:47, la planque** | Un matelas par terre, deux meubles de cuisine, des bouteilles là où elles sont tombées. Sur la table du salon : trois relances d'impayés. Les lire est le premier objectif, et c'est la seule chose qui explique la suite. |
| **L'appel** | Sami. Quatre phrases, pas une de plus — l'appli, le principe, la paie, l'illégalité. Il envoie le lien. |
| **L'installation** | « Source inconnue. Application non référencée. Diffusion interdite sur le territoire. » Et un bouton **INSTALLER QUAND MÊME**. |
| **La course** | RDV BASTON, une étoile sur cinq, 150 €. Puis la fiche du sujet : antécédents, localisation approximative, et surtout le **signalement**. |
| **Le trajet** | Monter dans la vieille bagnole rouillée. Fondu au noir, « 20 minutes plus tard ». |
| **Le groupe** | Cinq personnes devant le club. Aucune n'est marquée. Il faut les **dévisager** une par une et comparer avec le signalement. |
| **La bagarre** | Quatre consignes qui attendent une réussite, pas un délai : deux directs, un crochet, une garde, un coup à la tête. Puis le K.O. |
| **La preuve** | Sortir le téléphone, cadrer le corps au sol, déclencher. Photo hors cadre = photo refusée. |
| **Le retour** | Course validée, 150 €, et le premier avis client. « Propre et rapide. Il a rien dit, il a fait. » |

### Le téléphone

C'est l'objet central du jeu, donc il est construit comme un objet et pas comme un menu : une coque,
une dalle émissive et une petite lampe. Il se lève et se baisse avec **T**, il s'incline, il vibre
quand ça sonne, il **éclaire les mains dans le noir** et il **apparaît dans les reflets du bitume
mouillé**.

Son interface n'est pas collée par-dessus l'image : elle est dessinée **sur la dalle**, à partir de
ses quatre coins projetés à l'écran. Elle suit donc l'inclinaison du poignet et le balancement.

Deux conséquences de conception, assumées : **on peut marcher en regardant son écran** — c'est la
posture même du personnage — et **on ne peut pas frapper en le tenant**. Sortir le téléphone en plein
combat est une décision.

### Comment on trouve Bruno Moretti

Personne n'est marqué à l'arrivée. Le mauvais réflexe aurait été de poser une flèche au-dessus de la
cible : le joueur n'aurait alors rien repéré du tout, il aurait suivi une flèche, et le signalement
n'aurait servi à rien.

Ici il faut **regarder les gens**. Rester une seconde sur quelqu'un affiche ce qu'on voit de lui —
et c'est au joueur de comparer avec la fiche. Le marqueur n'apparaît qu'**après**, sur celui qu'il a
reconnu : il ne désigne pas la cible, il confirme une décision. Les quatre autres sont des corps
identiques au sien, avec le même rig et la même respiration ; seule la couleur des vêtements change.

### Le tutoriel

Chaque consigne **attend un résultat**. Tant que les deux directs ne sont pas placés, l'étape ne
passe pas. Ce n'est pas une punition, c'est la seule garantie que la touche a été essayée — un
tutoriel qui affiche une commande trois secondes puis passe à la suite n'a rien enseigné à qui
regardait ailleurs.

Et si le joueur met K.O. avant la fin des consignes, elles sautent : il a déjà prouvé qu'il avait
compris.

### Deux lieux, une seule scène

La planque est à 600 mètres de la rue dans la même scène, et un seul lieu est allumé à la fois. Le
fondu au noir du trajet rend les deux méthodes strictement identiques pour le joueur — alors que deux
scènes Unity obligeraient le joueur, son téléphone, le scénario et la fiche de mission à survivre au
chargement, donc à exister en double le temps d'une transition.

### Si une étape ne passe pas

La console journalise chaque changement d'étape (`Etape 7/22 : lien`). C'est le premier endroit où
regarder : un scénario bloqué et un scénario terminé se ressemblent beaucoup à l'écran.

---

## Le chapitre 1 — deux étoiles

Le prologue ne s'arrête plus sur un carton : il enchaîne. Pour tester le chapitre sans rejouer le
prologue, coche **`Start At Chapter One`** sur `PrologueDirector` (objet `=== Histoire ===`) — la
course du prologue est alors encaissée d'office, niveau 2 compris.

| | |
|---|---|
| **Fin du prologue** | La photo validée paie 150 € et **120 XP** : c'est le **niveau 2**, et le téléphone ouvre la page de **réputation** (niveau, XP, argent, avis). Capacité débloquée : **coup de tête (G)**. |
| **Deux jours plus tard** | L'appli vibre : RDV BASTON **deux étoiles**. Deux sujets, une course, 320 €. Fiche : **les frères Kovac**, survêtements, un noir, un bordeaux. |
| **Le parking du Vertigo** | Niveau -1, mâts à LED froids, bitume mouillé qui reflète, une camionnette blanche portes ouvertes. Les frères déchargent, de dos. |
| **L'embuscade** | Elle part quand on s'approche de la camionnette — ou dès qu'on les attaque de loin, bouteille comprise. |
| **Ce qui change à deux** | Trois consignes : **X** pour bousculer (écarter l'un, casser sa garde), **G** pour le coup de tête qu'on vient de gagner, **E + clic** pour lancer une bouteille. Le parking en est rempli, avec des caisses et des fûts qui roulent. |
| **Deux preuves** | Une photo **par frère**, au sol. Le téléphone compte (« 1 / 2 »), refuse un doublon et refuse un sujet encore debout. |
| **Niveau 3** | 260 XP de plus : capacité **Encaisseur** (+15 PV max). Nouvel avis : 4 étoiles, « Efficace. Un peu brutal pour le prix. » |
| **La planque, 01:05** | Le compte est fait avec **ton** argent réel contre le loyer de 640 €. Sami rappelle : un garage vers le port, trois étoiles. *À suivre.* |

---

## La physique des coups

Un coup n'est plus une animation de recul jouée d'un bloc : c'est une **impulsion appliquée au point
d'impact**. Chaque os (bassin, colonne, poitrine, tête, bras, avant-bras, cuisses, tibias) est un
ressort amorti qui reçoit `ω = (r × J) / I` par rapport à son propre pivot, puis revient à la pose
animée en dépassant un peu.

- **La trajectoire du poing décide**, pas le nom du coup : un crochet fait tourner la tête, un
  uppercut la relève, un direct l'envoie en arrière. La hitbox mesure sa vitesse au contact.
- **Ventre** : le corps se plie. **Torse** : coup du lapin, la tête reste en arrière. **Jambe** : la
  cuisse ou le tibia touché part. **Garde** : ce sont les avant-bras qui encaissent.
- **Toi aussi** : un coup à la tête secoue ta caméra dans le sens du coup.
- **Le décor bouge** : bouteilles, cônes, caisses, sacs, poubelles, chaise, télé… Frapper un objet le
  pousse **au point touché** ; il devient dangereux un court instant (un coup de pied dans une
  bouteille la renvoie dans une figure). Le verre éclate. On pousse aussi les objets en marchant.
- Réglage : **TAB → Combat → Physique des coups** (0 = coupé, 1 = normal, 2,5 = cartoon).

---

## Le décor et les graphismes

La scène de test n'est plus une arène abstraite : c'est **la rue devant la boîte, la nuit**, telle
que le dossier la décrit. Tout est généré par code (`Uber Bagarre → 2`), donc lisible et modifiable.

### Ce qu'il y a dans la rue

La façade de la boîte avec son enseigne au néon, son enseigne drapeau, sa marquise et ses hublots ;
le parvis avec tapis rouge, cordon de velours, barrières de file et pupitre du videur ; en face, six
immeubles habités aux fenêtres allumées, quatre commerces avec leur néon, un escalier de secours ;
huit lampadaires au sodium, chacun avec son cône de lumière ; **la vieille voiture rouillée** garée
devant l'entrée, phares allumés ; une silhouette de ville au loin ; benne, sacs, abribus rétroéclairé,
bornes, plots, grilles d'égout ; et une bruine fine qui explique pourquoi le sol est mouillé.

### Les quatre shaders écrits pour ça

Le projet est en Built-in Render Pipeline **sans le paquet Post Processing**. Il a donc fallu écrire
la chaîne à la main — ce qui est une bonne nouvelle : elle est lisible, commentée, et ne dépend de rien.

| Shader | Ce qu'il résout |
|---|---|
| `UberPost` | **Bloom en pyramide** (7 niveaux, préfiltre à moyenne de Karis), tonemap **ACES**, étalonnage, vignette, aberration chromatique, grain. Sans lui, la valeur d'un pixel est plafonnée à 1 : une enseigne au néon rend exactement comme un mur peint en rose. |
| `UberNeon` | Tube de néon non éclairé, au-dessus de 1, avec un cœur plus brillant de face. C'est ce dépassement — et rien d'autre — qui le fait lire comme une **source** et non comme une surface colorée. |
| `UberGlow` | Volumes de lumière additifs (cônes de lampadaire, halos, faisceaux de phares). Une lampe Unity éclaire les surfaces mais laisse l'air parfaitement transparent : on voit un disque clair au sol sans comprendre d'où il vient. |
| `UberWetGround` | **Bitume mouillé avec reflet planaire.** Une seconde caméra rend réellement la scène en miroir : les néons, les phares et les combattants sont dedans, en mouvement, exacts. |

### Pourquoi un reflet planaire et pas une sonde

Une sonde de réflexion capture la scène **une fois, depuis un point fixe**. Elle ne contient donc ni
les combattants, ni les phares allumés, ni rien qui bouge — exactement ce qu'on veut voir dans une
flaque pendant une bagarre. Le reflet planaire coûte un second rendu de la scène (d'où la
demi-résolution et l'absence d'ombres dedans), mais **l'adversaire qui tombe se voit tomber dans la
flaque**. C'est le seul élément de l'image qui fasse descendre la couleur des enseignes jusqu'aux
pieds des personnages, donc le seul qui unifie le haut et le bas du cadre.

### Tab — Graphismes

Un curseur d'**heure** (0 = nuit, 1 = plein jour) qui pilote d'un coup le soleil, la lune, le ciel,
la brume, l'ambiante et **l'extinction de toutes les enseignes** : une nuit n'est pas « la même scène
en moins lumineux », c'est six choses qui changent ensemble. Puis bloom, seuil de bloom, exposition,
saturation, contraste, vignette, aberration, grain, finesse du reflet, et trois ambiances prêtes à
l'emploi — **Sobre**, **Cinéma**, **Bâtard** (qui pousse volontairement au-delà du raisonnable).

Si ça rame : couper **REFLETS** en premier, c'est le seul réglage qui vaut un rendu complet de la scène.

### Espace colorimétrique

À la fin de la génération, l'outil propose de passer le projet en **linéaire** s'il est en gamma.
Ce n'est pas cosmétique : en gamma, Unity additionne les contributions des lampes sur des valeurs
déjà encodées pour l'écran. Deux lampes d'intensité 1 donnent beaucoup plus que 2, les zones
éclairées virent au blanc laiteux et les dégradés autour des lampadaires cassent en bandes. Avec une
trentaine de sources dans la rue, ça se voit immédiatement. Le post-traitement fonctionne dans les
deux cas, mais il ne peut pas rattraper un éclairage calculé faux en amont.

---

## Les outils de réglage

Tout se règle **pendant** le combat, parce que sortir du mode Play fait perdre la situation qu'on
voulait tester.

**Tab — Combat.** Tes PV et tes dégâts, ceux des adversaires, et trois réglages de **nervosité** :
vitesse des coups, force du ralenti d'impact, durée du tampon de touche. Quatre **profils**
d'adversaire volontairement très écartés — Voyou (référence), Boxeur (rapide, fragile, mobile),
Cogneur (lent, solide), Brute (très lente, très dure). Un adversaire qui diffère de 10 % du
précédent ne se joue pas différemment, donc il n'apprend rien.

**Tab — Vagues.** Des adversaires de plus en plus nombreux **et** de plus en plus solides. À
plusieurs, le combat pose des questions que le duel ne pose pas : se replacer, ne pas se faire
encercler, choisir qui mettre au sol d'abord, garder de l'endurance pour sortir d'une mauvaise
position.

**Tab — Graphismes.** Heure, bloom, exposition, couleur, objectif, finesse du reflet, et trois
ambiances prêtes à l'emploi. Ils existent pour une raison précise : « c'est trop » et « ce n'est pas
assez » ne sont pas des critiques qu'on peut traiter à distance. Le même bloom paraît discret sur un
écran et aveuglant sur un autre, et personne ne peut régler ça à la place de celui qui regarde. Ces
curseurs transforment un désaccord en manipulation. Voir la section précédente.

**Tab — Statistiques.** Coups lancés / au but, **réussite**, meilleur combo, plus gros coup,
parades, blocages, dégâts infligés et encaissés, rapport, chutes, K.O. La réussite est le chiffre le
plus utile : un joueur qui rate la moitié de ses coups a l'impression que l'adversaire encaisse
trop, alors que le problème est sa précision.

**F3 — Caméra d'observation.** Elle orbite autour de toi **et le combat continue** : tu peux
marcher, courir, frapper, te faire toucher et tomber pendant que tu regardes. C'est le seul moyen de
voir ton propre personnage — un FPS a cet angle mort énorme, et tout le travail d'animation, de
matière et de marques de coup porte sur un corps que le joueur ne regarde jamais.

**F1 — Diagnostic.** Cadence réelle en coups/s, écart en ms entre tes deux derniers coups, échelle
de temps courante, état du tampon d'entrée, et les trois zones de l'adversaire avec leur
multiplicateur.

Les réglages sont **sauvegardés** : un réglage trouvé après dix minutes d'essais et perdu au
redémarrage ne vaut rien.

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
| Chute sur coup bas **procédurale**, ragdoll **seulement à la mort** | Tant que le combattant est vivant, son squelette est piloté à chaque image par l'IK et le cycle de marche : un ragdoll se battrait avec eux, et la chute doit finir par un relevé reproductible. La mort, elle, est définitive — plus rien n'a besoin d'être reproductible, et c'est le seul moment où la physique peut prendre la main sans rien casser. | Ragdoll sur un vrai rig importé, avec un mélange de sortie |
| Matières **texturées par code** (grain, tissage) plutôt que couleurs plates | Une couleur plate ne réagit à la lumière que par son orientation : deux surfaces tournées pareil sont identiques, et l'ensemble se lit comme une maquette en plastique. C'est ça que « trop low poly » décrit en réalité. | Vraies textures + normal maps importées |
| Sons **synthétisés sans oscillateur audible** | Un sinus a une hauteur, donc on entend une note — et un corps frappé ne joue pas de note. Tout est du bruit filtré, dont seules l'enveloppe et l'ouverture du filtre changent. | Vrais échantillons |
| Anticrénelage et brume réglés **par code** (`VisualQuality`) | Un projet Unity neuf démarre sans MSAA, avec des ombres courtes et un filtrage minimal. Le générateur de scène ne touche à aucun réglage global du projet. | Quality Settings du projet, ou un volume de post-process |
| Post-traitement **écrit à la main** (`UberPost`) plutôt que le paquet Post Processing | Le paquet n'est pas dans le projet, et l'ajouter imposerait une version et un pipeline. Un shader en quatre passes et un `OnRenderImage` tiennent dans deux fichiers lisibles, et fonctionnent dans un projet vierge. | Volume de post-process URP/HDRP |
| Reflet **planaire** plutôt que réflexions en espace écran | Une réflexion en espace écran perd tout ce qui sort du champ — c'est-à-dire, quand on regarde ses pieds, la totalité de l'enseigne qu'on veut voir reflétée. Le sol étant plat, un seul plan miroir est exact. | Ray tracing matériel, si un jour le projet passe en HDRP |
| Enseignes faites de **tubes** plutôt que de texte 3D | Un `TextMesh` est un quad texturé : il reste plat, ne projette rien et ne se reflète pas correctement. Une enseigne réelle est un volume qui occupe de la place devant le mur. | Modèles d'enseignes importés |
| Décor **généré par code** plutôt que placé à la main | Un `.unity` est un graphe d'objets liés par GUID, illisible hors d'Unity. Le code de construction documente la rue mieux qu'une capture d'écran. | Level design à la main, quand la carte deviendra du contenu et non un banc d'essai |
| Reflets d'objectif dessinés en `OnGUI` (`SunFlare`) | Le composant de flare d'Unity dépend du render pipeline (asset `Flare` en Built-in, composant différent en URP/HDRP). Dessiner les halos soi-même donne le même rendu partout. | Lens flare natif du pipeline choisi |
