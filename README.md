# Über Bagarre — prototype de combat FPS

Prototype de combat aux poings en vue première personne, qui servira plus tard de base au jeu
« Über Bagarre » (commander un bagarreur comme on commande un Uber).
Le dépôt contient **deux scènes**, générées par le menu **Uber Bagarre** :

- **LE JEU** (`Prologue.unity`, menu **3 - Construire le JEU (histoire complete)**) — toute l'histoire :
  le prologue (la planque, l'appel de Sami, l'appli illégale, la course à une étoile, le groupe devant
  le club, la photo), le chapitre 1 (les frères Kovac au parking) et le chapitre 2 (l'intérieur du
  Vertigo, la fosse et son public). **C'est cette scène qu'il faut lancer pour jouer.**
- **Le bac à sable** (`CombatSandbox.unity`, menu **2**) — l'établi de combat, sans histoire, tous les
  réglages sous la main. Même là, l'adversaire attend qu'on ait **accepté la course au téléphone**.

Le rendu est en **différé** (toutes les lampes calculées par pixel, avec leurs ombres), avec une
lumière **volumétrique** calculée à partir des vraies lampes, des matériaux **PBR** (cartes de relief,
sondes de réflexion) et un anticrénelage temporel (TAA). Les mains sont un vrai maillage de main,
continu, qui se plie aux articulations.

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

Pour **jouer** : menu **Uber Bagarre → 3 - Construire le JEU (histoire complete)**, puis **Play**.

Pour **tester le combat** sans histoire : menu **Uber Bagarre → 2 - Construire le bac a sable (test du
combat)**. Ça génère `Assets/UberBagarre/Scenes/CombatSandbox.unity`. Au bout de deux secondes, la
commande tombe sur le téléphone : **E** pour l'accepter, et l'adversaire attaque.

