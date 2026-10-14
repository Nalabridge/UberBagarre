# CLAUDE.md — Über Bagarre

Ce fichier est lu automatiquement par Claude Code au début de chaque session ouverte dans ce
dossier. Il résume **tout ce qu'il faut savoir** pour reprendre le projet sans rien redemander :
qui est le propriétaire, les règles à ne jamais enfreindre, comment le projet est rangé, comment
marche le rendu URP, ce qui a été fait commit par commit, et ce qui reste en cours.

Documentation détaillée déjà dans le dépôt (à lire selon le besoin, elles sont longues) :

| Fichier | Contenu |
|---|---|
| `README.md` (~1100 lignes) | Mode d'emploi du joueur : ouvrir le projet, menus, commandes, chaque système expliqué |
| `Docs/ARCHITECTURE.md` (~2900 lignes, §1 à §33) | Décisions techniques, journal des pannes et de leurs causes, une section par grande passe |
| `Docs/HISTOIRE.md` | L'histoire complète « Hyland cogne » (prologue, 5 actes, 5 fins) |
| `Docs/SONS.md` | Chaque bruitage remplaçable par un fichier : noms, dossiers, où trouver des sons libres |
| `Docs/MODELE_3D.md` | Brancher un modèle 3D humanoïde (procédure Mixamo, symptômes) |
| `Tools/corps/README.md` | Fabrique des corps MakeHuman (ancien pipeline) |
| `Tools/schedule1/python/LISEZMOI.md` | Conversion de la carte Schedule 1 en pack Unity |
| `Tools/shaders_urp/LISEZMOI.md` | Banc de test des shaders URP hors Unity |

---

## 1. Le propriétaire et la façon de lui parler

- Le propriétaire du dépôt (GitHub `Nalabridge`) **n'est pas développeur**. Il teste dans Unity
  et renvoie des retours de jeu (souvent en majuscules, sans ponctuation, en vrac).
- **Répondre en français, simplement, en étapes courtes et numérotées.** Pas de jargon inutile.
  Quand il doit faire une manipulation lui-même, donner le chemin exact (menu, fichier, bouton).
- Il a **peu d'usage disponible** : aller droit au but, pousser le travail dès qu'il est prêt
  (« pousse déjà les modifs tu les reprendras après »).
- Il édite parfois des fichiers **à la main par copier-coller** sur ses instructions → risque de
  fichier coupé en deux (voir §9 « Pièges »). Préférer : « ouvre le fichier sur GitHub → Raw →
  Ctrl+A, Ctrl+C → dans l'éditeur Ctrl+A puis Ctrl+V → Ctrl+S », ou un `Pull` dans GitHub Desktop.
- Il travaille sous **Windows** avec **GitHub Desktop** (Fetch / Pull) et Unity Hub.
- Il veut un ressenti **« à la GTA »** : conduite, combat nerveux (« snappy »), ville vivante.
  Il déteste ce qui « fait IA » ou « jeu de 1980 » (sons synthétiques, klaxons bip-bip,
  personnages laids, PNJ qui ne font rien).
- Il doit parfois **montrer un build à quelqu'un en urgence** : dans ce cas, privilégier les
  correctifs minimaux qui compilent à coup sûr, plutôt que les refontes.

## 2. Règles absolues

1. **Le dépôt reste privé.** Le build `.exe` contient la carte de Schedule 1 → il reste privé
   lui aussi, jamais publié.
2. **Jamais de contenu de Schedule 1 dans git.** `Assets/Schedule1/` est ignoré
   (`.gitignore`) ; le pack de la carte s'installe à la main. `Tools/schedule1` ne contient que du
   **code** de conversion.
3. **Pas d'assets piratés.** Jugés non légitimes et donc **non utilisés** : Fighter Pack,
   POLYGON City, Low Poly Ultimate, Racing Cars. Jugés OK : **UMA 2** (gratuit, Asset Store),
   **PuppetMaster**, **Punch Swing Hit Sounds**, **FS – Melee Combat System** (intégré, voir §6).
4. **Laisser intacts les commits de test du propriétaire** (`map/f`, `test/`, le
   `.unitypackage.bin` de FS Melee à la racine).
5. **Branche de travail : `claude/uber-bagarre-combat-fps-0n0m4w`.** Pousser avec
   `git push -u origin claude/uber-bagarre-combat-fps-0n0m4w` (en cas d'erreur réseau, réessayer
   jusqu'à 4 fois avec 2 s, 4 s, 8 s, 16 s d'attente). **Pas de pull request** sauf demande.
6. Messages de commit **en français**, un titre court puis une liste « - ... » de ce qui change.
   Les terminer par les lignes d'attribution que fournit la session (`Co-Authored-By: …`,
   `Claude-Session: …`). **Aucun identifiant de modèle** dans le code, les commits ou la doc.
7. Le code est commenté **en français**, au même niveau de détail que l'existant (on explique
   le *pourquoi*, pas le *quoi*).

## 3. Versions, ouverture, menus

- **Unity réellement utilisé : 6000.0.23f1** (Unity 6.0) avec **URP 17.0**.
  `ProjectSettings/ProjectVersion.txt` dit encore `6000.6.0f1` (historique) : Unity Hub propose
  d'ouvrir avec la version installée, c'est normal.
- `Packages/manifest.json` est **volontairement minimal** (modules intégrés seulement : animation,
  audio, imageconversion, imgui, jsonserialize, particlesystem, physics, terrain,
  terrainphysics, ui, uielements). **URP n'y est pas** : il est installé sur la machine par le
  menu « 0 - Passer sous URP ». Le code **compile avec ou sans URP** (aucun type URP utilisé
  directement : réflexion et `PackageRequirements` dans les shaders).
- **Les scènes ne sont pas dans git** : elles sont **générées** par le menu `Uber Bagarre` :

| Menu | Rôle |
|---|---|
| `0 - Passer sous URP (le rendu de Schedule 1)` | Installe URP, crée le pipeline, convertit les matériaux (voir §5) |
| `0b - Diagnostic du rendu (copie un rapport)` | Rapport des shaders / pipeline / caméras, à coller dans la discussion |
| `1 - Verifier la configuration du projet` | Version, pipeline, backend d'entrée (corrige « Active Input Handling ») |
| `2 - Construire le bac a sable (test du combat)` | `CombatSandbox.unity` : établi de combat |
| `3 - Construire le JEU (histoire complete)` | `Prologue.unity` : ancienne histoire linéaire (avant la carte) |
| `3b - Construire le MONDE OUVERT (la ville)` | `MondeOuvert.unity` : **le jeu actuel** sur la carte Schedule 1 |
| `4 - Regenerer les coups par defaut` | Réécrit les `AttackData` dans `Resources/Attaques` |
| `5 - Brancher le modele 3D selectionne` | `HumanoidModelBinder` |
| `6 - Reinitialiser les touches par defaut` | Réécrit l'asset `InputBindings` |
| `Preparer la ville (portes, vehicules, murs de la demo)` | `CityPreparation` (automatique aussi) |
| `Afficher ou masquer la ville dans l'editeur` | `MapSceneLoader` |

- **Après chaque mise à jour du code chez lui** : Pull → Unity recompile → relancer **3b**
  (la scène doit être reconstruite pour recevoir les nouveaux objets et réglages).
- Dossiers générés et ignorés par git (régénérés par les menus) :
  `Art/Models/Corps/Generes`, `Art/Animations/Generes`, `Art/Ville/Generes`,
  `Art/Interieurs/Generes`, `Settings/Rendu` (le pipeline URP, propre à chaque machine),
  `Tools/uma/.cache`.

## 4. Hiérarchie des fichiers