> Cet outil est **re-jouable** : relance-le après chaque phase pour récupérer les nouveautés.
> Il demande confirmation, car il **remplace** la scène (tes assets — matériaux, réglages, données
> d'attaque — ne sont jamais touchés).

### 4. Après chaque mise à jour du code

> ⚠️ **Relance toujours « 3 - Construire le JEU »** après une mise à jour : la scène est générée,
> et les nouveautés (voix, ambiances, menu de triche, lampadaires) n'y apparaissent qu'après.

1. Récupère le code (`Fetch origin` → `Pull origin` dans GitHub Desktop), puis reviens dans Unity :
   il recompile tout seul dès que la fenêtre reprend le focus.
2. Menu **Uber Bagarre → 6 - Reinitialiser les touches par defaut** — *uniquement si j'ai changé
   des touches.* Un asset garde les valeurs du jour de sa création : une nouvelle touche par défaut
   dans le code ne met **pas** à jour un asset existant.
3. Menu **Uber Bagarre → 3 - Construire le JEU** (et/ou **2 - … bac a sable**) — pour appliquer les nouveaux
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
   générées. Sur une nouvelle machine, lance **Uber Bagarre → 3 - Construire le JEU (histoire complete)**
   (ou **2 - … bac a sable**). Play dans la scène vide par défaut n'affiche qu'un ciel.
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

### 6. Si l'image saccade ou « se coupe »

1. **Active la synchronisation verticale de la vue Game** : dans la vue Game, menu déroulant du
   format d'image (« Free Aspect »), coche **VSync (Game view only)**. Sans elle, l'image se
   « déchire » en bandes dès que la vue bouge vite — exactement ce qui arrive en combat. Dans le jeu
   compilé, c'est le bouton **VSYNC** de Tab → Graphismes (activé par défaut).
2. **F1** affiche en haut les images par seconde, la pire image récente et les passages du
   ramasse-miettes. La **Console** nomme chaque saut de caméra et sa cause.
3. Tab → Graphismes : l'anticrénelage par défaut est **FXAA** (le plus stable et le moins cher) ; le
   MSAA oblige un rendu bien plus coûteux avec toutes les lampes de la rue.

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
| **Maintenir** molette / F / V | Charge le coup lourd. Les coups rapides, eux, ne se répètent **plus** en maintenant : un coup = un appui |
| *Cadence* | Au moins **0,34 s** entre deux coups (ou la durée du coup + 0,08 s). Un appui fait un peu trop tôt est **gardé** 0,26 s et part dès que possible : on frappe en rythme, sans perdre ses appuis |
| *Endurance* | Tes coups coûtent 40 % de moins qu'à l'adversaire et elle remonte vite (40/s après 0,3 s). **À zéro** : épuisé 0,9 s, et tant que 25 % ne sont pas revenus |
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
| **T** | **Sortir / ranger le téléphone** — quand tu veux (on peut marcher en le regardant, pas frapper) |
| **Flèches** / **molette** | Téléphone sorti : choisir une appli, faire défiler |
| **Entrée** / **clic gauche** / **E** | Téléphone sorti : ouvrir, valider |
| **Retour arrière** / **clic droit** | Téléphone sorti : revenir (sur l'accueil : ranger) |
| **Appli Photo** | Viseur plein écran : **clic gauche** photo, **molette** zoom, **gauche / droite** filtre |
| **Tab** | **Menu de test** — dans le jeu **et** dans le bac à sable : Combat (PV, dégâts, nervosité, profils), **TRICHE**, Vagues (bac à sable), Statistiques, **Graphismes**, Commandes |
| **F6** | **Noclip** : vol à travers les murs (ZQSD dans le regard, Espace monte, C descend, Maj accélère) |
| **F7** | **Godmode** |
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
| 22 | Rendu différé, lumière volumétrique réelle, PBR (relief, sondes), TAA, bruit d'image supprimé | ✅ |
| 23 | Chapitre 2 : intérieur du Vertigo, public animé, musique générée, la commande déclenche le combat | ✅ |
| 24 | Vraies mains (maillage continu déformé par les os), anti-spam, épuisement, TAA, anti-chute | ✅ |
| 25 | Voix des personnages, ambiances sonores par lieu, caméra stable, combat nerveux, adversaire toujours lisible, menu de triche dans le jeu | ✅ |
| 26 | Téléphone avec un vrai système (8 applis), appareil photo plein écran, prise en main réaliste, cinématique d'avant-combat, foule qui se forme, garde et coups en vrille, mouchard de caméra | ✅ |
| 27 | Nettoyage, documentation, préparation des stats | 🔄 en continu |

---

## Structure

```
Assets/UberBagarre/
  Scripts/
    Core/        input (backend-agnostique), interfaces partagées
    Player/      déplacement, visée, curseur, head bob, pilotage des mains
    View/        squelette, IK deux os, cycle de marche, mains articulées, marques de coup,
                 physique des coups par os (BodyImpactPhysics), lumiere volumetrique,
                 lumieres au tempo (BeatLight), lyres (SweepingLight)
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
                 objets physiques (frapper, pousser, ramasser, lancer), public anime
                 (Spectator), musique et foule generees (ClubMusic, CrowdAudio)
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

`Uber Bagarre → 3 - Construire le JEU (histoire complete)`, puis Play.

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
une dalle et une petite lampe. Il se sort **quand on veut** avec **T**, il est **tenu dans la main
droite** (les doigts enroulés autour du bord gauche, le pouce en bas de l'écran), il vibre et
**sonne** quand on appelle, il éclaire faiblement les mains dans le noir.

Il a un vrai **système**, sombre et à la luminosité réglable, avec un écran d'accueil et huit applis :

| Appli | Contenu |
|---|---|
| **RDV BASTON** | la course en cours (contrat, fiche, directive, validation) et le **profil** (niveau, réputation, avis) — apparaît une fois installée |
| **Messages** | Sami (le lien de l'appli, puis ses réactions après chaque course), maman, la banque, SFR, l'agence |
| **Appels** | le journal des appels ; les appels entrants s'y affichent |
| **Photo** | l'appareil photo **plein écran** : grille, cadre de mise au point (vert sur une cible au sol), **zoom à la molette**, 5 **filtres**, vraie photo qui file en vignette |
| **Galerie** | les photos de la partie, en grille et en grand |
| **Banque** | le découvert, le portefeuille de l'appli, les dernières opérations |
| **Carte** | la planque, le Vertigo, le parking — et où tu es |
| **Réglages** | **luminosité** (4 crans), **mode nuit**, sonnerie ou vibreur |

Quand l'histoire a besoin du téléphone (un appel, le lien de Sami, une course, une preuve à
photographier), il ouvre l'appli concernée, comme une notification qu'on touche. On peut en sortir et
y revenir ; une validation ne compte que si l'écran de l'histoire est vraiment affiché.

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

## Le chapitre 2 — trois étoiles, dans la fosse

Pour y aller directement : coche **`Start At Chapter Two`** sur `PrologueDirector`.

| | |
|---|---|
| **Vendredi** | Pas de commande, pas de fiche : juste une adresse. Sami a dit « la salle du fond ». |
| **Le Vertigo** | Retour devant le club, et cette fois on entre (**E** sur la porte). |
| **La salle** | Bar ambré, scène du DJ avec mur d'écrans, piste de dalles lumineuses, lyres qui balaient la fumée, danseurs. La musique est générée, et **tout bat sur son tempo** : dalles, lyres, écrans, têtes des danseurs. |
| **La fosse** | Au fond à droite : un cercle de barrières, quatre projecteurs blancs à la verticale, et une vingtaine de spectateurs qui font mur. Le champion, **LE TAUREAU**, attend. |
| **La commande** | Rien ne se passe tant qu'elle n'est pas tombée : la bagarre commence **quand la commande est reçue sur le téléphone et acceptée** (E). La barrière s'ouvre, la salle rugit. |
| **Le combat** | La barrière se referme derrière toi. Le public regarde l'échange, lève les bras sur les beaux coups, grimace sur les coups durs, exulte au K.O. — chacun avec son propre temps de réaction. |
| **Après** | Photo, 600 €, **niveau 4** (*second souffle* : +30 % d'endurance), avis 5 étoiles. À la planque, le loyer est enfin payé… et quelqu'un avait commandé ce combat contre toi. *À suivre.* |

---

## Les bagarres commencent comme au cinéma

Quand un combat démarre (Bruno Moretti devant le club, les frères Kovac au parking, le Taureau dans la
fosse, l'adversaire du bac à sable), la caméra quitte tes yeux pendant quatre secondes : **bandes
noires**, plan large qui tourne autour des deux hommes, **gros plan sur l'adversaire avec son nom**,
contre-plongée, puis retour en vue subjective sur un « **BAGARRE !** ». Personne ne bouge pendant ce
temps ; **E**, **Espace** ou un clic pour passer.

Dans la rue et au parking, **des badauds arrivent** : ils partent de loin, chacun à son heure,
marchent jusqu'à une place autour du combat, s'arrêtent et regardent — puis réagissent aux coups
(bras levés, grimaces, clameurs) comme le public du club.

---

## Les voix et les ambiances

**Chaque réplique est dite.** Les répliques du scénario ont été enregistrées par une voix de
synthèse neuronale **hors-ligne** ([Piper](https://github.com/rhasspy/piper), voix françaises) :
le personnage a une voix d'homme posée, **Sami** une voix plus aiguë passée dans un combiné de
téléphone, les **frères Kovac** et **le Taureau** des voix plus graves (Dragan plus lent, Milan plus
nerveux), et **l'appli** une voix de femme synthétique. Les fichiers sont dans
`Assets/UberBagarre/Resources/Voix/` (`_liste.txt` dit qui dit quoi). Le sous-titre reste affiché
tant que la voix parle ; **E** passe la réplique et coupe la voix.

Les répliques **calculées en jeu** (une somme, un compte de photos) n'ont pas d'enregistrement : elles
sont **babillées** — des syllabes synthétisées à la hauteur de voix du personnage, au rythme du texte.
Une réplique modifiée dans le code devient babillée jusqu'à ce qu'on relance le générateur :

```
pip install piper-tts soundfile scipy numpy
python3 Tools/generer_voix.py          # --force pour tout régénérer
```

**Chaque lieu a son fond sonore**, synthétisé au premier passage :

| Lieu | Ce qu'on entend |
|---|---|
| **Maison** | la pièce, la pluie étouffée contre la vitre, la ville à travers les murs ; le **frigo** qui ronronne et l'**horloge** de la cuisine (on s'en approche, ça monte) ; parfois une voiture dehors, une sirène au loin |
| **Rue** | la rumeur de la ville, la bruine et ses gouttes, le vent entre les façades ; la **basse du club** à travers la façade, de plus en plus forte vers l'entrée ; voitures qui chuintent sur le bitume mouillé, klaxon, **pin-pon** |
| **Parking** | le vent, la ville plus loin, des gouttes qui résonnent, le **néon** du mât fatigué qui grésille |
| **Club** | la musique et la foule (déjà là au chapitre 2) |

Pendant qu'un personnage parle, l'ambiance s'efface un peu : la pluie ne couvre jamais une réplique.

---

## La lumière

- **Rendu différé** : chaque lampe est calculée par pixel, avec ses ombres. Avant, Unity n'en
  calculait qu'une poignée par objet et bascule les autres « par sommet » — sur un mur de quatre
  sommets, elles n'éclairaient presque rien.
- **Lampadaires = projecteurs** tournés vers le sol, avec ombres, au lieu de lampes ponctuelles.
- **Plus aucun cône en maillage.** Le faisceau dans l'air humide est calculé par le post-traitement
  à partir des **vraies lampes** (position, cône, couleur, portée, intensité, clignotement) :
  intégration exacte le long du rayon de vue, sans tirage aléatoire, donc **sans bruit**. Il
  s'arrête sur la première surface — un combattant devant un lampadaire coupe le faisceau.
- **PBR** : cartes de relief générées pour la brique, le béton, les dalles, le bitume, la rouille, le
  métal ; **sondes de réflexion** dans chaque lieu (le chrome et le béton ciré reflètent les néons au
  lieu du ciel noir).
- **Bruit supprimé** : plus de grain animé ni d'aberration par défaut, reflet du sol en pleine
  résolution avec MSAA, ondulations des flaques éteintes au loin, pluie plus discrète.
- **Anticrénelage** : **TAA** par défaut (la caméra est décalée d'une fraction de pixel à chaque
  image et l'historique est accumulé : les arêtes fines et les reflets ne scintillent plus en
  mouvement). Le bouton de **TAB → Graphismes** fait défiler **TAA → MSAA ×8** (rendu avant, arêtes
  très nettes, moins de lampes par pixel) **→ FXAA → aucun**.
- **Personnages mats** : peau et vêtements beaucoup moins lisses — le reflet rasant des néons
  faisait briller le bord des corps comme du vinyle.
- Réglages : **TAB → Graphismes** (*Lumière dans l'air*, *Anticrénelage*). Les anciens réglages
  sauvegardés sont ignorés : on repart des nouvelles valeurs.

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

**Tab — TRICHE** *(temporaire, pour tester)*. **Godmode**, **endurance infinie**, **noclip**
(on traverse les murs) et **vol** (les murs arrêtent), **un coup = K.O.**, multiplicateur de
**dégâts** jusqu'à ×10, **vitesse du temps**, **figer** les adversaires ou les mettre **tous K.O.**,
**soigner**. Dans le jeu, en plus : passer une réplique ou une **étape**, relancer au **prologue**,
au **chapitre 1** ou au **chapitre 2**, se **téléporter** (maison, rue, parking, club), **+500 €**,
**+1 niveau**. Rien de tout ça n'est sauvegardé : une invincibilité oubliée d'une session à l'autre
fausserait tous les tests suivants. **F6** et **F7** basculent noclip et godmode sans ouvrir le menu.

**F1 — Diagnostic.** En tête : **images par seconde, pire image des 2 dernières secondes, passages
du ramasse-miettes**, et le nombre de **sauts de caméra** détectés avec leur cause. Chaque saut est
aussi écrit dans la **Console** (« Saut de camera : 3.1 deg d'effet de camera : CameraPunch ») : si
l'image saccade encore, c'est là qu'il faut regarder. Ensuite : cadence réelle en coups/s, écart en ms entre tes deux derniers coups, échelle
de temps courante, état du tampon d'entrée, et les trois zones de l'adversaire avec leur
multiplicateur.

Les réglages du bac à sable sont **sauvegardés** : un réglage trouvé après dix minutes d'essais et
perdu au redémarrage ne vaut rien. Dans le jeu, ils ne le sont **pas** : l'équilibrage du bac à sable
ne doit pas fausser l'histoire.

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
| Sons générés par code | Aucun fichier audio dans le projet, sauf les voix | Vrais samples |
| Voix de synthèse neuronale (Piper, hors-ligne) | Un jeu où l'on parle en silence paraît en panne ; des comédiens ne sont pas disponibles pour un prototype | Doublage réel : il suffit de remplacer les `.ogg` de `Resources/Voix` (même nom) |
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