```
UberBagarre/                         racine du projet Unity (celle à ouvrir dans Unity Hub)
├── CLAUDE.md                        ce fichier
├── README.md                        mode d'emploi complet
├── Docs/                            ARCHITECTURE, HISTOIRE, SONS, MODELE_3D
├── Packages/manifest.json           modules intégrés seulement (URP installé à part)
├── ProjectSettings/                 seulement ProjectVersion.txt (Unity génère le reste)
├── FS - Melee Combat System …unitypackage.bin   déposé par le propriétaire (ne pas toucher)
├── map/, test/                      fichiers de test du propriétaire (ne pas toucher)
├── Tools/                           outils HORS Unity (Python, Blender en module, C# d'analyse)
│   ├── corps/                       ancien pipeline des corps (MakeHuman CC0) → Corps_*.bytes
│   │     fabrique.py (tout régénérer), makehuman.py, rig.py (squelette du jeu), pose.py
│   │     (même solveur de pose que le jeu), vetements.py, peau.py, subdivision.py
│   ├── uma/                         NOUVEAU pipeline des corps sur UMA 2 (en cours, voir §8)
│   │     umalib.py (lit le .unitypackage UMA via UnityPy), corps_uma.py (silhouettes, tenues,
│   │     export UBCORPS2 + Corps_<sil>.json), apercu.py (planche de contrôle), rendu.py (Blender)
│   ├── mocap/                       extrait_fs.py, cuisson_bras.py : gestes FS → BrasCaptures.json
│   ├── schedule1/                   conversion de la carte (code seul) + AnalyseMap.cs
│   │     python/ (parse, world, deps, scan, index, pack, textures, roads, reseau,
│   │     reseau_portes, correctif_urp …), shaders/ (CarteTriplanaire, CarteDoubleFace, CarteEau)
│   └── shaders_urp/                 compile nos passes URP hors Unity (glslang) : check.py …
└── Assets/
    ├── Fantacode Studios/           FS Melee Combat System : clips, avatars FBX, bruitages
    ├── Schedule1/                   ⚠ IGNORÉ PAR GIT : le pack de la carte, installé à la main
    └── UberBagarre/
        ├── Art/
        │   ├── Animations/BrasCaptures.json    gestes des bras du joueur (cuits hors ligne)
        │   ├── Models/Corps/                   Corps_<Silhouette>[_Foule].bytes (maillage + os,
        │   │                                   format UBCORPS2 gzip), Corps_Athlete.json (méta
        │   │                                   UMA), textures Peau_/Visage_/Tissu_/Yeux_
        │   └── Shaders/                        UberSkin (peau + bleus), UberWetGround, UberCiel,
        │                                       UberSky.hlsl (ciel + eau), UberNeon, UberGlow,
        │                                       UberRain, UberPost, UberInterieur, UberCheveux,
        │                                       UberUrp.hlsl + UberUrpPasses.hlsl (éclairage URP)
        ├── Resources/                          Polices (Roboto) ; Attaques/ et Sons/ créés au besoin
        ├── Editor/                             tout ce qui GÉNÈRE (scènes, ville, intérieurs…)
        │   ├── OpenWorldSceneBuilder*.cs       menu 3b, une partie par domaine (Carte, Histoire,
        │   │                                   Logements, Magasins, Meteo, Police, Saga, Voitures)
        │   ├── CityBuilder*.cs, CityMeshBuilder ville procédurale (sans la carte)
        │   ├── CityPreparation*.cs, MapPack, MapSceneLoader   préparer et charger la carte
        │   ├── CityVehicles.cs                 voitures de la carte rendues conduisibles
        │   ├── CarCabinBuilder.cs              habitacles (DÉSACTIVÉS pour la démo, voir §8)
        │   ├── Interiors/                      intérieurs des magasins : InteriorPlan → Designer
        │   │                                   (Commerces, Loisirs, Meubles, Mobilier,
        │   │                                   Equipements, Rue) → Mesher → Realizer ; Textures
        │   ├── CorpsImporter.cs, CorpsAvatarBuilder, HairProfiles   corps .bytes → SkinnedMesh
        │   ├── FighterBuilder.cs               construit un combattant (joueur ou PNJ)
        │   ├── MocapLibraryBuilder.cs          range les clips FS par attaque
        │   ├── UrpSetup.cs, RenderDiagnostic   menus 0 et 0b
        │   ├── SandboxSceneBuilder, PrologueSceneBuilder, ArenaBuilder, NightStreetBuilder,
        │   │   ClubInteriorBuilder, HouseBuilder, ParkingBuilder, ShopInteriorBuilder
        │   └── SerializedWiring, EditorBuildUtility, ProjectSetupValidator, BuildMaterials,
        │       ProceduralMeshFactory, NightMaterialFactory, NightMeshFactory, NeonTextBuilder …
        └── Scripts/                            tout ce qui TOURNE en jeu
            ├── Core/      entrées (IInputProvider, InputBindings, KeyRemap), GameSettings,
            │              SoundBank (sons remplaçables), ChannelSource (volumes par canal)
            ├── Player/    PlayerMotor, PlayerLook (+ Nudge = visée douce), PlayerCombat
            │              (tampon d'entrées, enchaînements), PlayerInputReader, CombatPresence,
            │              PlayerCondition (faim, blessures), PlayerWardrobe, PlayerUpgrades …
            ├── Combat/    AttackData, AttackExecutor, Combatant, Hitbox/Hurtbox, GuardSystem,
            │              DodgeSystem, KnockdownSystem, StaminaSystem, StunMeter, AimResolver …
            ├── Enemy/     EnemyBrain (IA), EnemyMotor, EnemyAvatarDriver
            ├── View/      corps et caméra : IkLimb, HandRig, MocapDriver, MocapArms,
            │              BodyImpactPhysics, DeathRagdoll, BruiseSystem, HairBuilder,
            │              TimeOfDay, GraphicsDirector, UrpBridge, PlanarReflection …
            ├── Feedback/  CameraPunch, CameraShake, HitStop, ImpactAudio, ImpactEffects …
            ├── World/     la ville : DrivableCar, CarAudio, CarCockpit, PlayerDriving,
            │              TrafficCar/TrafficDriver (+ Traffic/TrafficTrack, TrafficFlow,
            │              TrafficSystem), Passerby, MocapWalker, CarThief, SwingDoor,
            │              SlidingDoor, MapStreamer, Weather, WorldClock, OpenWorldDirector,
            │              OpenWorldStory (+ Saga/ actes 1 à 5), Police/, Shops/ …
            ├── Story/     StoryDirector, StoryBeat, FightIntro, sous-titres, Saga/StoryCast
            ├── Phone/     PhoneOS (applis), PhoneCamera, PhoneGallery, PhotoArchive
            ├── UI/        UiTheme (charte unique), CombatHud, GameMenu, SettingsPage,
            │              ShopScreen, ComputerScreen, LoadingScreen, MapIcons …
            └── Sandbox/   SandboxMenu (Tab), DebugCheats (triche), WaveDirector …
```

Principe d'ensemble (détaillé dans `Docs/ARCHITECTURE.md` §1-§2) : **un seul moteur de combat**
pour le joueur et les PNJ (l'IA appuie sur les mêmes « boutons » que le joueur), les données de
coups sont des assets (`AttackData`), l'affichage écoute des événements, et **tout le décor est
généré par code** (éditeur) plutôt que posé à la main.

## 5. Le rendu URP (« le truc URP »)

Schedule 1 est rendu avec **URP** (Universal Render Pipeline). Pour avoir exactement ses
matériaux et sa lumière, le jeu passe aussi sous URP. Détail : README « Le rendu de Schedule 1 »
et `Docs/ARCHITECTURE.md` §32.

**Installation sur une machine (une seule fois)** :
1. Menu `Uber Bagarre → 0 - Passer sous URP`. `Editor/UrpSetup.cs` installe le paquet
   (`Client.Add`), attend la recompilation, puis crée le pipeline **par réflexion** dans
   `Assets/UberBagarre/Settings/Rendu` (ignoré par git) : rendu **Forward** (le Forward+
   faisait clignoter des surfaces en blanc sur URP 17.0), ombres douces 4 cascades à 150 m,
   SSAO, HDR, espace **linéaire** ; convertit les matériaux Standard/Legacy en URP Lit ;
   terrain sur `TerrainLit` ; réimporte nos shaders (sinon ils restent roses).
2. Unity fermé : installer le **correctif URP de la carte** (`CorrectifURP.zip`, en 22 volumes,
   ou refait par `Tools/schedule1/python/correctif_urp.py`) : remet les matériaux d'origine de
   Schedule 1 (993 URP Lit, 5 Complex Lit, 6 Simple Lit, 142 triplanaires sur notre shader).
3. Rouvrir Unity, relancer **3b**.

**En jeu** : `Scripts/View/UrpBridge.cs` ne fait rien hors URP ; sous URP il crée un volume de
post-traitement global (tonemapping neutre, bloom, étalonnage, vignette) piloté par
`GraphicsDirector`, ajoute les données de caméra (FXAA/SMAA/TAA), coupe `UberPostProcess` et
`PlanarReflection` (versions « rendu intégré »). `TimeOfDay` reprend la lumière de Schedule 1
(soleil 2,76, ombres 0,95, ambiante trois couleurs, brume linéaire, reflets 25 %).

**Shaders** : chaque shader éclairé a **deux SubShaders** : un URP (gardé par
`PackageRequirements { "com.unity.render-pipelines.universal" }`, éclairé par `UberShade` dans
`UberUrp.hlsl` qui appelle `UniversalFragmentPBR`, passes ombre/profondeur/normales communes dans
`UberUrpPasses.hlsl`, compatibles SRP Batcher, mots-clés `_FORWARD_PLUS` déclarés) puis un
« rendu intégré ». Le ciel (`UberCiel` + `UberSky.hlsl`) refait le Sky Studio de Schedule 1 et
sert aussi au reflet de l'eau.

**Limites connues** : le rendu Forward d'URP n'éclaire un objet qu'avec **8 lampes** → les
intérieurs sont découpés en cases de 4 m (`InteriorMesher`). Les assets payants de Schedule 1
(Sky Studio, Stylized Water 2, Stylized Grass, God Rays, Amplify Impostors) ne sont pas là : le
jeu en a des équivalents maison.

## 6. Les grands systèmes (où regarder)

| Domaine | Fichiers clés | Notes |
|---|---|---|
| Combat | `Player/PlayerCombat`, `Combat/AttackExecutor`, `Combat/AttackData` | Enchaînement dès `comboCancelAt`, tampon d'entrées en file, coup chargé tenu pendant l'armement (`TryPlayHeld`/`ReleaseHold`), **coup de conclusion** après 2 coups portés, visée douce (`PlayerLook.Nudge`), `HitStop.PlayStrong`, fatigue de la perte de contrôle |
| Ressenti | `Feedback/CameraPunch` (ressort d'impact raide), `HitStop`, `CameraShake`, `ImpactAudio` | HitStop court (≈32 ms à 25 %) avec temps mort, sinon le jeu devient pâteux |
| Animations | `View/MocapDriver` (PNJ, graphe Playables), `View/MocapArms` (bras du joueur) | Clips de **FS Melee Combat System** (code FS non utilisé : notre combat décide, les clips jouent) |
| Physique des coups | `View/BodyImpactPhysics`, `View/DeathRagdoll` | Ressorts par os ; vrai ragdoll seulement à la mort |
| Corps | `Editor/CorpsImporter`, `Art/Models/Corps` | Format UBCORPS2 ; slots Peau, Visage, Yeux, Cils, Haut, Jean, Chaussures, Bandes ; méta JSON ; repli sur `Peau_<sil>_Albedo` / `Yeux_Marron` pour les anciens corps |
| Cheveux | `View/HairBuilder`, `Editor/HairProfiles` | 11 coupes, 5 barbes, posées sur le crâne mesuré |
| Voitures | `World/DrivableCar`, `CarAudio`, `PlayerDriving`, `Editor/CityVehicles` | Ressorts à fréquence propre, anti-roulis, pneus à dérive, 6 rapports, anti-tonneau |
| Circulation | `World/Traffic/TrafficTrack`, `TrafficFlow`, `TrafficSystem`, `World/TrafficDriver`, `TrafficCar` | Boucle préparée, suivi IDM, carrefours réservés, volant au contrôleur de Stanley, simulation en **bulle** autour du joueur |
| Passants | `World/Passerby`, `World/MocapWalker` | Chaque pas testé contre le décor, évitement social, activités |
| Portes | `World/SwingDoor`, `SlidingDoor`, `Editor/CityPreparation` | Battants doubles appariés, doublons de LOD éteints |
| Magasins | `World/Shops/*`, `UI/ShopScreen`, `Editor/Interiors/*` | Comptoir façon GTA, horaires, un intérieur par métier, rue en décor derrière la vitrine |
| Ville | `World/MapStreamer`, `OpenWorldDirector`, `Weather`, `WorldClock` | La carte vit dans sa propre scène chargée par-dessus `MondeOuvert` |
| Histoire | `World/OpenWorldStory` + `World/Saga/*`, `Story/Saga/StoryCast` | Fil d'étapes gardé dans la sauvegarde ; voir `Docs/HISTOIRE.md` |
| Police | `World/Police/*`, `World/Crimes` | Témoins, étoiles, recherche, garde à vue, casier |
| Téléphone / PC | `Phone/PhoneOS`, `UI/ComputerScreen` | Applis, appareil photo, banque, navigateur |
| Interface | `UI/UiTheme`, `Core/GameSettings`, `UI/SettingsPage`, `Core/KeyRemap` | Une seule charte, réglages centralisés, touches réassignables |
| Sons | `Core/SoundBank`, `Docs/SONS.md` | Tout son est remplaçable par un fichier dans `Resources/Sons/<Catégorie>/<nom>.wav` |

## 7. Vérifier sans Unity

Il n'y a **pas d'éditeur Unity** dans l'environnement de Claude : rien ne peut être lancé ici.
Avant chaque push, on vérifiait la compilation C# **hors ligne** : tous les scripts compilés
contre de faux types Unity (« stubs ») avec un script `build.sh` (sortie propre = seulement la
ligne `--- editor`). **Ce banc était dans le dossier temporaire de session et n'existe plus** :
à la prochaine session qui modifie du C#, le reconstruire (compilateur `mcs`/`csc` + stubs
minimaux de `UnityEngine`/`UnityEditor`) et, idéalement, le ranger dans `Tools/` pour qu'il
survive.

Autres bancs hors Unity déjà dans le dépôt : `Tools/shaders_urp` (shaders URP via glslang),
`Tools/uma/apercu.py` (rendu Cycles des corps exportés, relus exactement comme `CorpsImporter`),
simulations de circulation décrites dans ARCHITECTURE §33.2.

Le cache UMA (`Tools/uma/.cache`, index `[chemin, guid]`) et le paquet
`UMA2_For_UMA3_*.unitypackage` ne sont pas dans git : il faut que le propriétaire fournisse le
paquet UMA (gratuit sur l'Asset Store) pour relancer `corps_uma.py`.

## 8. État actuel (au commit `f6725a6`, 11/10/2026)

**Correctifs « démo » faits à la va-vite pour montrer un build** (à reprendre proprement plus tard) :
- `Editor/CityVehicles.cs` : l'habitacle généré par `CarCabinBuilder` est **détruit juste après
  sa construction**, et le `CarCockpit` retiré (il dépassait de la carrosserie). Commit `2fa3312`.
- `Editor/CityVehicles.cs` `Strip()` : supprime les rendus non-LOD0 des `LODGroup` et ceux nommés
  « LOD » (une voiture plus petite apparaissait dans chaque voiture). Commit `5052411`.
- Klaxon des PNJ coupé : `TrafficDriver.Honk()` commence par `_car.Honk(false); return;` (ancien
  corps gardé sous `#pragma warning disable CS0162`) et `TrafficCar.cs` teste
  `if (false && player && …)`. Commit `f6725a6`.

**Chantier en cours : refonte des personnages sur UMA 2** (commit `94558e5`) :
- Seul **Athlete** (le joueur) est exporté depuis UMA ; **Costaud, Sec, Colosse** gardent leurs
  anciens corps MakeHuman ; **Femme** et **Sportive** n'existent pas encore côté jeu.
- Reste à faire : traînées sur le visage, bords en escalier du tee-shirt, texture des chaussures ;
  exporter les autres silhouettes ; cheveux et barbes sur les corps UMA ; actrices et foules
  féminines ; doc ARCHITECTURE.
- Problèmes déjà réglés dans `umalib.py` : niveaux de détail UMA empilés (ne garder que
  `lodRanges[0]`), chemins relatifs UnityPy, overlays anciens en YAML, os LeftChest.

**Le propriétaire a demandé (14/10) de NE PAS reprendre ces chantiers** : il veut d'abord
« changer plein de choses ». Attendre sa nouvelle liste avant de toucher au code.

**Demande précédente encore ouverte** (retour de test, texte d'origine résumé point par point) :
1. Optimisation : IA de marche et de voiture uniquement autour du joueur — *fait (bulle) au
   commit `32683bb`, à revérifier*.
2. PNJ qui ne font rien ; voitures qui apparaissent les unes dans les autres (au crochetage ou
   sans rien faire) — *en partie*.
3. Voitures des IA trop grandes ; **IA de conduite stupide** — *gabarits réduits, IA refaite ;
   le propriétaire la trouve encore mauvaise*.
4. Suspensions trop molles, presque des tonneaux en virage ; conduite trop arcade, vouloir des
   sensations GTA — *nouveau modèle physique au commit `32683bb`*.
5. **Bruitages** qui « font IA » (surtout le **klaxon** « jeu de 1980 ») — *banque de sons
   remplaçables (`Docs/SONS.md`) : il faut de vrais fichiers ; klaxon PNJ coupé*.
6. **Personnages horribles : tout changer intégralement** — *refonte UMA en cours*.
7. Intérieurs pour chaque voiture — *faits puis désactivés (dépassaient)*.
8. **Portes en double** (un battant s'ouvre, un doublon reste fermé) — *corrigé au `32683bb`,
   à revérifier en jeu*.
9. Grosse révision du combat, ressenti GTA « snappy » — *fait au `94558e5`*.
10. Dans les magasins, une fois à l'intérieur, on voit la mer par la vitrine — *rue en décor
    derrière la vitrine au `32683bb`*.

## 9. Pièges connus

- **Fichier coupé par un copier-coller partiel** : erreurs du type
  `TrafficCar.cs(100,54): error CS1002: ; expected` / `CS1513: } expected` à une ligne où on n'a
  rien modifié → le fichier local est abîmé. Remède : remplacer **tout** le fichier par la version
  GitHub (Raw), ou faire un Pull. Dernier état connu chez le propriétaire (14/10) :
  `TrafficCar.cs` et `TrafficDriver.cs` abîmés ainsi ; les versions du dépôt compilent.
- **Erreur CS1519 dans CityVehicles.cs** : un bout de code collé au mauvais endroit (hors d'une
  méthode). Même remède.
- `[Worker0] Import Error Code:(4) Build asset version error … modification time` : un fichier a
  été modifié pendant une compilation. Sans gravité : clic droit sur `Assets` → **Reimport**, ou
  redémarrer Unity.
- **Avertissements sans gravité** (à ignorer, le dire au propriétaire) : « Convex Mesh … 256
  polygons », triangles de plus de 500 unités, « Renderer registered with more than one
  LODGroup », « Corps_*.json introuvable » (anciens corps), « Avatar main object name » , SSAO,
  matériaux `Computer_LOD2`.
- **Données figées** : un asset (touches, coups) garde les valeurs du jour de sa création ;
  changer une valeur par défaut dans le code ne le met pas à jour → menus 4 et 6, ou
  `dataVersion` (voir ARCHITECTURE §14).
- **« Le système existe mais ne produit rien d'observable »** : défaut récurrent du projet
  (ARCHITECTURE §14-§15). Toujours vérifier que le dernier maillon (ce que l'œil voit ou l'oreille
  entend) est juste, pas seulement que le code tourne. Un réglage exposé mais non lu est un
  mensonge de l'interface.
- Les composants posés dans une scène gardent leurs anciennes valeurs : les nouveaux réglages par
  défaut sont **écrits par le générateur de scène** → relancer 3b.

## 10. Journal des modifications, demande par demande

Chaque passe correspond à un retour du propriétaire. Les citations entre guillemets sont ses mots.
Le détail complet de chaque commit est dans `git log` (messages longs et explicatifs).

**Phase 1 – prototype FPS (11/09)** — dépôt vide au départ.
`c6a4594` squelette Unity, déplacement, visée, spawn, entrées indépendantes du backend ·
`7dcd463` version Unity et dossier à ouvrir · `3e7f7dd` sprint et head bob (« la marche paraît
exagérée, le sprint manque ») · `7424dc3` menu « réinitialiser les touches ».

**Phase 2-4 – mains, corps, coups (11/09)**.
`c6f70c9` mains FPS en IK deux os, garde, respiration · `cd7e57a` corps complet, mains à 5 doigts,
cycle de marche, accroupi, glissade (« c'est juste les mains qui vont de haut en bas », « je veux
que les mains ne soient pas un cube », « ne fais pas des animations cheat ») · `203d83c` les
coups (`AttackData`, `AttackExecutor`, hitbox par segment), géométrie des mains.

**Phase 5-10 – combat complet (12/09)**.
`62c978d` IK indépendante du squelette, branchement d'un modèle 3D · `6c06bdd` vraies mains
maillées, ennemi complet, vie, endurance, esquive, retours · `82feaee` portées corrigées (« on ne
peut pas toucher l'ennemi », « il fixe bêtement »), UI stylisée, arène en ruelle · `0eed2b2` et
`529ced1` panne des données d'attaque (« on ne peut toujours pas taper ») → chargement de secours
depuis `Resources` · `32a532e` zones de frappe, chute, garde et parade, coups de pied · `fbb94c6`
vraie animation de relevé, zones lisibles · `8b52d4c` bleus visibles, la visée décide la zone,
ragdoll à la mort, « pas assez snappy » → annulation d'enchaînement, sons refaits (« les bruits
sont trop goofy »), têtes refaites (« les têtes sont horribles »), menu Tab de triche · `9ca82b7`
matières texturées, rappel des commandes · `7a6ebb9` détection des canaux de maillage ·
`262330f` nervosité : HitStop trop long, touches jetées → tampon d'entrée, endurance ·
`cf8edad` charge, coups contextuels, riposte, étourdissement, vagues, caméra d'observation (F3).

**Rendu nuit et prologue (15-25/09)**.
`5f9613e` rue de nuit, néons, bitume mouillé, chaîne de post-traitement maison · `f864e1c`
prologue jouable (planque, appel, appli, groupe, bagarre, photo) · `7a7f16f` physique des coups
par os · `68362f1` coup de tête, bousculade, objets à lancer, chapitre 1 (frères Kovac) ·
`564b613` correctif CS0579 · `96b2450` souris sous Linux, ZQSD · `0ee46bf` rendu différé, PBR,
lumière volumétrique, chapitre 2 (Vertigo) · `968885d` vraies mains, anti-spam, épuisement, TAA ·
`d1d79d6` voix, ambiances, caméra stable, menu de triche · `983266e` téléphone OS, appareil photo,
intro de combat, foule · `ce971bc` hors combat : mains au repos · `4fc449c` face-à-face avant
chaque bagarre · `bb94665` courrier animé · `ed5a213` maison détaillée · `88e877c` bleus peints
dans la peau · `8ab741d` écran titre, pause · `57cc23c` chute vue de l'intérieur, babillage ·
`af32dc5` combat réécrit et **corps MakeHuman** (`Tools/corps`) · `a307028` ombre du joueur ·
`9871074`, `e4e2686` débardeur et veste.

**FS Melee et monde ouvert (25-27/09)**.
`e3f0a78` *(propriétaire)* dépôt du paquet FS Melee · `65a7659` adversaires en animations
capturées · `63fb5be` bras du joueur en gestes capturés, monde ouvert procédural · `d4e270e`
voitures conduisibles, lotissements · `90f2baf` outil d'analyse de la carte exportée (Schedule 1) ·
`e54288b`, `7648573` *(propriétaire)* fichiers de test.

**La carte Schedule 1 (27-29/09)**.
`185141c` monde ouvert sur la carte : courses, réputation, vestiaire, ordinateur · `96ce97b`
histoire dans la ville, logements meublés · `6702434` outils de conversion de la carte ·
`e80fa87` modules Unity manquants · `f685040` ville visible dans l'éditeur · `274a305`,
`01f765b` espace linéaire automatique · `579d176` face-à-face, appli installée par le joueur,
navigateur · `444a910` portes qui s'ouvrent, voitures de la carte, magasins, jour/nuit ·
`2b2c930` quartiers fermés rouverts, voitures en double, optimisation · `5a0b448` banque ·
`98f9691` Vertigo en sous-sol · `0bb8ef9` écran de chargement, police Roboto · `d9a9c35` météo ·
`3b2f4e5` frapper au sol, bonus · `ad05e83` faim et blessures · `fcb5162` vol de voitures
(crochetage) · `a7ebda2` provoquer les passants · `1c32eea` police · `9e65ceb`, `4bce336`
histoire complète « Hyland cogne » · `27bf5ec` carte façon GTA, premiers pas, rééquilibrage.

**Passage sous URP (29/09-08/10)**.
`b785990` menu 0, shaders à deux versions, ciel de Schedule 1, correctif des matériaux ·
`b344023` README du correctif en volumes · `b87a56c` URP 17.0 : `_FORWARD_PLUS`, réimport des
shaders, menu 0b de diagnostic · `8286ff6` rendu en Forward (clignotements blancs en Forward+),
distance d'affichage, arbres hors des maisons · `bec1752` vrais nuages, mer vivante · `c1d4b95`
carte animée · `c5fb93b` magasins vivants (caméra sur le vendeur), barbier, cheveux et barbes.

**Ville qui tient debout (09-10/10)** — retour : « HUD arcade, menus incomplets, voitures qui
s'emboutissent, passants qui traversent les murs, magasins vides ».
`9fab4ac` HUD sobre, réglages centralisés · `de67f5b` menus à onglets, touches réassignables ·
`93d429c` IA de conduite refaite · `55ffe64` passants · `ea068f6` intérieurs des magasins ·
`1733bfe` vêtements et visages · `8cb2e5d` `Assets/Schedule1` ignoré.

**Grand retour « tout est à revoir » (10-11/10)** — voir §8, liste des 10 points.
`32683bb` conduite façon GTA, habitacles, circulation en bulle, portes doubles, rue derrière les
vitrines, banque de sons · `94558e5` combat façon GTA + début de la refonte UMA 2.

**Urgence démo (11/10)** — « je dois montrer le build à quelqu'un ».
`5052411` plus de petite voiture LOD dans la voiture · `2fa3312` habitacles désactivés ·
`f6725a6` plus de klaxon des PNJ (« ça me pète les c… »).

**14/10** : ce fichier `CLAUDE.md`. Le propriétaire prépare une nouvelle liste de changements.

> Pour toute nouvelle session : ajouter ici une ligne par commit poussé (hash, date, ce qui
> change, la demande qui l'a provoqué), et mettre à jour §8.
