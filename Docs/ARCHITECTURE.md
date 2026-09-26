# Architecture — prototype de combat Über Bagarre

Ce document explique **pourquoi** le code est découpé comme il l'est, et **où brancher** chaque
extension future. Il est mis à jour à chaque phase.

---

## 1. L'idée directrice : un seul moteur de combat pour tout le monde

Le joueur et l'ennemi ne sont pas deux systèmes parallèles. Tout ce qui se bat est un **`Combatant`**.

```
      QUI DÉCIDE ?                          Joueur                  Ennemi
                                    PlayerCombatInput            EnemyBrain / EnemyAI
                                              \                      /
                                               \                    /
                                        TryAttack(AttackData) / TryDodge(direction)
                                                       |
      COMBAT (partagé, identique)       ┌──────────────▼──────────────┐
                                        │  Combatant                  │
                                        │   CombatantStateMachine     │
                                        │   AttackExecutor            │
                                        │   HealthSystem              │
                                        │   StaminaSystem             │
                                        │   CombatantStats            │
                                        │   DodgeSystem               │
                                        └──────────────┬──────────────┘
                                                       |
      DÉTECTION                          Hitbox (fenêtre d'impact) → Hurtbox (zone touchée)
                                                       |
      CONSÉQUENCE                        DamageInfo → HealthSystem → événements
                                                       |
      RESTITUTION                        ICombatAnimator · CameraShake · Vignette · HitStop · Audio · HUD
```

**Conséquence pratique :** quand tu demanderas « ajoute un coup de pied », il y aura un nouvel
asset `AttackData` + une animation, et il fonctionnera **pour le joueur comme pour l'ennemi**,
sans toucher au moteur.

---

## 2. Règles de conception appliquées

1. **Données plutôt que code.** Une attaque, des touches, des stats sont des ScriptableObjects.
   Ajouter un coup ne doit pas demander de recompiler une logique.
2. **Un composant = une responsabilité.** `PlayerMotor` déplace. `PlayerLook` vise. Aucun des deux
   ne sait que le combat existe.
3. **Communication par événements et interfaces**, pas par références croisées. `HealthSystem`
   émet « j'ai pris des dégâts » ; la vignette, le HUD, le camera shake et la réaction d'animation
   écoutent **sans que `HealthSystem` les connaisse**.
4. **Pas de singleton pour le gameplay.** Les références sont câblées dans la scène (par le
   générateur). Ça évite les dépendances invisibles et ça rend deux combattants, ou plus tard dix,
   possibles sans rien changer.
5. **Un nœud de transform = un effet.** Le rig caméra empile les effets au lieu de les additionner
   dans un même script, donc ils ne s'écrasent jamais entre eux.

---

## 3. Rig caméra du joueur

```
Player            lacet (gauche/droite), déplacement, lecture des entrées
 └─ Head          tangage (haut/bas) — hauteur des yeux : 1.62 m
     └─ CameraBob        oscillation de marche          (HeadBob)
         └─ CameraShake      secousses d'impact         (phase 7)
             └─ CameraPunch      recul du coup porté    (phase 5)
                 └─ MainCamera   near clip 0.04 (sinon les mains sont coupées)
                     └─ HandsRig     mains FPS          (phase 2)
```

Pourquoi séparer le lacet (sur `Player`) du tangage (sur `Head`) : le déplacement utilise l'axe
avant du corps. Si le tangage était sur le même transform, regarder le sol ferait marcher le joueur
dans le sol.

---

## 4. Couche d'input

```
InputBindings (ScriptableObject)   ← les touches, éditables, un seul endroit
        │
IInputProvider                     ← abstraction
  ├─ LegacyInputProvider           #if ENABLE_LEGACY_INPUT_MANAGER
  ├─ NewInputSystemProvider        #if ENABLE_INPUT_SYSTEM
  └─ NullInputProvider             secours : aucun backend actif
        │
PlayerInputReader (MonoBehaviour)  ← échantillonne 1×/frame, expose des propriétés
        │
PlayerMotor / PlayerLook / (plus tard) PlayerCombatInput
```

Pourquoi cette abstraction plutôt que `Input.GetKey` directement :

- le projet marche quel que soit le réglage *Active Input Handling* d'Unity ;
- les touches sont des données, donc rebindables sans toucher au code ;
- on pourra injecter un faux provider pour **rejouer une séquence d'entrées** et tester
  les esquives au frame près, sans joueur humain.

`PlayerInputReader` a un `DefaultExecutionOrder(-100)` : il est mis à jour avant tous ses lecteurs,
donc tout le monde lit la même valeur dans la même frame.

---

## 5. Spawn

`SpawnPoint` est une pure donnée de niveau (position + orientation + gizmo).
`SpawnDirector` place les combattants au `Start`, soit en instanciant un prefab, soit en
téléportant un objet déjà présent dans la scène.

Le directeur ne connaît **ni le joueur ni l'ennemi** : il notifie l'interface `ISpawnReceiver`.
C'est indispensable ici pour deux raisons concrètes :

- un `CharacterController` actif **écrase** une écriture directe de `transform.position` →
  `PlayerMotor.OnSpawned` le désactive le temps de la téléportation ;
- `PlayerLook` garde ses angles de visée en interne et les réapplique chaque frame → sans
  `OnSpawned`, il annulerait l'orientation du spawn dès la frame suivante.

Déplacer le spawn = déplacer l'objet `PlayerSpawn` dans la scène. Rien d'autre.

---

## 6. Outils éditeur (`Assets/UberBagarre/Editor`)

| Outil | Rôle |
|---|---|
| `ProjectSetupValidator` | Diagnostic : version, pipeline, shader, backend d'input (+ correction en un clic) |
| `SandboxSceneBuilder` | Génère la scène de test complète |
| `EditorBuildUtility` | Détection du render pipeline, matériaux, textures, primitives |
| `SerializedWiring` | Renseigne les champs `[SerializeField] private` comme le ferait un glisser-déposer |

**Pourquoi générer la scène par code** : un fichier `.unity` est un graphe d'objets liés par GUID.
Écrit à la main hors d'Unity, il casse au moindre détail. Généré par Unity lui-même, il est
forcément valide dans ta version et ton pipeline — et le code de construction documente la scène.

**Pourquoi `SerializedWiring`** : les champs restent `private [SerializeField]` (le runtime ne peut
pas les écraser n'importe quand), mais l'outil éditeur peut les câbler via `SerializedObject`,
exactement comme un glisser-déposer manuel.

---

## 7. Où brancher les extensions futures

| Tu voudras… | Ce qu'il faudra toucher |
|---|---|
| Un nouveau coup (coude, pied, coup de tête) | Nouvel asset `AttackData` + variantes d'animation. Aucun code. |
| Une stat qui augmente les dégâts | `CombatantStats` + la formule centralisée de calcul de dégâts |
| Une parade / un contre | Nouvel état dans `CombatantStateMachine` + fenêtre de timing dans les données |
| Un combo | Un `ComboResolver` qui enchaîne des `AttackData` ; `AttackExecutor` ne change pas |
| Un ennemi qui bloque | Nouveau comportement d'IA ; le moteur de combat reste identique |
| De vraies animations | Nouvelle implémentation de `ICombatAnimator`. Le combat n'y touche pas. |
| Un vrai HUD | Nouvelle implémentation de l'affichage ; les systèmes émettent déjà les événements |
| Plusieurs ennemis simultanés | Rien de spécial : aucun singleton de gameplay |

---

## 7 bis. Les mains première personne (phase 2)

```
FirstPersonHands  (sur HandsRig, enfant de la caméra)
   │  compose la pose des deux poings, en espace caméra
   │
   │  1. POSE DE BASE      garde normale → garde serrée → course (mélange par poids)
   │  2. COUCHES ADDITIVES respiration + bruit organique + inertie de visée + déplacement
   │  3. LISSAGE           donne du poids : une main ne se téléporte jamais
   │  4. COUCHE D'ATTAQUE  appliquée APRÈS le lissage (phase 3)
   ▼
FirstPersonArm  ×2   reçoit « le poing va ici, orienté comme ça »
   ▼
ArmIkSolver          en déduit épaule, bras et coude
   ▼
Transforms d'os      UpperArm → Forearm → Fist
```

**Pourquoi une IK à deux os plutôt que des rotations posées à la main.** Le cahier des charges
demande que l'épaule, le bras, l'avant-bras et la main bougent ensemble. Sans IK, chaque image-clé
de chaque variante de chaque attaque devrait spécifier un angle d'épaule *et* un angle de coude
cohérents entre eux — infaisable à régler à la main. Avec l'IK, une image-clé se résume à
*« le poing est là »*, et le triangle épaule/coude/poing se résout tout seul par la loi des cosinus :
pas d'itération, pas d'instabilité, même résultat à chaque frame.

C'est aussi ce qui rend les attaques de la phase 3 réglables dans l'Inspector par quelqu'un qui
n'est pas animateur.

**Pourquoi l'attaque est appliquée après le lissage.** Le lissage exponentiel donne de la masse aux
mains au repos. Appliqué à un jab, il en écraserait la vivacité. L'attaque impose donc sa position
avec son propre timing, et seul son poids de mélange gère l'entrée et la sortie du coup.

**Pourquoi `PlayerHandsDriver` est un composant à part.** `FirstPersonHands` ne lit aucune touche :
il est piloté de l'extérieur. L'ennemi réutilisera le même système de bras, piloté par son IA.
Si le composant d'affichage lisait le clavier, il serait inutilisable pour tout personnage non joueur.

**Limite connue.** Les bras peuvent traverser un mur si le joueur se colle dessus. La correction
habituelle (une seconde caméra dédiée aux mains, rendue par-dessus) sera ajoutée si ça devient
gênant — ce n'est pas une contrainte d'architecture.

---

## 8. Journal des décisions

### Phase 1
- **Pas d'`asmdef`** : les scripts vivent dans `Assembly-CSharp`, qui référence automatiquement tous
  les packages. Élimine la classe d'erreurs « type introuvable » dans un projet neuf.
  À reconsidérer quand les temps de compilation deviendront gênants.
- **`Packages/manifest.json` minimal** (physics, audio, imgui, animation, particles, ui…) :
  import rapide, et aucune dépendance au package Input System ou à TextMeshPro.
- **CharacterController plutôt que Rigidbody** pour le joueur : pas de tuning physique, pas de
  glissade involontaire, comportement déterministe — ce qu'on veut pour juger des timings de combat.
- **Sol en damier généré par code** : sur un sol uni, on ne perçoit pas son propre déplacement,
  donc impossible de régler vitesse et inertie à l'œil.

### Phase 1 — calibrage après premier test en jeu
- Head bob réduit d'environ 37 % (la marche était jugée légèrement exagérée), avec un renfort
  interpolé au sprint pour que les deux allures restent distinctes.
- Marche 3.4 → 3.1 m/s : à 3.4 m/s le joueur trottinait déjà, ce qui rendait le head bob plus
  visible qu'il ne devrait et ne laissait pas de place au sprint.
- **Sprint sur Maj gauche**, ×1.75, bloqué en marche arrière et en pas chassé
  (`_sprintRequiresForwardInput`) : sprinter de côté casserait la lisibilité des déplacements de combat.
- **Conflit de touche à trancher en phase 8** : l'esquive était prévue sur Maj gauche. Elle passe
  provisoirement sur Alt gauche. Trois pistes le moment venu : esquive sur double-tap de direction,
  sprint désactivé en garde (Maj redevient libre pour l'esquive en combat), ou touche dédiée.
- `InputBindings.ResetToDefaults()` (menu contextuel de l'Inspector) : un asset conserve les valeurs
  du jour de sa création, donc changer une valeur par défaut dans le code ne met jamais à jour un
  asset déjà existant. Sans ce bouton, chaque changement de touche par défaut devrait être répercuté
  à la main.

### Phase 2 — mains FPS
- **IK analytique à deux os** (`ArmIkSolver`) plutôt que des rotations d'os posées à la main :
  seule façon de rendre les attaques de la phase 3 configurables en données.
- **Poses en espace caméra** (`HandPose`) : un type unique pour la garde, la respiration et,
  demain, chaque image-clé d'attaque. Interpolable, additionnable, sérialisable.
- **Trois poses de base** (garde, garde serrée, course) mélangées par des poids 0..1 plutôt
  qu'une machine à états : ajouter une pose future (bloquer, encaisser, être étourdi) ne
  demandera aucun changement de structure.
- **Bruit de Perlin** pour le micro-mouvement au repos : un sinus se répère à l'œil au bout de
  quelques secondes, et les mains « respirent » alors comme un métronome.
- **Ombres portées désactivées sur les bras** : deux bras sans corps projetteraient une ombre
  qui trahit immédiatement l'illusion. Ils continuent de recevoir les ombres.
- **Primitives Unity** pour la géométrie : le projet n'a aucun modèle 3D. Seuls les transforms
  d'os comptent pour l'animation, donc les remplacer par un vrai modèle ne touchera aucun script.

### Phase 2b — corps complet, mains articulées, locomotion
- **Un vrai squelette** (`BodyRig`) plutôt que deux bras flottants attachés à la caméra.
  Déclencheur : « l'animation de marche est bizarre, c'est juste les mains qui montent et
  descendent ». Le vrai correctif n'était pas d'ajuster une courbe, mais que les bras soient
  portés par un torse qui marche, respire et contre-tourne.
- **Le corps est enfant de la racine joueur, pas de la caméra** : il suit le lacet mais pas le
  tangage. C'est ce qui permet de baisser les yeux et de voir son propre torse et ses jambes,
  au lieu d'un corps qui bascule avec le regard.
- **`HandsAimAnchor`** résout la tension entre les deux : les épaules sont sur le buste, mais
  les poings visent dans un repère qui ne reprend qu'une fraction du tangage. À 100 %, les
  bras se tordraient en regardant ses pieds ; à 0 %, la garde sortirait de l'écran en levant
  les yeux.
- **Pieds réellement plantés au sol.** Un pied en appui garde sa position MONDE pendant toute
  sa phase d'appui, le corps passe au-dessus, puis il décolle et se repose plus loin. C'est la
  seule façon d'éviter le patinage. Bassin, buste et balancement des bras sont dérivés du même
  cycle, donc tout le corps reste synchrone par construction.
- **La foulée se mesure par cycle complet (deux pas)**, pas par pas. L'erreur inverse donne une
  cadence double, effet « petits pas pressés ».
- **La cible d'IK des jambes est la cheville, pas la semelle** (`_ankleHeight`). Sans ce décalage
  la jambe se tend à l'extrême et le pied s'enfonce dans le sol.
- **Mains à 5 doigts de 3 phalanges**, de longueurs différentes et à fermeture décalée
  (`closeDelay`) : le poing est le résultat d'une vraie fermeture, pas un cube. Utile ensuite
  pour valider qu'un impact a lieu poing fermé, ou ouvrir la main pour une parade.
- **`IkLimb` générique** : bras et jambes sont le même problème géométrique. Un seul solveur,
  un seul composant, deux usages.
- **Accroupi et glissade dans `PlayerMotor`** : la hauteur de capsule ET la hauteur des yeux
  sont le même fait physique, donc le moteur possède les deux. Le relevé vérifie qu'il y a la
  place au-dessus, en ignorant les colliders du joueur lui-même plutôt qu'en imposant un calque
  au projet.

### Phase 3/4 — les coups
- **Correction de géométrie de main** : la paume faisait 3,4 cm d'épaisseur pour 9 cm de long,
  soit une planche ; les doigts repliés pendaient dessous et l'ensemble se lisait comme un pied.
  Un poing est un bloc presque aussi épais que large. Ajout d'une crête d'articulations, qui
  est aussi la surface de frappe.
- **Poings poussés de 27 à 34 cm de l'œil** : à courte distance la perspective déforme tout.
- **Bruit de repos divisé par trois** : le micro-mouvement « organique » se lisait comme un
  tremblement.
- **`AttackData` décrit tout un coup en données** : durée, fenêtre d'impact, dégâts, secousse,
  et une liste de poses-clés par variante. Ajouter un coude ou un coup de pied ne demandera
  aucune ligne de code.
- **Poses écrites pour la main droite uniquement, mirrorées à gauche.** Deux fois moins de
  réglages, et un crochet gauche reste le symétrique exact du droit par construction.
- **La rotation du buste fait partie des données du coup.** C'est de là que vient le poids :
  un direct tourne le corps de 12°, un crochet de 21°. La locomotion reste seule à écrire sur
  la colonne et intègre ce que le combat lui demande (`CombatBodyEuler`), donc les deux systèmes
  ne se marchent jamais dessus.
- **La hitbox teste le SEGMENT parcouru depuis la frame précédente**, pas seulement la position
  finale : un poing rapide franchit sa propre largeur en une frame et passerait au travers.
- **Chaque cible n'est comptée qu'une fois par attaque** : sinon une fenêtre d'impact de six
  frames infligerait six fois les dégâts.
- **Recul de caméra à deux entrées séparées** (suivi de l'animation / à-coup d'impact) : les
  mélanger empêcherait d'avoir un coup qui accompagne la caméra ET un choc sec au contact.
- **Arrêt sur impact plafonné à 60 ms** : au-delà, le jeu paraît saccadé au lieu de puissant.
- **Sac de frappe** plutôt que rien : sans cible, impossible de vérifier qu'une fenêtre
  d'impact fonctionne. Pendule simple, sans Rigidbody, donc comportement identique à chaque
  test — ce qu'on attend d'un banc d'essai.

### Phase 4b — IK agnostique au squelette
- **`IkLimb` mesure lui-même l'orientation et la longueur réelles de ses os au réveil.**
  Avant, il imposait « l'axe +Z suit l'os » — convention qu'aucun rig du commerce ne respecte,
  donc aucun modèle importé n'aurait fonctionné. Il aligne désormais chaque os par
  `FromToRotation` depuis sa pose de repos : rig-agnostique, et sans accumulation d'erreur
  puisqu'on repart toujours du repos.
- **Le pôle est exprimé dans un repère explicite** (`_poleSpace`, la racine du corps) et non
  plus dans le repère de l'os parent, dont l'orientation dépend entièrement du logiciel de rig.
- **`HumanoidModelBinder`** : l'outil ne remplace pas le système d'animation, il lui donne
  d'autres os. Les composants et tout leur câblage sont conservés ; seules les références
  changent. L'axe de flexion de chaque doigt est *calculé* à partir de la géométrie réelle de
  la main (normale du dos de la main × direction du doigt), car il est impossible à deviner.
- **Les primitives sont masquées et non détruites** : un branchement raté se défait en
  recochant les Mesh Renderer.

---

## 9. Le combat complet (phases 5 à 10)

### Le moteur est réellement partagé

```
        PILOTAGE            PlayerCombat  ·  PlayerAvatarDriver     EnemyBrain  ·  EnemyAvatarDriver
                                        \                           /
                                         ── TryPlay / TryDodge ────
                                                    │
        COMBAT (identique)          Combatant · CombatantStateMachine · AttackExecutor
                                    HealthSystem · StaminaSystem · CombatantStats · DodgeSystem
                                                    │
        CORPS (identique)           BodyRig · IkLimb ×4 · HandRig ×2 · ProceduralLocomotion
                                                    │
        RESTITUTION                 FighterHands · CameraShake · CameraPunch · HitStop
                                    DamageVignette · ImpactAudio · CombatHud
```

Le générateur de scène construit le corps du joueur et celui de l'ennemi avec **le même code**
(`FighterBuilder.BuildBody`). C'est la vérification concrète de la promesse : s'il avait fallu
deux implémentations, c'est que le moteur n'aurait pas été partagé.

Seules trois choses diffèrent entre eux :
- le **repère de visée** des poings (caméra pour le joueur, ancrage sur le corps pour l'ennemi) ;
- le **pilote** (entrées contre IA) ;
- la **tête** — le joueur ne voit jamais la sienne.

### Décisions de cette étape

- **Maillages générés plutôt que primitives.** Un cube reste un cube et une capsule reste un tube
  d'épaisseur constante. Il manquait deux formes : le **segment conique à bouts arrondis** (une
  phalange est plus épaisse à la base qu'à la pointe — sans ça, des saucisses) et la **boîte
  adoucie** (un poing n'a ni arête vive ni forme sphérique). Les maillages sont unitaires et
  réutilisés par mise à l'échelle : une poignée d'assets pour tout le corps.
- **Ordre des triangles.** Générer un maillage avec l'ordre `a,c,b` produit des faces tournées
  vers l'intérieur, donc invisibles. C'est l'erreur classique et elle est silencieuse.
- **Machine à états à priorités**, pas table de transitions. Deux lignes de règle : un état
  s'impose s'il est au moins aussi prioritaire que l'actuel. Ajouter une parade ou un contre
  ne demandera que de leur donner une priorité.
- **Un coup encaissé interrompt le coup qu'on portait** (`Hit` est prioritaire sur `Attacking`).
  Sans ça, se faire toucher n'a aucune conséquence et le combat devient un échange de dégâts.
- **La fenêtre d'invulnérabilité d'esquive ne couvre pas toute l'esquive.** C'est ce qui en fait
  une compétence de timing plutôt qu'un bouton d'invincibilité.
- **La secousse de caméra suit le carré du « traumatisme ».** Proportionnelle, elle tremblerait
  en permanence dès qu'on échange des coups ; au carré, deux petits coups ne font presque rien
  et un gros se sent nettement.
- **Encaisser secoue plus fort que frapper.** Sinon on ne distingue pas les deux à l'écran.
- **Le calcul des dégâts est à un seul endroit** (`DamageCalculator`) : force de l'attaquant en
  pourcentage, défense du défenseur à rendement décroissant. Équilibrer le jeu se fera là, pas
  dans quinze fichiers.
- **La séquence scriptée de l'ennemi** (« après 2 s, direct ; après 1,5 s, crochet ») est le seul
  moyen de vérifier au timing près une esquive ou une fenêtre d'impact. Une IA aléatoire rend ce
  test impossible.
- **L'ennemi réagit au démarrage réel du coup adverse**, en écoutant l'exécuteur de sa cible, avec
  un temps de réaction réglable. Il ne triche pas sur une intention cachée.
- **HUD et vignette en IMGUI.** Un projet Unity neuf n'a ni police, ni TextMeshPro, ni sprite, ni
  post-processing. Cette interface ne dépend de rien et s'affiche identiquement dans les trois
  render pipelines. Le passage à un Canvas ne touchera que ces deux composants.
- **Portées calibrées sur la géométrie réelle** : le poing atteint ~0,55 m devant le centre du
  corps, la hurtbox fait 0,28 m de rayon, donc l'ennemi doit se tenir à ~0,95 m. Une portée
  d'attaque réglée à vue laissait l'ennemi frapper dans le vide.

### Ce qui est prêt pour la suite

| Ajout futur | Ce qu'il faudra toucher |
|---|---|
| Coup de pied, coude, coup de tête | Un asset `AttackData` de plus. Aucun code. |
| Parade, contre, projection | Un état de plus dans la machine + sa priorité |
| Combo | Un `ComboResolver` qui enchaîne des `AttackData` ; l'exécuteur ne change pas |
| Esquive parfaite | Mesurer l'écart entre le début d'esquive et l'ouverture de la fenêtre adverse |
| Améliorations, équipement, buffs | `StatModifier` : tout le reste suit sans modification |
| Plusieurs ennemis | Rien : aucun singleton, et le registre `Combatant.All` gère déjà la cible |
| Vrais modèles 3D | `Uber Bagarre → 5 - Brancher le modele 3D`, voir MODELE_3D.md |

---

## 10. Correctifs et interface (retour de test)

### Le bug qui rendait le combat impossible

Les portées avaient été réglées à vue. En les calculant :

```
poing tendu                 0,58 m devant le centre du corps
+ rayon de la hitbox        0,10  →  portée réelle      0,68 m
+ rayon de la hurtbox       0,15  →  distance maximale  0,83 m

distance de combat de l'IA                              0,95 m   ← hors de portée
```

Personne ne pouvait toucher personne, et l'ennemi paraissait « fixer bêtement » parce qu'il
frappait dans le vide. Correction en trois points, tous vérifiables par le calcul :

- **hurtbox élargies** (corps 0,28 → 0,38, tête 0,15 → 0,21). Une zone touchable plus large que
  le modèle est la norme en jeu de combat : elle pardonne l'imprécision, là où une zone collée
  au personnage donne l'impression de le traverser ;
- **rayons de hitbox** portés à 0,15 / 0,17 ;
- **capsules de collision réduites** (0,30/0,32 → 0,28/0,30) et distance de combat à 0,85 m.

**Leçon retenue :** une portée d'attaque n'est pas un réglage de ressenti, c'est une contrainte
géométrique. Elle se calcule à partir de la chaîne complète — extension du poing, rayon de
hitbox, rayon de hurtbox, rayon des capsules de collision — avant d'être ajustée à l'œil.

### Le conflit de touche

`Alt` était à la fois l'esquive et le modificateur d'uppercut : un Alt + clic déclenchait les
deux. Les coups ont maintenant chacun leur entrée (gauche / droit / molette), sans modificateur.
Plus discoverable, et structurellement impossible à remettre en conflit.

### Marche sur-jouée

Le poids d'animation atteignait son maximum dès la **marche** : toutes les amplitudes étaient
donc à fond en permanence. Il est désormais calé sur la vitesse de **course** — marcher donne
~60 % d'amplitude — et les amplitudes de base ont été divisées par deux.

### Le sprint ne coûtait rien

L'endurance n'était consommée que par les coups et l'esquive. Courir était gratuit, donc fuir
aussi, ce qui vidait la gestion d'endurance de son sens. Le sprint puise maintenant dans la
jauge et se coupe quand elle est vide.

### Interface

- **`GuiKit`** : toute l'interface est dessinée à partir d'un unique pixel blanc teinté. Aucune
  image, aucune police importée, aucun Canvas — un projet Unity neuf n'a rien de tout ça.
- **Gros chiffre de vie** plutôt qu'une simple barre : en pleine action on ne *lit* pas une
  barre, on la perçoit. Un chiffre qui change de couleur et tressaute se capte en vision
  périphérique, sans quitter l'adversaire des yeux.
- **Couche « retard »** sur les jauges : la vraie valeur tombe d'un coup, la couche claire la
  rattrape lentement. On voit *combien* on vient de perdre, au lieu de le déduire.
- **Barre de vie au-dessus de l'ennemi** : en première personne, une barre en haut d'écran
  oblige à quitter des yeux l'adversaire au moment précis où il faut le regarder.
- **Chiffres de dégâts** : sans eux, le joueur *suppose* qu'un crochet fait plus mal qu'un
  direct. Les voir rend l'équilibrage lisible en jouant. Tête et coups lourds sont écrits plus
  gros et d'une autre couleur — l'information « bien placé » se lit avant le chiffre.
- **`Combatant.AnyDamaged`**, événement statique : l'affichage des dégâts doit réagir à des
  combattants apparus après lui. Ce n'est pas un singleton — aucune logique, aucun état, juste
  une notification.
- **Compteur de combo** qui se brise quand on encaisse : sans rupture, il ne récompense rien.

### Décor

Une boîte en cubes n'est pas qu'un problème esthétique : sans verticales proches (poteaux,
grillage, façades), on ne perçoit ni son propre déplacement ni celui de l'adversaire. L'arène
est maintenant une ruelle — bitume et brique générés par code, trottoirs, grillages,
lampadaires **avec de vraies lumières** (donc de vraies ombres portées, sans lesquelles les
combattants semblent flotter), bennes, barils, caisses. Et un **cercle peint au sol** qui donne
une référence de distance immédiate : à portée de poing, ou pas.

---

## 11. La panne des données d'attaque, et ce qu'elle a changé

### Symptôme

Impossible de frapper, aucune animation, rien en console. Diagnostic posé en une session
grâce aux journaux ajoutés : les trois `AttackData` arrivaient **nulles** dans `PlayerCombat`.

### Pourquoi c'était invisible

Le clic était bien lu, `TryPlay` bien appelé, et il refusait sur `attack == null` **en
retournant `false` sans rien écrire**. Un système qui échoue sans parler est indébogable à
distance : le symptôme (« rien ne se passe ») est identique pour une touche non lue, un état
bloqué, une endurance vide ou une donnée manquante.

### Trois corrections, de la plus superficielle à la plus structurelle

1. **Chaque refus dit sa cause** (`AttackExecutor.Refuse`), et `PlayerCombat` contrôle ses
   coups au démarrage. Le symptôme devient un message.
2. **Le câblage est relu après construction** (`SerializedWiring.Verify`). Une référence restée
   vide se voit au moment de générer la scène, pas au lancement du jeu.
3. **Les coups vivent dans `Resources`**, et le jeu va les y chercher si la référence de scène
   est absente. C'est la correction qui compte : une référence de scène peut se perdre de
   plusieurs façons (scène non régénérée après mise à jour, asset recréé, câblage échoué), et
   aucune d'elles ne doit rendre le jeu muet. La référence de scène devient une commodité,
   plus un point de rupture unique.

L'ennemi bénéficie du même filet : un répertoire de coups vide est reconstitué depuis Resources.

### Ce que je retiens

Une dépendance critique qui ne peut être satisfaite que d'une seule manière est un point de
rupture. Quand cette manière est un câblage d'éditeur — invisible, silencieux, détruit par une
régénération — il faut un second chemin. Le coût est de quelques lignes ; l'absence de second
chemin a coûté plusieurs allers-retours de test.

Corollaire pratique : **tout `return false` dans un chemin critique mérite une raison
journalisable.** Ce n'est pas du bruit, c'est ce qui rend un système diagnosticable sans y avoir
les mains dedans.

---

## 12. Conséquences : zones, chute, garde, recul

Cette phase n'ajoute presque aucune mécanique nouvelle — elle donne des **conséquences** à
celles qui existaient. Un prototype de combat peut être complet sur le papier et rester creux :
si toucher la tête, le ventre ou la cuisse produit le même résultat, il n'y a rien à décider.

### 12.1 Les zones ne servaient à rien, et c'était invisible

`HitZone` existait depuis la phase 6, avec ses multiplicateurs. Deux choses l'empêchaient de
fonctionner.

**D'abord, il n'y avait que deux zones, et la hurtbox de corps couvrait tout** — de 0,22 m à
1,75 m sur un corps de 1,80 m. Un coup aux chevilles comptait donc comme un coup au torse.

**Ensuite, et c'est le vrai problème : le choix de la zone n'était pas déterministe.** La hitbox
faisait un `Physics.OverlapSphere` et retenait la première hurtbox rencontrée, avec une seule
touche par combattant et par attaque. Or l'ordre de retour d'`OverlapSphere` n'est pas spécifié.
Dès que deux zones se recouvraient, le **même coup au même endroit** pouvait compter comme jambe,
corps ou tête d'une fois sur l'autre — avec des dégâts et des conséquences différents, et aucune
erreur nulle part.

Et il n'était pas question de supprimer le recouvrement : un trou entre la cuisse et le bas du
torse se traduirait par des coups qui ne font **rien du tout**, le pire ressenti possible.

La correction est dans `Hitbox.TestSphere` : on collecte les candidats, et on retient pour chaque
combattant **la zone la plus proche du point d'impact**. C'est déterministe, et c'est aussi la
règle la plus intuitive — on touche là où le poing se trouve réellement. Les trois zones peuvent
alors se chevaucher librement :

| Zone | Hauteurs (corps de 1,80 m) | Dégâts | Conséquence propre |
|---|---|---|---|
| Jambes | 0,00 → 0,92 | × 0,55 | 70 % de chances de **chute** |
| Corps | 0,74 → 1,54 | × 1,00 | — |
| Tête | 1,40 → 1,80 | × 1,60 | traité comme un coup lourd |

Le coup de pied bas existe grâce à ce tableau : 11 dégâts seulement, mais il ouvre la seule
situation où l'adversaire ne peut rien faire pendant deux secondes.

### 12.2 Pourquoi une chute procédurale et pas un ragdoll

Le squelette est piloté **à chaque image** par l'IK des quatre membres et par le cycle de marche.
Un ragdoll physique entrerait en conflit avec eux : il faudrait désactiver toute la chaîne, la
laisser à la physique, puis resynchroniser les cibles d'IK sur la pose obtenue pour se relever —
pour un résultat différent à chaque chute, donc impossible à régler.

`KnockdownSystem` fait basculer le corps autour de ses pieds en quatre phases (debout, chute,
au sol, relevé), en pilotant un seul poids. C'est déterministe, réglable au degré, et l'IK
continue son travail pendant toute la durée. `ProceduralLocomotion.KnockdownWeight` fait
d'ailleurs glisser les cibles de pied d'un repère **monde** vers un repère lié au corps : des
pieds restés vissés au sol tordraient les jambes pendant la bascule.

Deux détails qui ne sont pas des détails :

- **Le transform qui bascule est le CORPS, jamais la racine.** La racine porte le
  `CharacterController` et la caméra. Une capsule de collision couchée traverserait le sol.
- **En vue première personne, basculer le corps ne se voit pas.** Le joueur ne voit pas son
  torse. Sans déplacer le point de vue, « tomber » serait indiscernable d'une simple perte de
  contrôle. D'où `_cameraRoot`, et un nœud `CameraKnockdown` dédié dans le rig caméra — un nœud,
  un effet, pour que deux systèmes n'écrivent jamais sur la même rotation.

### 12.3 Le recul était juste dans les chiffres, et faux à l'écran

`HitReaction` appliquait bien un recul depuis la phase 6. Il était invisible, et le calcul dit
pourquoi : le recul est une **vitesse**, amortie linéairement par les moteurs. La distance
réellement parcourue vaut donc

```
distance = v² / (2 × amortissement)
```

Avec l'ancien réglage, un direct donnait 0,88 m/s et un amortissement de 8 : **4,8 cm**. Le
système fonctionnait parfaitement et ne produisait rien de perceptible.

La plage utile se lit directement dans la formule : 2 m/s pour 25 cm, 4 m/s pour un bon mètre.
Les valeurs sont désormais exprimées pour atterrir dedans.

C'est le même genre d'erreur que la portée d'attaque en phase 9 : **une grandeur de ressenti se
calcule, elle ne s'estime pas à l'œil.** Un amortissement et une vitesse ne disent rien tant
qu'on n'a pas écrit la distance qui en découle.

### 12.4 Garde et parade : la même touche, deux mécaniques opposées

Lier les deux à la même touche est délibéré. Ce qui les sépare est la **durée de maintien**, donc
le timing :

- **Garder** absorbe 72 % et coûte 11 d'endurance par coup encaissé. C'est une position
  d'attente, pas un abri. Endurance vide = garde brisée + 0,7 s d'étourdissement.
- **Parer** est la fenêtre de 0,26 s qui suit la levée de garde. Le coup est annulé, l'attaquant
  est étourdi 0,6 s, repoussé, son coup annulé, et le défenseur récupère 16 d'endurance.

Sans la fenêtre, garder en permanence serait toujours la meilleure option ; sans le coût, ce
serait la seule. La garde ne protège que dans un cône frontal de 120° : contourner reste payant.

**Le drapeau `DamageInfo.Blocked` est la pièce qui rend tout ça réel.** Sans lui, un coup bloqué
arrivait en aval exactement comme un coup pris en pleine face : même état `Hit`, même annulation
du coup en cours, même chance de chute, mêmes bleus. Bloquer n'aurait changé qu'un nombre. Le
drapeau voyage avec les dégâts, et chaque système décide : `HitReaction` ne retire plus le
contrôle, `KnockdownSystem` ne fait plus tomber, `BruiseSystem` ne marque plus la peau. Un
combattant qui bloque **garde l'initiative** — c'est précisément ce qu'il achète avec son
endurance.

Côté ennemi, la fenêtre de parade est réglée à 0,06 s au lieu de 0,26 s. Ce n'est pas une
approximation : il lève sa garde **en réaction** à l'attaque détectée, donc avec la fenêtre du
joueur il parerait presque tout et le joueur n'aurait jamais la main. L'ennemi **bloque**, le
joueur **pare** : le timing reste une compétence humaine.

### 12.5 Les coups de pied, et pourquoi ils ne sont pas des coups de poing

Trois différences, toutes imposées par la mécanique et non par le goût :

1. **Le repère n'est pas le même.** Une pose de poing vit dans le repère de visée, qui suit le
   tangage de la caméra. Appliquer ça à un pied voudrait dire que lever les yeux envoie le coup
   de pied en l'air. Les poses de pied vivent donc dans le repère du personnage, origine au sol :
   « 0,97 » veut dire 97 cm au-dessus du sol, la seule unité qui permette de régler un coup de pied.
2. **La jambe est déjà occupée.** Le cycle de marche la pilote. Plutôt que deux systèmes qui
   s'écrasent, le combat **dépose une cible et un poids** (`SetFootOverride`) et la locomotion
   reste seule à écrire sur l'os. La reprise et la restitution sont volontairement plus lentes
   que pour un poing (18 % / 28 % du coup), sinon le pied claque d'une position à l'autre.
3. **La hauteur d'impact décide de la zone.** Le coup de pied de face vise 0,97 m, franchement
   au-dessus du bassin : 10 cm plus bas, il basculait dans la zone « jambes » et ses dégâts
   étaient divisés par deux. Les cibles de cheville restent par ailleurs sous 0,82 m de la
   hanche, pour une jambe de 0,86 m — au-delà, l'IK sature et le balayage se figerait en
   milieu de course.

Au passage, `AttackPoseKey` a gagné une pose de **membre libre**. Un bras qui part seul pendant
que l'autre reste figé est la signature d'une animation bricolée : sur un poing, l'autre main
remonte se couvrir ; sur un coup de pied, le bras opposé s'ouvre pour tenir l'équilibre. C'est
de la donnée, pas du code — la règle unique est « la main opposée au membre qui frappe ».

### 12.6 La glissade se payait à la seconde, donc presque rien

Le sprint est facturé par seconde, et c'est correct : courir longtemps coûte plus que courir un
instant. Appliquer la même logique à la glissade était une erreur de modèle. Une glissade relancée
en boucle ne facturait que le temps réellement écoulé — quelques dixièmes à chaque fois, la barre
bougeait à peine, et la glissade restait spammable **alors que le coût existait**.

Une glissade est un **événement**, pas une durée : elle se paie à l'entrée, en une fois (18), et
on la refuse avant qu'elle ne parte s'il n'y a pas de quoi la payer. Le moteur ne connaît toujours
pas l'endurance — il expose un événement `SlideStarted` et un drapeau `SlideBlocked`, exactement
le couple déjà en place pour le sprint.

### 12.7 « Ça fait vieux, trop low poly »

Ce reproche ne portait presque pas sur la géométrie. Quatre causes, par ordre d'effet réel :

1. **Aucun anticrénelage.** Un projet Unity neuf démarre avec MSAA désactivé. Toutes les arêtes
   sont en escalier, ce qui est le marqueur visuel le plus daté qui existe. `VisualQuality`
   l'active (8×), avec le filtrage anisotrope, des ombres en 4 cascades sur 70 m, et une brume
   légère — sans elle, un décor proche et un décor lointain ont le même contraste et la scène
   paraît plate.
2. **La lumière.** Un soleil pâle et vertical aplatit tout. Un soleil rasant (18°) et **chaud**,
   opposé à un appoint froid venant du ciel, crée des ombres longues donc du relief. C'est le
   réglage qui change le plus l'impression générale, pour zéro triangle de plus.
3. **Les silhouettes.** 14 côtés sur un bras, ça se voit dès qu'il passe devant un fond clair.
   Les subdivisions ont été augmentées (22 côtés, 16 subdivisions pour les boîtes adoucies) :
   quelques milliers de triangles au total, une fraction d'un personnage de jeu moderne.
4. **L'ombrage de la boîte adoucie.** Ses six faces ne partagent aucun sommet, donc
   `RecalculateNormals` laissait une cassure nette sur chaque arête du cube alors que la
   géométrie, elle, était arrondie. Les normales sont maintenant **calculées** : mélange de la
   normale de face et de la direction radiale dans la même proportion que la géométrie. Un poing
   cesse d'avoir des facettes.

Un détail qui aurait pu annuler tout le point 3 : `ProceduralMeshFactory` gardait tout maillage
déjà présent sur disque. Affiner une forme dans le code n'aurait donc rien changé pour qui a déjà
ouvert le projet une fois. Le nombre de sommets sert désormais de signature, et l'asset est
**réécrit en place** — l'identifiant reste le même, donc aucune référence de scène ne tombe.

### 12.8 Ce que je retiens

Trois pannes de cette phase (zones non déterministes, recul invisible, glissade gratuite) ont la
même forme : **le système était écrit, branché et correct, et ne produisait rien d'observable.**
Aucune n'aurait provoqué le moindre message d'erreur.

Le point commun est qu'aucune des trois n'était vérifiable en lisant le code. Il fallait à chaque
fois écrire la grandeur finale : la distance parcourue, la hauteur d'impact comparée aux bornes
de zone, le coût réellement prélevé sur une partie. C'est la même leçon que la portée d'attaque en
phase 9, et elle mérite d'être énoncée comme une règle : **une valeur de ressenti se calcule
jusqu'à son unité observable avant d'être réglée à l'œil.**

---

## 13. Audit de la phase 12 : deux choses faites « sur le papier »

Relecture de la demande mot par mot, une fois le code écrit. Onze points sur treize tenaient.
Les deux autres relevaient du même défaut, et c'est devenu le défaut signature de ce projet : le
système existe, il est correct, et il ne produit rien d'observable.

### 13.1 « Une animation pour quand il se relève »

Ce qui était codé : la bascule du corps repassait de 84° à 0° avec un lissage. Autrement dit **la
chute jouée à l'envers**. Ça se lit immédiatement comme faux, parce que personne ne se relève en
décrivant exactement la trajectoire de sa chute.

Un relevé humain a deux temps, pas un : on se **ramasse** (le torse quitte le sol vite, le corps
reste plié, on passe par un appui sur le côté), puis on se **déplie**. Les deux temps suivent donc
maintenant deux courbes déphasées :

- la bascule tombe de 84° à 35° sur les 38 premiers pourcents, puis de 35° à 0° sur le reste ;
- le bassin se plie en cloche (`sin`), maximum au milieu du relevé : c'est la position accroupie
  par laquelle on passe forcément ;
- un roulis de 22° s'ajoute au début, du côté où on est tombé : on se met sur le côté, on ne se
  redresse pas à plat dos d'une seule pièce.

Le pliage du bassin passe par une nouvelle propriété `ProceduralLocomotion.ExtraPelvisDrop`, pas
par un accès direct à l'os. Même règle que partout ailleurs : **la locomotion est seule à écrire
sur le bassin.** Deux systèmes qui écrivent sur le même transform, c'est le dernier exécuté qui
gagne, et l'ordre d'exécution n'est pas quelque chose sur quoi on veut parier.

Ce qui reste absent, et je le note plutôt que de le laisser croire : les **mains ne poussent pas
sur le sol** pendant le relevé. Les bras sont pilotés par l'IK dans le repère de visée ; leur faire
chercher le sol demanderait un canal de pose supplémentaire, et le risque de conflit avec
l'exécuteur de coups est réel pour un gain moindre que les trois éléments ci-dessus.

### 13.2 « Je veux le torse, tête et jambe »

Les trois zones existaient, avec trois multiplicateurs, et **rien à l'écran ne disait laquelle
venait d'être touchée.** Le joueur voyait donc des chiffres qui varient sans pouvoir relier la
variation à son geste — c'est-à-dire sans pouvoir apprendre à viser. Un système de zones qu'on ne
peut pas lire n'est pas un système de zones, c'est du bruit dans les dégâts.

Trois ajouts, tous du côté affichage :

1. **La zone est écrite sous le chiffre** (TETE / CORPS / JAMBES), avec une couleur par zone.
2. **« BLOQUE » est écrit** quand la garde a absorbé. Sans ce mot, un coup réduit de 72 %
   ressemble exactement à un coup mal placé, et le joueur conclurait que ses dégâts sont
   aléatoires.
3. **F1 affiche les trois zones de l'adversaire** avec leur multiplicateur, et la **hauteur exacte
   du membre qui frappe** pendant le coup.

Ce troisième point a d'ailleurs révélé un fait qu'aucune lecture de code n'aurait donné. La
hauteur du poing se calcule :

```
hauteur = hauteur_des_yeux + (y_pose · cos θ − z_pose · sin θ)      θ = tangage × 0,45
```

Avec la pose d'extension du direct (y = −0,055 ; z = 0,500) et un tangage maximal de 85° :

| Posture | Regard | Hauteur du poing | Zone atteinte |
|---|---|---|---|
| Debout (yeux 1,62 m) | droit devant | 1,57 m | Tête |
| Debout | au sol (θ = 38°) | **1,27 m** | Corps |
| Accroupi (yeux 1,00 m) | au sol | 0,65 m | Jambes |

**Debout, le poing ne peut pas descendre sous 1,27 m** — même avec une influence du tangage de
100 %, il plafonnerait à 1,07 m, car la descente est bornée par la longueur du bras. La zone
« jambes » s'arrêtant à 0,92 m, elle est **géométriquement inatteignable au poing en position
debout**. Ce n'est pas un bug, c'est juste vrai : on ne frappe pas la cuisse de quelqu'un au poing
sans se baisser. Mais sans ce calcul écrit noir sur blanc, le symptôme aurait été « les coups aux
jambes ne marchent pas » et j'aurais cherché le problème dans la détection.

La zone jambes a donc exactement deux accès : **s'accroupir** (C) en regardant vers le bas, ou le
**coup de pied bas** (V). Ce dernier devient par construction le coup qui ouvre les chutes, ce qui
lui donne enfin une raison d'exister au-delà de ses 11 dégâts.

### 13.3 Une erreur dans ma propre documentation

J'avais écrit que le coup de pied bas cumulait ses 55 % de chance de chute avec les 70 % de la
zone « jambes ». C'est faux : le code prend le **maximum** des chances applicables, pas leur
somme. Un coup de pied bas dans les jambes fait donc tomber à 70 %, pas à 86 %. Corrigé dans le
README, avec le tableau complet.

La leçon n'est pas sur le chiffre : c'est qu'**une documentation écrite de mémoire juste après
avoir codé est aussi peu fiable qu'une estimation à l'œil.** Elle se relit sur le code, comme
tout le reste.

---

## 14. Quand le système est juste et le résultat inexistant

Passe de corrections après un test complet. Sept reproches, dont trois portaient sur des features
que je croyais livrées. Les trois ont la même forme, déjà rencontrée en phase 9, 12 et 13 : **le
code est écrit, branché, correct, et ne produit rien d'observable.** Aucun ne provoque d'erreur.

À ce stade, ce n'est plus une coïncidence mais un type de bug à part entière, et il mérite son nom :
une panne où la chaîne fonctionne et où le dernier maillon — celui qui atteint l'œil ou l'oreille du
joueur — est faux d'un ordre de grandeur.

### 14.1 Les bleus existaient, à 19 cm de la peau

`DamageInfo.Point` vient de `collider.ClosestPoint()` sur la **hurtbox**, dont le rayon est
volontairement généreux : 38 cm pour le torse, pour pardonner l'imprécision du joueur. Le torse
visible, lui, fait 19 cm de rayon.

Les marques apparaissaient donc systématiquement à une vingtaine de centimètres de la peau, en
suspension dans le vide à côté du personnage. Elles étaient bien créées, animées, accrochées à un
os, et invisibles — parce que personne ne regarde l'air à côté d'un combattant.

Deux corrections, et la seconde est moins évidente :

1. Le point est reprojeté sur `Renderer.bounds.ClosestPoint()` du morceau de corps **visible** le
   plus proche. Ça ne demande aucun collider sur la chair et ça marche aussi sur un modèle importé.
2. La marque est accrochée à l'**os**, pas au visuel. Les morceaux de chair sont des maillages
   unitaires mis à l'échelle — un torse, c'est la boîte adoucie redimensionnée en
   (0,34 ; 0,30 ; 0,22). Accrochée au visuel, la marque héritait de cette échelle non uniforme :
   étirée en largeur, écrasée en profondeur, et d'autant plus déformée qu'elle est posée en biais.

### 14.2 « La cible c'est que la tête » : le bon critère n'était pas géométrique

La phase 12 avait remplacé un choix de zone non déterministe par « la zone la plus proche du point
d'impact ». C'était mieux, et c'était encore faux.

**Une zone large rayonne plus loin qu'une zone petite.** Le torse (38 cm de rayon) a sa surface plus
près du poing que la tête (18 cm) sous presque tous les angles, y compris quand le poing arrive par
le haut. Plus une zone est grosse, plus elle vole les coups destinées aux autres. Le critère
favorisait mécaniquement le torse, et réduire la tête n'y changeait rien : ça ne faisait que
déplacer le seuil.

Et un calcul a montré que le problème était plus profond. La hauteur du poing vaut

```
hauteur = yeux + (y_pose · cos θ − z_pose · sin θ)      θ = tangage × influence
```

Avec la pose d'extension (z = 0,50 m), le terme soustrait est borné par la longueur du bras :
**même à 100 % d'influence du tangage, un combattant debout ne descend pas son poing sous 1,07 m.**
La zone « jambes » s'arrêtant à 0,92 m, aucune position du poing ne pouvait l'atteindre. Le critère
géométrique condamnait donc une zone sur trois, définitivement.

La règle correcte en vue première personne est la seule qu'un joueur puisse apprendre : **je touche
là où je vise.** `AimResolver` lance un rayon depuis l'origine de visée — pour le joueur, la tête,
qui porte le tangage de la caméra, donc exactement la direction du réticule — et la première zone
adverse rencontrée gagne, quelle que soit la position du poing. La géométrie ne sert plus qu'à
savoir SI le coup porte.

Le même résolveur sert à l'affichage : le réticule annonce la zone visée et son multiplicateur
**avant** le coup. Il ne peut donc pas y avoir de désaccord entre ce que le joueur lit et ce qu'il
obtient — et surtout, la règle devient apprenable, ce qui était tout l'objet des zones.

### 14.3 Les bras restaient en l'air au-dessus d'un corps couché

Le nœud qui basculait pendant la chute contenait le corps et ses zones touchables. Il ne contenait
pas l'**ancrage des bras** de l'adversaire, qui vivait à hauteur d'yeux sur la racine.

Or cet ancrage est le repère dans lequel les poses de main sont exprimées : c'est lui qui donne aux
bras leur cible d'IK. Résultat, un adversaire couché au sol gardait les deux bras tendus vers le
ciel à 1,62 m — et sa barre de vie, accrochée au même repère, flottait au-dessus du vide.

La correction est structurelle plutôt que ponctuelle : **un seul nœud d'inclinaison**, et tout ce
qui doit se coucher vit dessous — corps, zones touchables, repère des bras. Faire basculer trois
transforms en parallèle finissait forcément par en oublier un, et l'oubli était spectaculaire.

### 14.4 « Pas assez snappy » : il manquait l'annulation d'enchaînement

Les durées n'étaient pas le problème principal. Un direct de 0,30 s n'est pas lent ; ce qui est lent,
c'est de devoir **attendre le retour à la garde** avant de relancer. Chaque coup se payait de sa
durée entière plus son temps de repos, soit 0,35 s de latence minimale entre deux coups, quelle que
soit l'intention du joueur.

`AttackData.comboCancelAt` ouvre une fenêtre juste après la fenêtre d'impact. Un nouveau coup lancé
dans cette fenêtre interrompt le précédent **sans temps de repos** — c'est la récompense d'avoir
laissé le coup aller au bout de sa chance de toucher. L'état `Attacking` ne bloque plus un
enchaînement, mais uniquement le sien : tout autre état (touché, étourdi, esquive) refuse toujours.

Avec ça, un direct repart à 0,14 s. Les durées ont aussi été resserrées (0,30 → 0,24 s), l'armement
raccourci de 22 % à 8 % du coup, et le plateau d'animation tenu jusqu'à 84 % au lieu de 78 % — un
coup passait un quart de sa durée à n'être ni en garde ni en extension, et c'est exactement la
sensation de mollesse.

### 14.5 Pourquoi les sons faisaient jouet

Les impacts étaient construits autour d'un **sinus**. Un sinus a une hauteur, donc on entend une
**note** — et un corps frappé ne joue pas de note. Le cri de douleur, lui, empilait deux sinus
harmoniques pour imiter des cordes vocales : une voix synthétisée par deux oscillateurs ne ressemble
à aucune voix humaine, et c'est le seul son que tout le monde remarque.

Toute la synthèse est refaite sur une règle unique : **du bruit filtré, jamais d'oscillateur
audible.** Ce qui distingue deux sons d'impact n'est pas leur hauteur mais leur enveloppe et leur
contenu spectral. Le filtre se **ferme** au fil de l'impact — clair puis sourd, comme une surface
qui absorbe —, le cri devient une expiration, la parade un claquement sec au lieu d'un « ting »
musical.

Détail qui n'en est pas un : tous les clips sont **normalisés**. Un son construit à partir de bruit
a une amplitude qui dépend du tirage aléatoire, donc deux générations du même clip ne sortent pas au
même volume — et les réglages de volume dans l'Inspector ne veulent alors rien dire.

### 14.6 Pourquoi une tête faite de boîtes ne marchera jamais

Dix boîtes adoucies empilées se lisent comme dix boîtes adoucies empilées, quelles que soient les
proportions : les jointures entre les blocs sont visibles sous tous les angles, et aucune n'existe
sur un visage.

Le crâne est maintenant un maillage unique (660 sommets, moins que les dix boîtes qu'il remplace),
obtenu en déformant une sphère. Deux asymétries portent tout le reste :

- le **menton**, qui se resserre fortement et avance ;
- l'**arrière du crâne**, plus volumineux que le front.

Ce sont elles, et pas le détail, qui font lire instantanément de quel côté quelqu'un regarde —
l'information la plus utile en combat. Les normales sont calculées analytiquement : une sphère UV
duplique ses sommets sur la couture de longitude, et `RecalculateNormals` y laisserait une ligne
d'ombrage verticale en plein milieu du visage.

### 14.7 « Trop low poly » parlait surtout des matières

Après MSAA, les ombres et les subdivisions de la phase 13, il restait le défaut le plus coûteux :
**tout était en couleur plate.** Une couleur plate ne réagit à la lumière que par son orientation,
donc deux surfaces tournées pareil sont rigoureusement identiques. Le résultat se lit comme une
maquette en plastique, et c'est probablement ce que « trop vieux » désignait depuis le début.

Chaque matière du corps a maintenant une texture générée : grain de peau, tissage de chemise, denim,
cuir. Trois échelles de bruit superposées, parce qu'une seule se lit comme une trame.

Un piège a failli annuler tout le gain : **la boîte adoucie n'avait aucune coordonnée de texture.**
Tous ses sommets étaient en (0,0), donc une texture appliquée sur un torse n'en aurait affiché qu'un
seul pixel, étiré sur tout le corps — autrement dit un aplat de couleur, exactement ce qu'on
cherchait à remplacer. C'est le genre de détail qui fait conclure « les textures ne servent à rien »
alors qu'elles n'ont jamais été échantillonnées.

### 14.8 Le ragdoll, et pourquoi il est réservé à la mort

Deux mécaniques différentes, pour une raison qui n'est pas esthétique :

- une chute sur coup aux jambes doit **se terminer par un relevé reproductible**. Elle reste donc
  procédurale, déterministe, réglable au degré ;
- une mort n'a rien à reproduire ni à relever. C'est le seul moment où la physique peut prendre la
  main sans entrer en conflit avec l'IK — et le moment où ça compte le plus.

`DeathRagdoll` construit 11 segments à l'instant du K.O. : Rigidbody, capsules, `CharacterJoint` aux
limites serrées, projection activée (un ragdoll généré à la volée finit sinon régulièrement avec un
membre à deux mètres du corps). Les pilotes sont coupés depuis une **liste explicite**, pas par une
recherche automatique : un seul pilote oublié écraserait la physique à chaque image et le ragdoll
resterait figé debout, sans qu'aucune erreur apparaisse.

Le joueur, qui ne voit pas son propre corps, n'a pas de ragdoll mais un effondrement définitif
(`KnockdownSystem.Collapse`) : son point de vue descend et ne se relève plus. C'est la seule façon de
sentir sa propre mort en première personne.

### 14.9 Le mécanisme qui manquait : la version des données

Le générateur ne réécrit jamais un asset d'attaque existant, pour ne pas effacer les réglages faits
à la main. C'est la bonne règle, et elle avait une conséquence que je n'avais pas vue : **affiner un
timing dans le code n'avait aucun effet pour quiconque avait déjà ouvert le projet une fois.**

Le symptôme n'était pas « mon asset n'est pas à jour ». C'était « tu n'as pas fait ce que j'ai
demandé » — parce que de l'extérieur, les deux sont indiscernables.

`AttackData.dataVersion` distingue désormais les deux cas qui se ressemblaient : un asset **réglé
par l'utilisateur** (on n'y touche pas) et un asset **créé par une version antérieure du code** (on
le met à jour, en le disant dans la console). Le tampon de version est posé après la configuration,
donc impossible à oublier dans un nouveau coup.

La leçon générale : **un mécanisme de préservation sans mécanisme de migration est un mécanisme de
gel.** Dès qu'on décide de ne pas écraser les données de l'utilisateur, il faut décider dans le même
mouvement comment on y apporte les corrections.

---

## 15. Phase fonctionnalités : ce qui manquait n'était pas de la finition

Demande de carte blanche sur les fonctionnalités, après plusieurs passes de correction. L'occasion
de regarder le prototype avec une autre question : non pas « qu'est-ce qui est cassé », mais
« qu'est-ce qu'il n'y a pas à décider ».

La réponse était franche. Le combat se résumait à choisir parmi cinq coups et à reculer quand la
vie descendait. Tout le reste — la chute, la parade, les zones — existait mais ne débouchait sur
rien : on mettait quelqu'au sol et on attendait, on parait et on ne perdait simplement pas de vie.

### 15.1 Deux statistiques fantômes

Avant d'ajouter quoi que ce soit, j'ai trouvé deux leviers qui existaient et que **rien ne lisait** :

- `StatType.AttackSpeed`, infobulle « multiplie la vitesse d'exécution des coups » — aucune ligne
  de code ne l'utilisait. `AttackExecutor` lisait `attack.duration` directement.
- `StatType.MoveSpeed`, même chose — `EnemyMotor` n'en savait rien.

C'est pire qu'une statistique absente : une stat réglable qui ne fait rien **fait croire que le
levier existe**. On la tourne, rien ne change, et on cherche le problème ailleurs. Les deux sont
maintenant branchées, ce qui a débloqué au passage les profils d'adversaire — un boxeur « rapide »
ne pouvait pas être rapide avant.

### 15.2 Donner une suite à ce qui n'en avait pas

Trois mécaniques existaient sans conséquence. Chacune a reçu la suite qui lui manquait :

| Mécanique | Avant | Maintenant |
|---|---|---|
| Parade | Annule le coup | Ouvre une **riposte ×2,2** pendant 0,9 s |
| Chute | Temps d'attente | Permet un **coup de grâce** à 28 dégâts |
| Coup à la tête | × 1,6 dégâts | Remplit la **jauge d'étourdissement** deux fois plus vite |

La riposte est **consommée** et pas seulement lue : une parade ouvre UN coup renforcé, pas une
seconde entière de dégâts doublés. Sans ça, parer une fois permettrait de placer quatre coups à
220 % et la parade cesserait d'être un échange pour devenir la seule ouverture utile du jeu.

La jauge d'étourdissement est la plus structurante des trois, parce qu'elle ajoute un **second axe**
à côté des points de vie : la pression. Sans elle, un échange est une soustraction et rien de ce qui
se passe entre deux coups n'a d'importance. Elle se remplit vite, se vide après un court répit, et a
un temps mort après déclenchement — sinon un adversaire sonné se fait re-sonner immédiatement et ne
rejoue plus jamais, ce qui est le piège classique de cette mécanique.

### 15.3 Quatre attaques de plus, zéro touche de plus

Sprinter, être en l'air, glisser, avoir un adversaire au sol : la même commande produit un coup
différent. C'est la façon la moins chère d'ajouter de la variété, et surtout celle qui ne demande
**rien à apprendre** — le joueur les découvre en jouant normalement.

L'ordre des substitutions est une priorité, pas un hasard : être en l'air l'emporte sur tout le
reste, parce qu'aucun autre coup n'a de sens les pieds décollés. Et le coup de grâce ne remplace que
les coups de PIED : achever quelqu'un au sol d'un crochet demanderait de se pencher, ce que le corps
ne sait pas faire.

### 15.4 Charger, et pourquoi tous les coups ne se chargent pas

Les coups **lourds** se chargent, les **rapides** se répètent. Ce partage découle du coup lui-même :
l'intérêt d'un direct est de partir tout de suite, donc le maintenir doit l'enchaîner ; l'intérêt
d'un uppercut est son poids, donc le maintenir doit l'armer. Chaque touche garde ainsi un
comportement qu'on peut deviner sans l'avoir lu.

La charge est gérée par l'exécuteur et non par le lecteur d'entrées, parce que c'est l'exécuteur qui
sait si un coup peut partir : charger en étant étourdi ou déjà engagé n'a aucun sens, et la charge
doit alors s'annuler plutôt que s'accumuler dans le vide.

Elle **se voit** : pose d'armement tenue, tremblement croissant, barre sous le réticule, et un
« CHARGE PLEINE » qui pulse. Une charge sans retour visuel est injouable — le joueur relâche au
hasard, ne voit pas la différence, et conclut que la mécanique ne sert à rien.

### 15.5 La caméra d'observation, et l'angle mort du FPS

Un jeu en première personne a un angle mort énorme : **on ne voit jamais son propre personnage.**
Tout le travail d'animation, de matière, de chute et de marques de coup porte sur un corps que le
joueur ne regarde jamais. Quand il dit « les animations sont horribles », ni lui ni moi ne pouvons
savoir de quoi on parle.

La caméra orbitale (F3) résout ça, à une condition : **le gameplay continue.** On peut marcher,
courir, frapper, se faire toucher et tomber pendant qu'on orbite. C'est donc un outil de jugement,
pas une caméra libre décorative.

Un détail a failli tout casser. La caméra d'observation éteint celle du jeu, or les affichages du
monde — barres de vie, chiffres de dégâts, reflets du soleil — gardent une référence **câblée** vers
la caméra première personne. Ils auraient continué à projeter depuis une caméra éteinte, et tout se
serait retrouvé au mauvais endroit de l'écran sans qu'aucune erreur n'apparaisse. D'où
`GuiKit.ActiveCamera`, et le fait que les deux caméras portent le même tag `MainCamera`.

### 15.6 Mode vagues : la difficulté par les statistiques, pas par le nombre

Un duel contre un adversaire réglé une fois pour toutes finit par se jouer toujours pareil, et on
n'apprend plus rien. À plusieurs, le combat pose d'autres questions : se replacer, ne pas se faire
encercler, choisir qui mettre au sol d'abord, garder de l'endurance pour sortir d'une mauvaise
position. Ce sont ces questions qui révèlent ce qui manque aux mécaniques.

La difficulté monte par les **statistiques** et non par le nombre seul : plus de vie, plus de dégâts,
des coups plus rapides. Multiplier les adversaires sans les renforcer rend les vagues plus longues
mais pas plus dures — et allonger un test n'apprend rien.

La montée passe par les overrides de statistiques, donc par le même chemin qu'une amélioration de
personnage : ce n'est pas un cas particulier câblé à part, c'est le système de stats utilisé
normalement.

### 15.7 Mesurer au lieu de discuter

« Ce n'est pas assez nerveux » n'est pas une information exploitable : cinq choses interviennent en
même temps — durée des coups, ralenti d'impact, tampon d'entrée, endurance, vitesse de déplacement —
et rien ne dit laquelle domine pour un joueur donné.

Deux réponses, et la seconde compte plus que la première :

1. **Mesurer.** L'overlay affiche la cadence réelle en coups par seconde, l'écart en millisecondes
   entre les deux derniers coups, et l'échelle de temps courante. Un ressenti devient un fait, et un
   fait se corrige.
2. **Donner les curseurs.** Les trois réglages de nervosité sont dans le menu, réglables en jouant.
   Les trouver en jouant prend trois minutes ; les deviner à distance prend un aller-retour de test
   par essai.

Les statistiques de combat relèvent de la même idée. La **réussite** est le chiffre le plus utile de
tous : un joueur qui rate la moitié de ses coups a l'impression que l'adversaire encaisse trop,
alors que le problème est sa précision. Aucune quantité de discussion ne tranche ça ; un pourcentage
le tranche en une seconde.

### 15.8 Les réglages sont sauvegardés

Détail d'ergonomie qui n'en est pas un : un réglage de ressenti se trouve par essais successifs,
parfois longs. Le perdre au redémarrage oblige à tout refaire, ce qui est exactement la friction que
ce menu existe pour supprimer.

### 15.9 Ce que je retiens

La question « qu'est-ce qui manque » a donné de bien meilleures réponses que « qu'est-ce qui est
cassé ». Et la meilleure de toutes n'était pas une fonctionnalité : c'était de constater que deux
leviers de réglage étaient branchés dans le vide.

**Un paramètre exposé et non lu est un mensonge de l'interface.** Il coûte plus cher qu'une absence,
parce qu'il dirige le travail de réglage vers un endroit où il ne se passe rien. Avant d'ajouter un
levier, vérifier que les existants en sont vraiment.

---

## 16. La nuit, ou pourquoi une scène « correcte » paraît quand même fausse

Demande : *« des graphismes de bâtard, du bloom, avec la nuit comme du ray tracing, et la map de A à Z. »*

### 16.1 Le vrai problème : la valeur d'un pixel est plafonnée à 1

Avant cette phase, la scène était éclairée, texturée, ombrée, antialiasée — et elle paraissait
plate. La cause n'était ni la géométrie ni les matières : c'est qu'**aucune source lumineuse ne se
comportait comme une source**.

Une enseigne au néon, un phare et un mur blanc bien éclairé sortent tous les trois du rendu avec une
valeur proche de 1. Sans traitement après coup, ils s'affichent identiquement. L'œil, lui, sait
parfaitement qu'un néon est mille fois plus lumineux qu'un mur — il le sait parce que, dans la
réalité, la lumière **déborde** : elle diffuse dans l'œil, dans l'objectif, dans l'air humide. Sans
ce débordement, le cerveau conclut « surface peinte », pas « lampe ».

C'est le diagnostic qui a décidé de tout le reste. Il ne fallait pas plus de triangles ni de
meilleures textures : il fallait une plage dynamique, et de quoi la rendre visible.

### 16.2 Pourquoi écrire la chaîne de post-traitement à la main

Le projet est en Built-in Render Pipeline, sans le paquet Post Processing. L'ajouter imposerait une
version de paquet et un pipeline, dans un dépôt dont tout le principe est de tourner du premier coup
dans un projet vierge.

`UberPost.shader` fait donc le travail en quatre passes : préfiltre, réduction, agrandissement,
composition. Trois détails qui n'en sont pas :

- **La moyenne de Karis au préfiltre.** Sans elle, un pixel isolé à 40 de luminance domine tout son
  voisinage et produit un scintillement franc dès que la caméra tourne d'un demi-pixel. Le bloom
  devient alors un défaut visible plutôt qu'un effet.
- **Le seuil à genou doux.** Une coupure franche dessine un contour net autour de chaque source,
  très visible sur un dégradé. Le genou étale l'entrée en seuil sur une plage.
- **La pyramide plutôt qu'un flou large.** Une source réelle a un cœur serré *et* une nappe très
  large. Un flou unique ne donne qu'une seule taille de halo, et se lit comme un calque.

Et un choix de conception plus important que les trois : **le tonemap ACES est appliqué après
l'étalonnage, pas avant**. Appliquer un contraste après la courbe réécrase les hautes lumières
qu'elle vient justement de sauver. C'est aussi ACES qui désature progressivement les très hautes
lumières — sans quoi un néon rouge saturé devient un aplat rouge pur, sans cœur blanc, c'est-à-dire
exactement l'aspect « couleur vive » au lieu de « lumière ».

### 16.3 Le matériau d'effet n'est jamais celui de l'asset

La composition écrit une vingtaine d'uniformes par image. Les écrire dans le matériau d'asset le
marquerait modifié en permanence : le projet aurait un fichier à sauvegarder à chaque seconde de jeu
en mode édition. Le composant travaille donc toujours sur une instance jetable.

L'asset, lui, sert à autre chose, et ce n'est pas facultatif : **un shader que rien ne référence
n'entre pas dans une build**. Un `Shader.Find` suffit dans l'éditeur et renvoie null une fois le jeu
compilé. Le matériau d'asset est ce qui garantit la compilation du shader.

### 16.4 Le reflet planaire : pourquoi pas une sonde, pourquoi pas de l'espace écran

La demande disait « comme du ray tracing ». Ce qu'on reconnaît sous ce nom, sur une rue de nuit,
c'est une chose précise : **le sol contient l'image de ce qui est au-dessus, et cette image bouge**.

Trois façons de l'obtenir, et deux ne marchent pas ici :

- **Une sonde de réflexion** capture la scène une fois, depuis un point fixe. Elle ne contient ni
  les combattants, ni les phares allumés, ni rien qui bouge. Or c'est précisément ce qu'on veut voir
  dans une flaque pendant une bagarre.
- **Une réflexion en espace écran** perd tout ce qui sort du champ. Quand on regarde ses pieds,
  l'enseigne qu'on veut voir reflétée n'est plus à l'écran : le reflet disparaît exactement au
  moment où on le cherche.
- **Un reflet planaire** rend réellement la scène en miroir dans une texture. Il coûte un second
  rendu — d'où la demi-résolution et les ombres coupées pendant ce rendu — mais il est **exact**, et
  l'adversaire qui tombe se voit tomber dans la flaque.

Le sol est plat et à y = 0, ce qui rend un seul plan suffisant. C'est une contrainte de décor
acceptée pour un gain de rendu, pas une limitation subie : découper le sol en morceaux à des
hauteurs différentes ferait apparaître des ruptures dans le reflet.

Deux pièges dans l'implémentation, tous deux silencieux :

1. **Le culling doit être inversé.** Un miroir inverse l'orientation ; sans inversion, toutes les
   faces visibles deviennent des faces arrière et la scène reflétée disparaît purement et simplement.
2. **La projection doit être oblique**, avec son plan proche sur le plan du miroir. Sinon les
   fondations des bâtiments apparaissent dans les flaques.

Et un garde-fou : la caméra de reflet déclencherait à son tour un rendu de reflet, et ainsi de suite
jusqu'au blocage complet de l'éditeur.

### 16.5 Ce qui rend une nuit crédible n'est pas la couleur

Baisser l'intensité du soleil et mettre du bleu donne une image grise et sale, pas une nuit. La
nuit change **six choses à la fois** : la direction et la couleur de la source principale, la
couleur du ciel, la densité et la teinte de la brume, la couleur de l'ambiante, et surtout le fait
que l'éclairage passe du soleil aux lampes.

Les régler séparément, c'est garantir qu'un seul sera oublié. `TimeOfDay` les pilote donc avec un
seul curseur — ce qui donne gratuitement un cycle jour / nuit, et permet à l'arène de jour de rester
utilisable.

Deux détails qui viennent de là :

- **Le matériau de ciel est dupliqué en jeu, mais PAS en édition.** Une copie porte
  `HideFlags.DontSave` : la scène enregistrerait une référence vers un objet qui n'existe plus au
  rechargement, et rouvrir le projet donnerait un fond noir sans la moindre erreur pour l'expliquer.
- **Le reflet d'objectif suit la puissance de la source, pas seulement sa direction.** Sans ce test,
  le halo de soleil continuait de s'afficher en pleine nuit : un soleil invisible qui éblouit. Le
  même correctif fait naître le halo tout seul au lever du jour.

### 16.6 L'air doit être visible

Une lampe ponctuelle éclaire les surfaces et laisse l'air parfaitement transparent. On voit alors un
disque clair au sol **sans comprendre d'où il vient**. Les cônes additifs (`UberGlow`) rendent le
trajet de la lumière visible, et c'est ce que l'œil lit comme de la brume.

Leur disparition sur les bords vient du produit scalaire vue / normale : un cône dont la silhouette
est nette se lit comme un cône en plastique, pas comme de la lumière. D'où aussi un cône **ouvert**,
sans fond ni sommet fermés — un disque additif plein apparaîtrait brutalement en passant dessous.

La bruine joue le même rôle sur toute l'image : elle justifie le sol mouillé (sans pluie, une
chaussée miroir est une décision arbitraire) et met de la matière entre la caméra et les façades.

### 16.7 La rue : trois plans, et le troisième est celui qu'on oublie

Le trottoir et la chaussée (où l'on se bat), les façades d'en face (à quinze mètres), et une
silhouette de ville au loin. **Sans le troisième, le ciel touche les toits** et la rue devient une
boîte — c'est le plan qui manque presque toujours, et son absence donne l'impression de décor de
studio.

Trois autres règles ont gouverné la construction :

- **Les hauteurs sont inégales et non périodiques.** L'œil détecte une période bien avant de
  reconnaître un bâtiment ; une rangée d'immeubles identiques trahit une ville générée en une seconde.
- **Chaque enseigne porte une vraie lampe** en plus de son matériau émissif. Sans elle, le néon
  brille mais n'éclaire rien, et la scène se lit comme des autocollants lumineux sur du carton.
- **La façade est construite en couches** — mur, socle, renfoncement, marquise, enseignes — parce
  que ce sont les décrochements qui créent des ombres portées les unes sur les autres. Une façade
  plate couverte de néons reste un panneau.

Les fenêtres allumées demandent **deux textures** et non une : réutiliser l'albédo comme carte
d'émission fait émettre le mur entre les fenêtres, et l'immeuble entier devient une lanterne.

### 16.8 Le piège qui revient à chaque phase

Deux occurrences de plus du même motif que les sections 12 à 15 décrivent : un mécanisme correct
dont le résultat est invisible, ou faux, sans qu'aucune erreur ne soit levée.

- **Le mélange additif double.** Sortir une couleur déjà multipliée par alpha *et* demander
  `Blend SrcAlpha One` la multiplie une seconde fois. Les halos auraient été presque invisibles, et
  la conclusion naturelle aurait été « les cônes de lumière ne marchent pas ».
- **L'axe d'une enseigne tournée.** Une enseigne drapeau pivotée de 90° a son axe X local le long de
  la rue : une potence construite sur le mauvais axe part dans le vide, parallèle à la façade. Rien
  ne le signale.

Le correctif systémique reste le même : **calculer jusqu'à l'unité observable avant de régler à
l'œil**, et vérifier qu'un paramètre exposé est effectivement lu. Deux matériaux de halo créés et
jamais référencés ont été trouvés de cette façon, par une simple comparaison entre ce que la palette
déclare et ce que le décor utilise.

### 16.9 L'espace colorimétrique

En gamma, Unity additionne les contributions des lampes sur des valeurs **déjà encodées pour
l'écran**. Deux lampes d'intensité 1 donnent beaucoup plus que 2, les hautes lumières se délavent en
blanc laiteux, et les dégradés autour d'un lampadaire cassent en bandes visibles. Une rue de nuit
éclairée par une trentaine de sources est exactement le cas où ça se voit le plus.

Le générateur le propose au lieu de l'imposer : le changement déclenche un réimport complet du
projet, et ça ne doit jamais être une surprise. Mais il faut le dire clairement — **le
post-traitement ne peut pas rattraper un éclairage calculé faux en amont**.

### 16.10 Ce que je retiens

Le reproche « ça fait vieux, c'est cheap » ne portait pas sur le nombre de triangles. Il portait sur
le fait qu'aucune lumière de la scène ne se comportait comme une lumière : rien ne débordait, rien
ne se reflétait, rien ne traversait l'air. Trois absences, pas un manque de détail.

**Ce qu'on prend pour un problème de modèles est très souvent un problème de plage dynamique.**

---

## 17. Le prologue : raconter sans quitter le jeu

Demande : *« commençons le prologue, avec le PDF, tu fais l'histoire. »*

Le dossier décrit le début du jeu en un paragraphe : la maison insalubre, l'appel d'un ami,
l'application interdite, la course à une étoile, le groupe devant la boîte, la bagarre, la photo.
Tout y est, et rien n'y dit comment le faire tenir dans un moteur.

### 17.1 Pourquoi une liste d'étapes en C# et pas des assets

Une étape d'histoire n'est pas du texte : c'est une **condition de sortie**. « Quand le joueur a
répondu au téléphone », « quand la cible est au sol », « quand trois coups ont porté ». Ces
conditions sont du code.

Les sérialiser aurait demandé un mini-langage de script : un éditeur, un interpréteur, et une
documentation. Le coût aurait été payé tout de suite, le bénéfice jamais — le prologue tient en
vingt-deux étapes et une seule personne les écrit.

Ce qui comptait vraiment, c'est que la séquence soit **lisible d'un seul tenant**. `PrologueDirector`
est volontairement un seul fichier avec une seule liste : elle se lit comme un synopsis, et c'est
elle le document. Éclatée en vingt objets de scène à remplir, la même chose aurait demandé six clics
pour savoir ce qui se passe après l'appel — et personne ne les aurait faits.

Le moteur, lui, ne connaît pas le prologue. `StoryDirector` sait enchaîner des étapes, afficher un
objectif, jouer des répliques et geler le joueur. Le scénario est ailleurs. Mélanger les deux aurait
donné un composant où changer une réplique oblige à relire la boucle de mise à jour.

### 17.2 Une étape se termine quand TROIS choses sont vraies

Ses répliques sont finies, sa durée plancher est écoulée, et sa condition est remplie. Les trois, et
pas une seule : un objectif rempli par hasard pendant un dialogue couperait la réplique au milieu, et
une réplique longue retiendrait une étape que le joueur a déjà accomplie.

### 17.3 Le décor parle avant les répliques

Le personnage ne dit jamais qu'il est fauché. Le jeu le montre : un matelas par terre plutôt qu'un
lit, une cuisine en deux meubles dont un a perdu sa porte, des bouteilles là où elles sont tombées,
et sur l'écran verrouillé du téléphone trois notifications — découvert bancaire, facture impayée,
troisième relance de loyer.

Lire le courrier est la seule interaction obligatoire de la séquence, et c'est délibéré : c'est la
seule chose qui explique pourquoi cet homme va accepter.

### 17.4 Le téléphone devait être un objet, pas un menu

Tout le concept passe par lui : on ne trouve pas les bagarres, on les **reçoit**, comme une course.
En faire une interface plein écran aurait été le plus simple, et aurait tué l'idée.

Il est donc tenu en main. Il occupe de la place, il s'incline, il vibre, il éclaire les mains dans le
noir et il apparaît dans les reflets du bitume mouillé. Son interface est dessinée **sur la dalle**,
à partir de ses quatre coins projetés à l'écran, via une transformation affine — elle suit donc le
poignet au lieu d'être collée par-dessus.

Deux décisions en découlent, et ce sont des décisions de jeu, pas de rendu :

- **On peut marcher en regardant son écran.** C'est la posture du personnage.
- **On ne peut pas frapper en le tenant.** Sortir le téléphone en plein combat devient une décision.

Un détail technique mérite d'être écrit parce qu'il décide de la LISIBILITÉ et qu'il a l'air
anodin : l'espace de mise en page n'est pas une résolution de maquette fixe, c'est la **taille
projetée en pixels**. Avec une maquette fixe, la matrice vaudrait par exemple 0,6 sur un écran donné
et un texte écrit en corps 14 s'afficherait en 8 pixels, illisible, sans que rien ne l'explique. À
l'échelle 1, un corps 14 fait 14 pixels partout, et c'est la mise en page qui s'adapte. Toutes les
dimensions du téléphone sont donc exprimées en fraction de la hauteur de sa dalle.

### 17.5 Repérer quelqu'un, ce n'est pas suivre une flèche

Le dossier écrit : « arriver devant le groupe et une fois que vous avez repéré l'individu, à vous
d'engager ». C'est une phrase qui cache une vraie question : comment fait-on « repérer » ?

Poser un marqueur au-dessus de la cible dès l'arrivée aurait été le réflexe, et aurait vidé la scène
de son idée. Le joueur n'aurait rien repéré : il aurait suivi une flèche, et la fiche de
signalement — veste rouge, jean clair — n'aurait servi à rien.

Alors personne n'est marqué. Le joueur **dévisage** les gens un par un ; rester une seconde sur
quelqu'un affiche ce qu'on voit de lui, et c'est au joueur de comparer. Le marqueur n'apparaît
qu'après, sur celui qu'il a reconnu : il ne désigne pas la cible, il **confirme une décision**.

Cela impose une contrainte qu'il fallait tenir : les quatre figurants sortent du même constructeur
que la cible. Même corps, même rig, même respiration. S'ils avaient été des mannequins immobiles, on
aurait repéré la cible au simple fait qu'elle est la seule à bouger, et tout le dispositif se serait
effondré.

Le signalement, lui, a été choisi pour être lisible **sous cet éclairage**. « Crâne rasé » ne
distingue personne — aucun personnage n'a de cheveux dans ce jeu. Une veste rouge franche tient le
coup même sous un néon magenta.

### 17.6 Le tutoriel attend un résultat, pas un délai

Chaque consigne a un compteur. Tant que les deux directs ne sont pas placés, l'étape ne passe pas.
Ce n'est pas une punition : c'est la seule garantie que la touche a été essayée. Une consigne qui
s'efface toute seule après trois secondes n'a rien enseigné à qui regardait ailleurs.

Le compteur sert aussi de retour immédiat, et c'est peut-être son rôle le plus utile : chaque coup
porté le fait avancer, donc le joueur sait tout de suite si son coup a compté. C'est exactement
l'information qui manque quand on découvre un système de combat.

Et si le joueur met K.O. avant d'avoir fini les consignes, elles sautent. Il a déjà prouvé qu'il
avait compris.

### 17.7 Deux lieux, une seule scène

La planque est à six cents mètres de la rue, dans la même scène, et un seul lieu est allumé à la
fois. Deux scènes Unity auraient obligé le joueur, son téléphone, le scénario et la fiche de mission
à survivre au chargement — donc à être marqués persistants, donc à exister en double le temps d'une
transition. Le premier bogue de ce genre est invisible et le second est incompréhensible.

Le fondu au noir du trajet rend les deux méthodes strictement identiques pour le joueur. La
contrepartie — une scène plus lourde et des coordonnées très écartées — ne se voit jamais en jeu.

Le déplacement passe par `ISpawnReceiver`, exactement comme le spawn de combat : c'est le seul
chemin qui désactive le CharacterController le temps du saut ET réaligne l'angle de visée. Déplacer
le joueur « à la main » l'aurait fait revenir à sa position d'avant dès l'image suivante, sans la
moindre erreur pour l'expliquer.

### 17.8 Ce qu'un prologue ne doit jamais faire : se bloquer

Trois garde-fous, et chacun répond à une panne précise :

- **Le joueur ne peut pas mourir.** Une mort au milieu d'un tutoriel laisserait la séquence sur une
  condition qui ne se réalisera jamais, et le joueur se retrouverait vivant devant un objectif
  impossible. Tant qu'il n'y a pas d'écran de défaite, ne pas mourir est la seule option cohérente.
- **L'étape de K.O. attend la MORT, pas la chute.** Un adversaire au sol se relève — c'est tout
  l'intérêt du système de chute. Terminer l'étape sur une chute aurait fait passer à la photo
  pendant qu'il se remet debout.
- **Chaque changement d'étape est journalisé.** À l'écran, un scénario bloqué et un scénario terminé
  se ressemblent beaucoup.

### 17.9 Une seule source de vérité pour « Bruno Moretti »

Le nom de la cible apparaît à cinq endroits : la fiche du suspect, l'objectif, l'étiquette au-dessus
de sa tête, la validation et l'avis du client. Cinq copies, c'est quatre occasions de n'en corriger
que trois. D'où `MissionBriefing`, un composant qui ne fait rien d'autre que porter ces chaînes.

### 17.10 Ce que je retiens

La partie difficile n'était aucune des mécaniques. C'était de décider, à chaque scène, **ce que le
jeu montre et ce qu'il dit**. Le personnage ne dit pas qu'il est fauché : on lit son courrier. Le jeu
ne désigne pas la cible : on la reconnaît. Le tutoriel n'explique pas le crochet : il attend qu'on
en place un.

**Chaque fois qu'on hésite entre le dire et le faire faire, le faire faire est meilleur — et c'est
presque toujours moins de code.**

---

## 18. Quand on tape, ça bouge : physique des coups, décor, chapitre 1

Demande : *« le combat est nul, je veux un truc interactif : quand on tape, ça bouge avec de la
physique là où j'ai tapé — et continue le prologue. »*

Le reproche était juste. Le recul existant déplaçait le combattant entier et inclinait son buste
d'un bloc : même réponse pour un direct au menton, un crochet dans les côtes et un coup de pied dans
la cuisse. Le corps n'avait pas de parties, et le décor n'existait que pour être regardé.

### 18.1 Des ressorts par os, pas un ragdoll actif

La solution évidente — un ragdoll actif en permanence, poussé par des moteurs vers la pose animée —
se bat contre l'animation procédurale : deux systèmes écrivent les mêmes os, chacun a raison, et le
résultat tremble. Il faut ensuite des semaines de réglage pour qu'un personnage tienne debout.

`BodyImpactPhysics` fait plus simple : chaque articulation (bassin, colonne, poitrine, tête, bras,
avant-bras, cuisses, tibias) porte un **ressort amorti à trois axes** qui s'AJOUTE à la pose animée.
Un coup est une impulsion `J` appliquée en un point : chaque os de la chaîne reçoit
`ω = (r × J) / I` par rapport à son propre pivot, avec une atténuation le long de la chaîne. Le
ressort ramène ensuite l'os à zéro en dépassant un peu (amortissement 0,36 : sous-critique exprès —
c'est le dépassement qui fait « encaisser »).

Propriété décisive : un ressort additif **ne diverge pas**. Au pire il revient à zéro. Il ne peut ni
faire tomber le personnage, ni le coincer dans une pose, ni désynchroniser le corps des hitbox.

### 18.2 La trajectoire, pas le nom du coup

La direction « vers l'avant de l'attaquant » ne dit rien du geste : un crochet arrive de côté, un
uppercut d'en bas, et ils ont le même avant. La hitbox mesure donc sa **vitesse** d'une image à
l'autre et la transmet dans `DamageInfo.Velocity`. `StrikeDirection` prend la trajectoire quand
elle est fiable (> 0,5 m/s), l'avant sinon. Résultat, sans une ligne spécifique par coup : le
crochet fait tourner la tête, l'uppercut la relève, le direct l'envoie en arrière.

### 18.3 Choisir l'os : la zone d'abord, la géométrie ensuite

Les hurtbox font 38 cm de rayon. « L'os le plus proche du point d'impact » aurait donc désigné les
avant-bras levés en garde sur la plupart des coups au corps. L'ordre est inversé : la **zone** touchée
choisit la famille d'os (tête → tête, jambe → cuisse ou tibia, corps → bassin / colonne / poitrine,
coup bloqué → avant-bras), et la géométrie ne départage qu'à l'intérieur de cette famille.

Deux réflexes que la mécanique seule ne produit pas sont ajoutés à la main : le **pliage** sur un
coup au ventre, et le **coup du lapin** sur un coup au torse (la tête reste en arrière pendant que
le buste part).

### 18.4 Ajouter après l'animation, retirer avant la suivante

Tous les écrivains d'os travaillent en `LateUpdate` (locomotion 80, ancrage des mains 90, mains 100).
L'impact passe à 500, donc **après** eux, et retire son offset au `Update` suivant. Sans ce retrait,
la nuque — que personne d'autre n'écrit — aurait accumulé l'offset d'image en image jusqu'à tourner
sur elle-même.

### 18.5 Le décor qui bouge

`PhysicsProp` fait d'un objet de décor un corps rigide avec une **matière** (verre, bois, métal,
plastique, mou) qui décide du son et de la casse. Trois chemins le mettent en mouvement :

- **frappé** : la hitbox qui touche un collider qui n'est pas une hurtbox pousse l'objet au point
  touché (`AddForceAtPosition`), une fois par geste. L'objet devient dangereux 0,7 s, au nom de celui
  qui l'a frappé : une bouteille envoyée d'un coup de pied dans une figure doit faire mal ;
- **bousculé** : `CharacterPusher` pousse ce que le joueur percute en marchant ;
- **lancé** : `PropHandler` (E pour ramasser, clic gauche pour lancer). Le projectile blesse via la
  hurtbox la plus proche de la victime — donc par tout le chemin normal d'un coup : zone, garde,
  recul, physique des os, bleus. Une bouteille sur le crâne compte comme un coup à la tête.

Deux pièges de construction, corrigés dans `NightStreetBuilder.MakePhysical` :

- la primitive Cylinder d'Unity porte un **CapsuleCollider** : une bouteille ou un fût debout
  basculait tout seul au démarrage. Il est remplacé par un `MeshCollider` convexe ;
- les caisses étaient positionnées par leur **centre**, donc à moitié dans le sol : un corps rigide
  à moitié enterré est éjecté à la première image. Elles le sont maintenant par leur **base**.

### 18.6 Un verrou de combat par propriétaire

Le téléphone coupait les coups avec un booléen. En ajoutant l'objet tenu en main, deux systèmes
l'écrivaient à chaque image : ranger le téléphone rendait les poings alors qu'on tenait encore une
bouteille. `PlayerInputReader.SetCombatLock(owner, locked)` tient une liste de propriétaires : les
coups reviennent quand **plus personne** ne les bloque. Chaque propriétaire libère son verrou dans
`OnDisable` — et `PropHandler` le libère aussi si l'objet tenu est détruit (verre cassé en main).

### 18.7 Coup de tête et bousculade

Deux gestes de corps à corps, qui n'existent que parce qu'on se bat désormais à plusieurs :

- **Bousculer (X)** : deux paumes dans le torse, 3 dégâts, 15 de force d'impact. Il ne sert pas à
  blesser mais à **faire de la place** — écarter l'un pour ne pas finir entre les deux.
- **Coup de tête (G)** : une hitbox sous le front du joueur (et sous la nuque des ennemis), 21
  dégâts, 22 % de chute, portée minuscule. La caméra plonge vers l'avant pendant le geste : c'est
  elle, la tête.

Tous deux court-circuitent la résolution contextuelle (charge, coup plongeant…) : un coup de tête en
sprintant reste un coup de tête. Le coup de tête a un verrou de capacité, `HeadbuttUnlocked`, que le
**scénario** ouvre.

### 18.8 La progression compte, le scénario récompense

`PlayerProgress` tient l'argent, l'expérience, le niveau, les courses et les avis. Il ne décide
d'**aucun** effet de jeu : il prévient (`LeveledUp`). C'est `PrologueDirector` qui décide que le
niveau 2 ouvre le coup de tête et que le niveau 3 donne +15 PV. Mettre les effets dans la table de
niveaux en aurait fait un endroit où l'on règle aussi des dégâts.

Les paliers (0, 100, 350, 700, 1200, 1900) sont choisis pour que **la toute première course** fasse
passer un niveau : le joueur doit voir le système exister à la fin du prologue, pas au bout de trois
heures. Une course qui franchit deux paliers les annonce **un par un**, pour que chacun débloque sa
capacité.

La page de **réputation** du téléphone (écran Profil) affiche niveau, XP, argent, note moyenne et
avis. C'est la demande du dossier : « une page notée avec des avis que les clients lui ont laissés ».

### 18.9 Le chapitre 1 : ce que deux adversaires enseignent

Le prologue apprenait les coups. Le chapitre 1 apprend ce qui n'a de sens qu'à **deux contre un** :
se placer, bousculer, se servir du décor. D'où le choix du lieu — un parking de nuit, rempli de
bouteilles, de caisses et de fûts qui roulent — et d'une embuscade qui part à l'approche de la
camionnette **ou au premier coup porté**, pour qu'un joueur qui ouvre les hostilités de loin avec
une bouteille ne soit pas rattrapé par l'histoire.

La capacité gagnée à la fin du prologue (le coup de tête) est demandée dans la bagarre qui suit
immédiatement : une capacité débloquée qu'on n'essaie pas tout de suite est une capacité oubliée.

Le parking est un **troisième lieu** de la même scène (+600 m en Z), géré par le même
`LocationDirector` : rien de nouveau n'a été nécessaire pour le trajet.

Les leçons prennent désormais leur condition de saut en paramètre : dans le prologue, « la cible est
au sol » ; au chapitre 1, « les deux frères sont K.O. ».

### 18.10 Deux preuves, pas deux clics

La photo exige maintenant une **liste** de sujets. Chacun compte une fois, et il doit être **au
sol** : photographier deux fois le même frère ne prouve rien sur l'autre, et photographier un sujet
debout ne prouve rien du tout. Le téléphone affiche le compte (« 1 / 2 »), et l'appli répond à chaque
refus (« Déjà envoyé », « Le sujet doit être au sol », « Sujet absent du cadre »). Une preuve qui ne
peut pas être ratée n'est pas une preuve.

### 18.11 Tester la suite sans rejouer le début

`PrologueDirector._startAtChapterOne` saute directement au chapitre. Le problème n'est pas le saut
(`StoryDirector.JumpTo` existait) mais l'**état** : sans le prologue, le joueur n'a ni argent, ni
niveau 2, ni coup de tête. `EnsureChapterOneState` encaisse donc la course du prologue d'office —
par le même chemin que le jeu, `CompleteContract`, ce qui déclenche le même passage de niveau et
débloque la même capacité. Un raccourci de test qui prend un autre chemin que le jeu teste autre
chose que le jeu.

Le compte final (« 480 euros sur la table, il en manque 160 ») est calculé à l'entrée de l'étape à
partir de l'argent **réel** : une somme écrite en dur aurait menti au premier réglage de récompense.

### 18.12 Ce que je retiens

« Le combat est nul » ne voulait pas dire « il manque des coups ». Il en avait déjà une quinzaine.
Il manquait la **réponse** : un coup qui arrive quelque part doit changer quelque chose à cet
endroit-là — une tête qui tourne, un ventre qui se plie, une bouteille qui roule. Ce n'est pas ce
que le joueur fait qui rend un combat vivant, c'est ce que le monde lui renvoie.

---

## 19. « Les lumières c'est des cônes » : lumière réelle, PBR, et une salle pleine

Demande : *« pourquoi il y a du bruit dans les graphismes, les lampadaires c'est juste des cônes
lumineux, pas de la vraie lumière — fais un intérieur du club avec des spectateurs, et que le combat
commence une fois qu'on a reçu la commande sur le téléphone. »*

Les trois reproches étaient fondés, et ils avaient des causes précises.

### 19.1 Le bruit venait de six endroits

| Source | Pourquoi ça grouillait | Correction |
|---|---|---|
| Grain de pellicule | Animé à chaque image, par défaut à 0,055 | Désactivé par défaut |
| Aberration chromatique | Dédouble chaque point lumineux au bord de l'image | Désactivée par défaut |
| Ondulations des flaques | Au loin, plus fines qu'un pixel : un moiré qui bouge | Éteintes progressivement avec la distance |
| Reflet planaire | Demi-résolution, sans anticrénelage : chaque arête reflétée scintille | Pleine résolution, MSAA ×4 sur la texture de reflet |
| Pluie | 420 traits/s plus fins qu'un pixel, très contrastés | 170/s, plus pâles |
| Arêtes fines | Rendu sans anticrénelage (et le différé n'a pas de MSAA) | FXAA sur l'image tonemappée |

Les réglages sauvegardés changent de clé (`UberBagarre.Gfx2.`) : sinon les anciennes valeurs,
grain compris, seraient revenues au premier lancement.

### 19.2 Pourquoi les lampes « n'éclairaient pas »

Le rendu **avant** d'Unity ne calcule qu'une poignée de lampes par objet ; les autres passent en
approximation par sommet. Un mur est un cube : quatre sommets par face. Une lampe calculée à ces
quatre coins ne dessine rien — au mieux une teinte uniforme. La rue en comptait une trentaine : la
plupart n'existaient pratiquement pas à l'écran.

Le passage en **rendu différé** règle la question d'un coup : chaque lampe est calculée par pixel,
sans plafond, avec ses ombres. Le prix est le MSAA (impossible en différé), d'où le FXAA. Le reflet
planaire, lui, reste en rendu avant : il utilise une projection oblique que le différé ne gère pas.

Les lampadaires étaient des lampes **ponctuelles** : elles éclairaient le haut de leur propre mât et
les façades à six mètres. Ce sont maintenant des **projecteurs** tournés vers le sol, avec ombres —
un disque de lumière sur la chaussée, et une ombre nette sous tout ce qui passe dessous.

### 19.3 Le faisceau est calculé à partir de la lampe, pas posé à côté

Les cônes en maillage additif avaient trois défauts impossibles à corriger : un bord (on voit la
forme), aucune occlusion (le cône traverse le combattant qui passe dessous), et aucun lien avec la
lampe (elle clignote, lui non ; on l'éteint au lever du jour, lui reste).

La passe volumétrique du post-traitement lit les lampes marquées `VolumetricLight` et intègre, pour
chaque pixel, la lumière reçue le long du rayon de vue jusqu'à la première surface (tampon de
profondeur) :

- l'intégrale de `1/r²` le long d'une droite a une **forme exacte** en arc tangente ;
- les échantillons sont placés à **pas d'angle constant vu depuis la lampe** (échantillonnage
  équi-angulaire) : denses près de l'ampoule, espacés au loin ;
- douze échantillons **fixes**, aucun tirage aléatoire : un lancer de rayons classique avec du bruit
  de tramage aurait justement produit le grouillement qu'on venait de supprimer.

Le cône du projecteur, la portée, et une brume plus dense près du sol modulent chaque échantillon.
Calcul à demi-résolution : le résultat est lisse par construction, un filtrage bilinéaire suffit.
Douze lampes au plus par image, les plus proches (au bord de leur portée, pas à leur centre).

Chaque lampe a son propre coefficient de diffusion : un lampadaire sous la bruine à 1, une lyre dans
la fumée du club à 3,2. C'est ainsi que la même passe donne une rue humide et une salle enfumée.

### 19.4 Le PBR a besoin de relief et de quelque chose à refléter

Le shader Standard est physique, mais il ne peut rien sur une surface plate qui reflète un ciel noir.
Deux ajouts :

- **cartes de relief** générées à partir des textures existantes (luminance → hauteur → Sobel), avec
  un flou avant dérivation pour les textures bruitées par pixel — sinon le relief scintille à la
  moindre distance. Enregistrées en espace **linéaire** : une normale n'est pas une couleur ;
- **sondes de réflexion** temps réel, rendues une fois à l'activation de chaque lieu. Dans les
  intérieurs, à projection en boîte : le reflet du béton ciré est à la bonne place.

### 19.5 La commande déclenche le combat — partout

C'était déjà vrai dans l'histoire, pas dans le bac à sable : l'adversaire attaquait au chargement.
`EnemyBrain.HoldAll` tient tous les adversaires en attente (garde baissée, immobiles) ; `SandboxOrder`
fait tomber la commande sur le téléphone après deux secondes, et la lâche quand on l'accepte. R
relance ensuite directement : on ne redemande pas la course à chaque réglage.

Au chapitre 2, la règle est poussée au bout : le joueur arrive au club **sans contrat**. Il attend,
la salle danse, le champion patiente dans la fosse — et rien ne commence avant que la commande tombe
et soit acceptée.

### 19.6 Un public, pas des figurants

Les spectateurs ont le corps des combattants (même squelette, mêmes jambes procédurales) et aucun
système de combat — leurs zones de frappe sont même retirées à la construction, pour qu'aucune
erreur de câblage ne puisse jamais faire frapper le public.

Ce qui en fait une foule tient en trois décisions :

- ils regardent **l'échange** (la moyenne des combattants proches), pas un point fixe ;
- chacun réagit avec **son propre retard** (80 à 400 ms) et son tempérament — trente personnes qui
  lèvent les bras à la même image se lisent comme une animation ;
- ils ont un **collider** : la foule fait mur autour de la fosse, et renvoie les combattants au
  centre.

La musique est **synthétisée** (grosse caisse, charleston, clap, basse, nappe), comme tout l'audio du
projet, et c'est elle qui donne le tempo : dalles de la piste, écrans, lyres et têtes des danseurs
lisent la position de lecture réelle du son. Rien ne dérive. La foule a sa voix — brouhaha permanent,
clameurs filtrées sur les formants d'une voyelle — qui répond aux coups.

### 19.7 Ce que je retiens

« Pas de la vraie lumière » ne voulait pas dire « pas assez de lumière ». Il y en avait trop, et elle
était fausse de deux façons : des lampes qui n'éclairaient pas (par sommet), et des faisceaux qui
n'étaient pas de la lumière (des cônes). Dans la rue, la correction n'a presque rien ajouté : elle a
rendu les lampes existantes réelles, et fait dériver tout le reste — faisceaux, reflets, ambiance —
de ces lampes-là.

---

## 20. Des mains, pas des saucisses ; un rythme, pas du martelage

Demande : *« on tombe dans le vide après la voiture ; quand on n'a plus d'endurance il faut un
délai ; un plus grand délai entre chaque coup, on ne doit pas pouvoir spammer ; les persos sont
brillants ; l'anticrénelage bugue ; change le modèle des mains pour de vraies mains, pas des
saucisses sur un Rubik's cube. »*

### 20.1 La main : une surface, pas un assemblage

L'ancienne main était une paume presque cubique (8 × 7 × 7,6 cm), un cube à chaque jointure et
quinze cylindres de rayon constant. Le défaut n'était pas le nombre de polygones : des pièces
rigides posées côte à côte ne peuvent pas ressembler à de la peau, parce que la peau est continue
et qu'elle se plie.

`HandMeshBuilder` construit une seule surface, liée aux os existants par un
`SkinnedMeshRenderer` (poignet, paume, quinze phalanges). Aux articulations, les sommets sont
partagés entre les deux phalanges : quand le doigt se ferme, la peau se plie au lieu de s'ouvrir
en deux cylindres. Le squelette ne change pas — poses de poing, de garde et de prise intactes.

Ce qui fait lire une main :

- une paume plate (2,6 cm aux jointures, 3,5 cm au poignet), plus large aux jointures, dont le
  bord avant suit la rangée des jointures (index et majeur en avant, auriculaire en retrait) ;
- les éminences du pouce et de l'auriculaire côté paume, les jointures sous la peau côté dos ;
- des doigts à section ovale (plus larges qu'épais, plus plats sur le dos), qui s'affinent, avec
  un renflement à chaque articulation, une pulpe au bout et un ongle ;
- des doigts de 1,9 cm de large à la base : les jointures du squelette sont espacées de 1,7 cm, un
  doigt plus large chevaucherait son voisin.

La géométrie a été vérifiée hors d'Unity, par une reproduction en Python : orientation de toutes
les faces vers l'extérieur, main ouverte, et poing fermé déformé avec les mêmes poids d'os.

### 20.2 Le rythme des coups

Trois règles, qui se complètent :

- **un coup = un appui** : maintenir le clic n'enchaîne plus (seuls les coups lourds se chargent) ;
- **un intervalle minimal** : 0,5 s entre deux coups, ou la durée du coup plus 0,2 s de
  récupération si elle est plus longue — un coup de pied lourd expose plus longtemps qu'un direct ;
- **un appui trop tôt est ignoré** : seul l'appui fait dans le dernier tampon avant la fin de la
  récupération est gardé. Marteler ne fait donc rien partir de plus ; frapper en rythme, si.

### 20.3 L'épuisement

Arrivé à zéro, un combattant est **épuisé** : aucune action coûteuse (coup, esquive, glissade)
pendant au moins 1,2 s, sans régénération pendant ce temps, et jusqu'à ce que 30 % de l'endurance
soient revenus. Sans cet état, une barre vide ne coûtait rien : on regagnait quelques points en une
fraction de seconde et on refrappait. L'interface l'annonce (« ÉPUISÉ — REPRENDS TON SOUFFLE ») tant
qu'il dure, même quand la barre remonte. La règle vaut aussi pour les adversaires.

### 20.4 La chute dans le vide

Le passage d'un lieu à l'autre est durci à trois niveaux, parce qu'une chute hors du décor laisse le
scénario sans issue :

- `Physics.SyncTransforms()` après l'activation du lieu : ses colliders existent dans la simulation
  avant que le joueur y soit posé ;
- le point d'arrivée est reposé sur le **vrai sol** par un rayon vers le bas (un autre personnage
  ne compte pas comme un sol) ;
- un **filet** : sous 12 m sous l'arrivée du lieu courant, le joueur y est ramené, et le cas est
  journalisé avec la position — pour savoir où le sol manquait.

### 20.5 Brillance et anticrénelage

Le reflet rasant (effet de Fresnel) monte avec le lissage du matériau. Avec des sondes de
réflexion pleines de néons, une peau à 0,30 et des tissus à 0,10 brillaient au bord comme du
vinyle. Peau à 0,16, tissus à 0,03–0,05.

Le FXAA ne voit qu'une image : il adoucit les escaliers mais laisse scintiller tout ce qui est plus
fin qu'un pixel (câbles, barreaux, reflets) dès qu'on bouge. Le **TAA** décale la caméra d'une
fraction de pixel à chaque image (suite de Halton), reprojette l'historique avec les vecteurs de
mouvement, et le borne par les couleurs voisines de l'image courante pour éviter les traînées. Le
mouvement est lu sur le pixel le plus proche du voisinage, et le calcul se fait sur des couleurs
compressées pour qu'un néon isolé ne clignote pas. MSAA ×8 (en rendu avant) et FXAA restent
disponibles depuis le menu.

## 21. Un jeu qui parle, une caméra qui tient, un adversaire qu'on voit

Demande : *« la caméra est bizarre, jittery, ça se coupe, surtout quand je tape ; le téléphone est
une vraie flashbang, et le perso devrait le tenir ; qu'il parle au téléphone, ou au moins des
bruitages ; des bruits de fond chez lui, des bruits de ville devant le club ; le combat n'est pas
snappy et l'endurance baisse trop vite ; dans le combat contre le gars on ne le voit pas du tout ;
pas de menu de debug en jeu ; il faut lancer le bac à sable avant le jeu pour que ça marche ; et un
menu de triche : noclip, vol, godmode, multiplicateur de dégâts… »*

### 21.1 La caméra : cinq secousses qui s'additionnaient

Chaque coup déclenchait en même temps un ralenti (0,25 pendant 32 ms), une secousse, un « punch »
de caméra, un recul de la tête par la physique des os, et le TAA reprojetait tout ça avec un
historique qui n'était plus valable. Chacun, seul, était raisonnable ; ensemble ils faisaient
sauter l'image à chaque impact. Tout a été ramené à ce qui se sent sans se voir : ralenti à 0,72
pendant 20 ms au plus, secousses d'amplitude divisée par trois et déclenchées deux fois moins
fort, punch amorti plus vite (retour en ~90 ms), recul de la caméra par la physique à 45 % et plafonné à 6°. L'anticrénelage par défaut
passe au **MSAA ×8** (rendu avant) : il ne dépend d'aucun historique, donc rien ne « traîne » ni ne
« saute » quand la vue bouge brutalement. Le TAA reste disponible dans le menu. Les préférences
graphiques et de bac à sable changent de clé : les anciennes valeurs sauvegardées ne ressuscitent
pas les saccades.

### 21.2 Un combat nerveux sans redevenir du martelage

Le rythme de la phase 20 était juste dans son principe (un appui = un coup, un intervalle minimal)
mais trop lent dans ses valeurs. Intervalle 0,34 s, récupération 0,08 s, et l'appui fait un peu
trop tôt est **gardé** 0,26 s au lieu d'être ignoré : on ne perd plus un coup parce qu'on a cliqué
deux images trop tôt. L'endurance du joueur coûte 40 % de moins que celle des adversaires, se
régénère à 40/s après 0,3 s, et l'épuisement dure 0,9 s jusqu'à 25 %.

### 21.3 Le téléphone : une lampe de poche devenue un écran

L'écran émettait assez pour allumer le bloom de toute l'image, et une lampe posée devant lui
éclairait le décor comme un projecteur. Émission, lampe, flash de photo et néon du téléphone sont
divisés par trois à cinq. Et il est **tenu** : `FirstPersonHands.SetHold` amène la main droite sur le
téléphone (poignet sous l'appareil, doigts refermés sur le bord) avec un poids qui suit
l'animation de sortie. La pose est appliquée en `LateUpdate`, après les mains, pour que la main
suive le téléphone et pas l'inverse.

### 21.4 Les voix

Un sous-titre silencieux se lit comme un bug d'audio. Les 75 répliques fixes du scénario sont
*(Retiré depuis : voir 24.2, les personnages babillent.)* **enregistrées** par une synthèse neuronale hors-ligne (Piper) : `Tools/generer_voix.py` lit
`PrologueDirector.cs`, évalue les concaténations (noms des cibles, tenues, récompenses) avec les
valeurs que pose le générateur de scène, synthétise, transforme la voix par personnage (hauteur,
débit, filtre de combiné pour Sami et l'appli), et écrit des OGG dans `Resources/Voix`.

Le lien entre le code et le fichier est une **empreinte** FNV-1a 32 bits de « PERSONNAGE|texte »,
calculée à l'identique en Python et en C# (`DialogueVoice.Key`, sur les unités UTF-16). Il n'y a
donc aucune table à tenir à jour : changer une réplique la rend muette (babillée) jusqu'à la
prochaine génération — jamais fausse. Les répliques calculées en jeu sont **babillées** : une source
glottale à la hauteur du personnage, deux formants de voyelle tirés au hasard, une syllabe toutes
les trois lettres, des silences sur la ponctuation — la même réplique donne toujours le même
babillage. Le sous-titre dure au moins autant que la voix.

### 21.5 Les ambiances

`AmbientSoundscape` a deux formes : une **couche** de lieu (2D, continue, avec des événements rares
tirés au hasard) et une **source ponctuelle** spatialisée (frigo, horloge, néon, basse du club).
Tout est synthétisé au premier passage puis gardé en mémoire : bruit brun filtré pour la rumeur et
le vent, bruit blanc en bande pour la pluie, chocs amortis pour les gouttes, sirène deux tons,
voiture avec Doppler et chuintement des pneus mouillés, grosse caisse et basse filtrées à 160 Hz
pour le club vu de la rue. Les boucles sont sans couture (la fin est fondue dans le début ; les
sons périodiques ont une période qui divise la boucle). Les composants vivent sous la racine de leur
lieu : `LocationDirector` éteint l'ambiance avec le lieu. Et quand un personnage parle, l'ambiance
baisse de 45 %.

### 21.6 On ne voyait pas l'adversaire

Le combat du prologue a lieu devant l'entrée du club, exactement dans le trou entre deux
lampadaires espacés de 44 m. Deux réponses :

- **le décor** : deux lampadaires de plus qui encadrent l'entrée (avec ombres) et le **projecteur
  du videur** sous la marquise, tourné vers le trottoir ;
- **la lumière de lecture** (`OpponentLight`) : une petite lampe invisible, placée devant et
  au-dessus de la tête de l'adversaire le plus proche, côté joueur. Elle n'éclaire que la face
  tournée vers nous, sur quelques mètres, et suit la cible en douceur. Le cinéma a la même règle :
  l'acteur a toujours sa lumière, même dans une ruelle « sans éclairage ». Elle vaut pour tous les
  lieux, parking et fosse compris.

### 21.7 Le jeu se construit seul

La scène du jeu ne se construisait correctement qu'après celle du bac à sable, parce que le dossier
`Settings` (où vit l'asset de touches) n'était créé que par elle : sans lui, la création de l'asset
échouait et la construction s'arrêtait en route. Le générateur du jeu crée maintenant tout ce dont
il a besoin (dossiers, touches, espace colorimétrique linéaire, inscription dans les Build Settings).

### 21.8 Le menu de test dans le jeu

Le menu du bac à sable (Tab) existe aussi dans le jeu, en **mode histoire** : pas d'onglet vagues,
et aucun réglage relu ou sauvegardé. Un nouvel onglet **TRICHE** pilote `DebugCheats` : godmode
(un drapeau `HealthSystem.GodMode` distinct de l'invulnérabilité de l'esquive, qui l'aurait
éteint à chaque roulade), noclip et vol (le `PlayerMotor` est coupé, le `CharacterController`
aussi pour le noclip, et le filet anti-chute du lieu est suspendu), endurance infinie, K.O. en un
coup, multiplicateur de dégâts, vitesse du temps (`HitStop.BaseTimeScale` : le ralenti d'impact y
revient au lieu de remettre le temps à 1), adversaires figés, et les raccourcis de l'histoire —
réplique, étape (`StoryDirector.SkipBeat`), chapitre (`PrologueDirector.StartAt`), lieu, argent,
niveau. Rien n'est sauvegardé, et un `DebugCheats` désactivé ne laisse aucune triche derrière lui.

## 22. Un téléphone qui en est un, des mains qui tiennent, des bagarres qui commencent

Demande : *« toujours le bug de caméra, check ; la caméra du téléphone ne marche pas, je veux un
fonctionnement comme dans GTA V ; le téléphone est toujours une flashbang et il ne le tient pas ; une
interface de téléphone, un OS avec des applis, que je sors quand je veux ; une mini-cinématique au
début du combat et une foule qui se forme doucement autour ; l'animation des coups plus humaine. »*

### 22.1 La caméra : ce que le code faisait vraiment

Trois causes, trouvées dans le code et non supposées :

- **Le ramasse-miettes.** `GuiKit.Style` créait un nouveau `GUIStyle` à CHAQUE appel. Une vingtaine
  d'affichages en appellent plusieurs, deux fois par image (passages Layout et Repaint d'OnGUI) :
  des centaines d'objets par seconde, avec finaliseur natif. Les passages du ramasse-miettes
  gelaient l'image quelques dizaines de millisecondes — et d'autant plus en combat, où les chiffres
  de dégâts, les barres, les combos et le diagnostic s'affichent en même temps. Les styles sont
  maintenant mis en cache (clé : taille, graisse, alignement, retour à la ligne) et partagés ; ceux
  qui les modifiaient reçoivent une variante dédiée.
- **Le recul de caméra par à-coup.** Chaque coup porté ajoutait jusqu'à ~5° et 7 cm à la caméra
  EN UNE IMAGE, puis les reprenait en décroissant : un saut d'image, pas un choc. `CameraPunch` est
  devenu un **ressort à amortissement critique** (pas exact, stable à tout pas de temps) : une
  impulsion y devient une vitesse, la vue monte en ~50 ms, culmine et revient sans dépasser. Les
  amplitudes sont aussi plafonnées (4°, 3,5 cm) et le coup porté ne donne plus qu'un à deux degrés.
- **Le coût du rendu.** Le MSAA ×8 imposait le rendu avant : chaque objet redessiné par lampe (dix
  au plus, les autres « par sommet », d'où un éclairage qui saute), et le reflet planaire refaisait
  toute la scène en pleine résolution. Retour au rendu différé avec **FXAA** par défaut, reflet en
  demi-résolution, et **synchronisation verticale** activée (sans elle, l'image se déchire en bandes
  quand la vue bouge vite).

Et un mouchard, `CameraDiagnostics`, sur la caméra : il compare à chaque image la rotation du rig
(tout ce qui sépare la tête de la caméra) et le déplacement du corps à ce que la souris et la vitesse
expliquent ; au-delà, il nomme le nœud coupable dans la Console. L'overlay F1 affiche images par
seconde, pire image et passages du ramasse-miettes. Si quelque chose saute encore, on saura quoi.

### 22.2 La « flashbang » était la dalle

Le matériau de la dalle (le shader de néon) gardait sa couleur blanc bleuté : la teinte et
l'atténuation étaient envoyées dans `_EmissionColor`, une propriété que ce shader ne lit pas. Un
rectangle presque blanc couvrait un quart de l'image à chaque sortie du téléphone, avant que
l'interface — sombre — ne s'affiche par-dessus. La dalle est maintenant du verre noir à peine teinté,
l'interface a un thème sombre, et la luminosité (4 crans) et un mode nuit se règlent dans l'appli
Réglages : c'est un voile sur tout l'écran, texte compris, comme sur un vrai téléphone.

### 22.3 Un système, des applis

`PhoneOS` porte l'état (appli ouverte, sélection, fils de messages, réglages) ; `PhoneDisplay` dessine
(accueil avec fond, horloge, grille d'icônes dessinées en rectangles et disques, pastilles de
notification ; puis chaque appli) ; `PhoneCamera` est l'appareil photo. L'histoire garde la main sur
ce qui compte : quand elle affiche un écran, l'OS ouvre l'appli correspondante. Une validation
(installer, accepter) passe désormais par un événement `PhoneDevice.StoryConfirmed`, émis seulement
si l'écran de l'histoire est RÉELLEMENT affiché au début de l'image : ouvrir la galerie avec Entrée
ne peut plus accepter une course. Téléphone sorti, E lui appartient (les portes ne réagissent plus).

L'appareil photo, façon téléphone de jeu en monde ouvert : le téléphone monte à l'œil puis
disparaît, les mains sortent du champ, l'image entière devient le viseur (grille des tiers, cadre de
mise au point qui dit qui est visé et s'il est au sol, zoom à la molette par le champ de vision avec
une visée ralentie d'autant, cinq filtres par le post-traitement). Le déclencheur fait un vrai rendu
de la caméra dans une texture (post-traitement compris, sans l'interface), qui file en vignette et
rejoint la Galerie. La preuve n'est envoyée au client que quand l'histoire en attend une.

### 22.4 Les mains étaient construites en miroir

Paume vers le bas (−Y), doigts vers l'avant (+Z) : le pouce d'une main droite est du côté −X. Le
signe était inversé depuis le début, dans le squelette comme dans la peau : chaque main était une
main de l'autre côté, le pouce du poing droit sortait vers l'extérieur, et aucune prise ne pouvait
avoir l'air naturelle. Corrigé aux deux endroits.

La prise du téléphone a ensuite été **calculée** : une reproduction exacte de la main (squelette,
peau, courbures) et du téléphone, une recherche aléatoire puis affinée sur 22 paramètres (angle et
position de la main, trois courbures par doigt, rotation de la base du pouce) sous contraintes —
aucun point de doigt dans l'appareil, bouts des doigts sur le bord gauche juste devant la face,
paume portant le dos, pouce posé en bas de l'écran — puis le rendu depuis la caméra du joueur pour
choisir. `HandRig.SetPoseOverride` applique ces courbures doigt par doigt ; une fermeture globale ne
pouvait pas les produire.

### 22.5 Les coups : la vrille

La garde est passée aux **poings verticaux** (pouces en haut et vers l'intérieur). Les coups partent
de là et tournent le poing à plat à l'impact (direct, crochet), ou paume vers soi pour l'uppercut :
c'est la vrille, qui distingue un coup d'un bras qui se tend. La main libre remonte au menton dans la
même orientation, le buste tourne davantage sur le direct arrière. La version des données d'attaque
passe à 5 : les assets existants sont régénérés à la reconstruction.

### 22.6 Le début d'une bagarre

`FightIntro` filme avec la caméra d'observation (qui a déjà sa chaîne d'image) : bandes noires, plan
large tournant, gros plan en légère contre-plongée avec la carte de l'adversaire, contre-plongée
large, retour en vue subjective sur « BAGARRE ! » et un coup sourd. Les adversaires sont tenus et les
commandes coupées pendant ce temps ; chaque plan vérifie qu'aucun mur ne s'intercale. Les affichages
de jeu se cachent (`FightIntro.AnyPlaying`).

`FightCrowd` fait venir les badauds : une place tirée autour du combat et vérifiée (pas dans un mur,
pas dans une voiture), un point de départ derrière elle dont le trajet est dégagé, un départ décalé
pour chacun, la marche animée par la locomotion procédurale et posée sur le sol (trottoir, bordure),
un ralentissement à l'arrivée. Arrivé, le badaud devient un `Spectator` : il regarde le combat et
réagit aux coups. Le brouhaha naît avec le premier arrivé.

### 22.7 Un vrai compilateur

Le code est désormais compilé à chaque étape par Roslyn (le compilateur C# officiel), contre les
assemblages de référence d'Unity 2021.3 et un environnement .NET 8 récupérés sur NuGet — jeu et
éditeur. Seules les API apparues après 2021.3 (`linearVelocity`) sont renommées le temps de la
vérification.

## 23. Un jeu qui s'ouvre, des bagarres qui se parlent, une maison qui existe

Demande : *« un menu dans le jeu avec les graphismes, et un menu pour lancer le jeu ; la maison un peu
plus détaillée ; quand on sélectionne les factures, une animation puis les lettres à l'écran ; hors
combat, les mains pas en position de combat, pas de dash, pas de barre de vie ; la voiture sur une
allée comme une maison normale ; du dialogue avant la baston, comme les sous-titres du début ; les
bleus en texture là où on tape, pas un ovale collé ; et comment intégrer des modèles 3D. »*

### 23.1 Être en combat, ou pas

`CombatPresence` (sur le joueur) répond à une seule question : **est-on en train de se battre ?** Oui
si un adversaire vivant, hostile et ENGAGÉ (cerveau allumé, rien ne le retient) est à moins de 11 m,
ou si on vient soi-même de frapper, garder ou encaisser (6 s de rémanence). La réponse est lissée
(`Weight` : la garde monte en ~0,2 s, redescend en ~0,8 s) et tout le reste la lit : les mains
(`FirstPersonHands.RelaxedWeight` mélange la garde et une pose bras le long du corps), l'esquive et la
glissade (`PlayerCombat` les refuse hors combat), le HUD (`GuiKit.Alpha` porte la visibilité), les
barres au-dessus des têtes, et les PNJ (`EnemyAvatarDriver` baisse les bras tant que le cerveau dort).

### 23.2 Le face-à-face

Le moteur d'étapes a gagné deux verbes : `During(action)` (appelé à chaque image de l'étape) et
`SkipWhen(condition)` (étape sautée si la condition est vraie en y entrant). Un face-à-face est donc une
étape ordinaire (`PrologueDirector.FaceOffBeat`) : `Freeze`, bandes noires (`FightIntro.Bars`, que la
cinématique reprend sans à-coup), répliques doublées, et pendant ce temps `FaceOff` fait avancer les
adversaires au pas (`EnemyMotor.SetMoveIntent`, sans IA) jusqu'à distance de conversation et glisse le
regard du joueur vers eux (`PlayerLook.SetLookAngles`, amorti). `SkipWhen` sur « le joueur a frappé le
premier ». Le combat lui-même démarre à l'étape suivante, qu'il y ait eu dialogue ou non.

### 23.3 Le courrier

`LetterReader` dessine tout en IMGUI avec trois textures générées (papier, poche d'enveloppe entaillée
en V, rabat triangulaire) : l'enveloppe monte, le rabat se retourne, la lettre sort pliée en trois
(format DL), se déplie en A4, puis le texte **s'imprime**. L'astuce du texte qui s'écrit sans que les
mots sautent d'une ligne à l'autre : on affiche toujours le texte complet, dont la fin est rendue
transparente par une balise `<color=#00000000>` — la mise en page est celle du texte final dès la
première lettre. Le montant apparaît, puis le tampon tombe (échelle 1,9 → 1 en 110 ms, choc sourd).

`ModalScreen` : un écran plein (lettre, menu) se déclare, et l'interaction (E) et la touche du
téléphone se taisent tant qu'il est ouvert. Un verrou par propriétaire, comme les verrous d'entrée.

### 23.4 La maison et l'allée

Le toit à deux pans demandait une forme qu'Unity ne fournit pas : un **prisme triangulaire**
(`NightMeshFactory.Gable`, une face par sommet pour garder les arêtes franches) ferme les pignons ; les
pans sont des boîtes inclinées dont la face inférieure passe exactement par le haut des murs. Le reste
est de la composition (`HouseBuilder` : `Roof`, `Door`, `Porch`, `Window`, `InteriorWalls`, `Sofa`…).
La voiture est posée sur une allée le long du flanc droit, le nez vers le fond du terrain ; la portière
conducteur (côté maison) porte l'interaction. La clôture a enfin un collider par pan : entre deux
poteaux, on traversait le grillage. Une rue, un trottoir et les voisins ferment le décor, bordés de
murs invisibles.

### 23.5 Les bleus dans la peau

Les ovales en relief (une sphère aplatie par coup, accrochée à l'os) ont disparu. `BruiseSystem`
convertit au démarrage chaque matière du corps vers le shader `UberBagarre/Peau` (une copie par matière
d'origine et par combattant, propriétés recopiées), puis, à chaque coup, pousse dans un
`MaterialPropertyBlock` propre au morceau touché jusqu'à huit marques : centre dans l'espace de l'objet,
rayon en mètres, intensité, graine de forme. Le shader mesure la distance **après application de
l'échelle de l'objet** (les morceaux de corps sont des maillages unitaires étirés : sans ça, un bleu
rond deviendrait une ellipse sur le torse), la déforme par deux octaves de bruit, et multiplie l'albédo
par une teinte de halo et une teinte de cœur. Peau (nom de matière contenant *skin* / *peau*) : violet ;
le reste : trace sombre. Le point d'impact est reprojeté sur l'ellipsoïde inscrit dans la boîte du
maillage (le point transporté par le coup est sur la zone touchable, plus large que le corps), et la
marque est posée sur tous les morceaux qu'elle touche. Limite connue : sur un modèle importé à
**maillage déformé** (SkinnedMeshRenderer), l'espace de l'objet est celui du personnage entier ; une
marque sur un avant-bras qui bouge glisserait. Le prototype n'a que des morceaux rigides.

### 23.6 Le menu

`GameMenu` porte les deux écrans : le titre (au lancement de la scène d'histoire, `PrologueDirector`
n'y démarre plus seul) et la pause (Échap). Le titre filme la maison avec la caméra de cinéma, en
travelling aller-retour entre deux points posés par `HouseBuilder` ; la musique est synthétisée à
l'ouverture (quatre accords, nappes désaccordées, basse pulsée, fondus aux changements).

La pause arrête `Time.timeScale` et `AudioListener.pause` — mais une bonne partie du jeu compte en
temps réel (dialogues, cinématique, téléphone, lettres, ralenti d'impact) : ces `Update` testent
`GameMenu.IsPaused` et rendent la main. Le ralenti d'impact en particulier aurait sinon remis le temps à
1 derrière le menu. Au retour, le temps reprend à `HitStop.BaseTimeScale` (le ralenti de triche survit).

Les réglages graphiques passent par `GraphicsDirector` (déjà sauvegardé) ; le menu ajoute la qualité
Unity, le plein écran, la résolution, le champ de vision, la sensibilité (`PlayerLook.UserSensitivity`,
distincte du multiplicateur que le zoom de l'appareil photo remet à 1), le volume et le compteur
d'images. Tout est dessiné en IMGUI, avec des widgets maison (barre cliquable et glissable, flèches),
et piloté au clavier comme à la souris.

### 23.7 Des modèles 3D à la place des formes générées

Voir `Docs/MODELE_3D.md`, complété : personnage **Humanoid** (Mixamo, etc.) branché par l'outil
existant, objets rigides (voiture, meubles) posés à la place des boîtes générées. Les bleus suivent
d'office, puisque la conversion des matières se fait au démarrage.

## 24. Tomber pour de vrai, parler en babil, sortir soi-même son téléphone, un menu qui bouge

Demande : *« le menu paraît trop calme, et entre les menus des animations ; abandonne les voix, elles
sont bizarres, juste des blablabla ; c'est moi qui sors mon téléphone, avec une appli lançable ; et
l'animation quand on tombe est mauvaise, la caméra passe derrière notre crâne. »*

### 24.1 La chute vue de l'intérieur

Deux défauts se cumulaient. `KnockdownSystem` calculait la bascule du corps avec les signes inversés :
rotation positive autour de X = la tête part vers l'AVANT (c'est aussi le sens du tangage d'une caméra
qui regarde vers le bas), donc un coup de face faisait tomber vers l'attaquant, face contre terre ; les
deux signes sont désormais ceux de l'opposé du coup. Et la caméra du joueur, au lieu de suivre la tête,
descendait tout droit d'un mètre et s'inclinait d'une fraction de la bascule, pendant que le corps se
couchait autour des pieds : les yeux restaient au-dessus des chevilles, le corps allongé à côté, et on
regardait l'intérieur de sa nuque.

La caméra subit maintenant **exactement** la bascule du corps (exposée par `KnockdownSystem.Tilt`),
autour du même pivot, dans le repère du joueur : sa position est `pivot + bascule × (œil − pivot)`, sa
rotation `bascule × tangage`, ramenées dans le repère de la tête. Pendant le relevé, l'œil descend avec
le bassin qui plie. Un lancer de sphère depuis l'œil debout arrête la tête contre un mur, et un rayon
vers le bas la garde à 20 cm au-dessus du sol. Le repère des mains (`HandsAimAnchor`) prend la même
bascule : la garde tombe avec le corps, serrée (`PlayerAvatarDriver` la force au sol) — on voit ses
poings devant son visage, sur fond de ciel. Le mouchard de caméra ignore la chute.

### 24.2 Le babil

Les fichiers de voix et leur générateur sont retirés (ils restent dans l'historique git).
`DialogueVoice` ne fait plus que du babillage, mais un babillage qui lit le texte : syllabes par groupe
de voyelles (digrammes français compris : ou, eu, au, ai, oi ; e muet de fin de mot sauté), attaque
tirée de la consonne précédente (occlusive = silence puis éclat de bruit filtré au lieu
d'articulation ; nasale = murmure grave ; liquide = glissement de formants ; fricative = souffle),
trois formants par voyelle, source glottale adoucie plus souffle, sous-harmonique pour les voix qui
grognent, bips carrés sur une gamme pour l'appli. L'intonation est calculée par phrase (déclinaison,
remontée des questions, élan des exclamations, accent aléatoire en fin de mot). Les résonateurs sont
normalisés en gain au sommet : sans ça, un « i » (premier formant très bas) sortait cinq fois plus fort
qu'un « a ».

### 24.3 Le téléphone reste dans la poche

L'histoire ne choisit plus l'appli à la place du joueur. `PhoneOS.SyncStory` ne fait plus `Open(...)` :
il **notifie** (`Notify(app, titre, texte)` : vibration, pastille, et un toast si le téléphone est déjà
en main) et attend. `PhoneDisplay` dessine la notification en bandeau quand le téléphone est rangé,
puis en pastille. Sortir le téléphone rouvre l'appli où on l'avait laissé, sauf qu'un appel prend
l'écran et qu'une notification en attente ramène à l'accueil, le curseur sur l'appli concernée. Les
`Raise()` du scénario ont disparu, un appel ne sort plus le téléphone tout seul, les objectifs disent
quoi faire (« sors ton téléphone (T), ouvre Über Bagarre et accepte »), et le prologue attend que le
joueur ait ouvert l'appli (`PhoneShowsStory`) avant qu'elle se présente.

### 24.4 Le menu qui bouge

`GameMenu.Show` ne change plus de page d'un coup : l'ancienne file vers la droite en 0,16 s
(`_leaving`), puis la nouvelle entre ligne par ligne, décalées de 45 ms. Sur l'écran titre, la caméra
de cinéma a un cadre par page (`_zoom` amorti : plus près et un peu tourné dans Chapitres et
Graphismes), le nom s'allume lettre par lettre comme un néon (grésillement à l'allumage, clignotement
rare, saut rouge-cyan toutes les huit secondes), la pluie tombe à l'écran et une voiture passe toutes
les 17 s. La musique est devenue un groove à 92 BPM (batterie, basse syncopée, nappe), toujours
synthétisé au lancement, et chaque transition a son souffle.


## 25. Des corps humains, des coups qui arrivent

Demande : « refais tout le combat », pas arcade, des animations dignes de Spider-Man 2, des coups qui
touchent vraiment, une caméra qui suit le mouvement de la frappe, des bras musclés réalistes et des pouces
qui ne ressemblent pas à des saucisses. Carte blanche sur le code et les modèles.

### 25.1 Le corps : MakeHuman, recuit pour le jeu

Les sites d'assets (Sketchfab, Mixamo, Poly Haven…) sont inaccessibles depuis l'environnement de
fabrication ; le dépôt public de MakeHuman l'est, et ses données (maillage hm08, cibles de morphologie,
squelette, poids) sont **CC0**. `Tools/corps/` en tire un corps de jeu complet, sans aucun outil 3D :

- **Morphologie** : les réglages macro de MakeHuman recalculés comme `apps/human.py` (genre, âge,
  muscle, poids, taille, proportions, origines) plus des cibles locales (biceps, avant-bras, deltoïdes,
  pectoraux, dorsaux, V, abdominaux, cou). Échelle choisie pour que les **yeux** tombent à la hauteur
  voulue : 1,62 m pour le joueur, soit exactement la caméra.
- **Squelette du jeu** (`rig.py`) posé sur les articulations MakeHuman (centroïdes de sommets) avec les
  noms attendus par le code. Conventions : +Z le long des os de membre ; pour le bras et l'avant-bras
  +X est **l'axe réel du coude** ; main et doigts +Y dos de la main, fermeture = rotation autour de +X ;
  pied à plat (sinon la locomotion relèverait les orteils de 18° au premier pas). Os `Yeux` (ancrage
  caméra et poses) et `Knuckles` (point de frappe). Poids : les 163 os MakeHuman fusionnés sur les
  nôtres (4 influences), lissés sur la main (plis en lame au creux du pouce).
- **Le poing** : fermetures et resserrement des doigts, et la pose du pouce trouvée par **optimisation**
  (scipy, Powell) : bout du pouce sur la phalange moyenne du majeur, articulation sur celle de l'index,
  pénalité d'interpénétration mesurée sur les sommets. Résultat : pouce en travers, par-dessus.
- **Vêtements** (`vetements.py`) : découpés dans le « collant » d'aide du maillage (qui suit déjà toutes
  les morphologies), bords recalés sur leur ligne de coupe, lissés, décollés de la peau d'une épaisseur
  minimale (avec garde-fou de distance : sinon une manche loin du jean était projetée à 30 cm), drapés
  (le tee-shirt tombe des pectoraux et droit sous la taille, le jean tombe droit du genou), ourlés
  (tranche + revers intérieur). Chaque face de peau porte un masque des vêtements qui la cachent.
- **Subdivision** Catmull-Clark maison (numpy), poids et UV interpolés. **Peau** peinte texel par texel
  depuis la position anatomique (atlas rasterisé en 3D) : veines, ongles, lunules, jointures, coudes,
  genoux, joues, oreilles, lèvres, sourcils, crâne rasé, barbe, creux ; relief en espace tangent
  dérivé d'une carte de hauteur dans l'espace des UV (donc aligné sur les tangentes d'Unity).

Côté Unity, `CorpsImporter` lit le format `UBCORPS2` (gzip), assemble par **tenue** un maillage qui
ne garde que la peau visible et les vêtements portés (sommets compactés, 32 bits d'index), range la
position de repos dans le 3e canal d'UV (bleus), et enregistre le maillage dans `Corps/Generes`.
`FighterBuilder` reconstruit le squelette depuis les positions de liaison et câble tout le reste avec
les mêmes composants qu'avant (`IkLimb`, `HandRig`, `BodyRig`, `ProceduralLocomotion`, hitbox). Le
joueur est reculé pour que ses yeux soient sur la caméra : il a un corps, sans tête.

**`IkLimb` en mode charnière.** L'ancien alignement (`FromToRotation`) laissait le roulis des os libre :
invisible sur des cylindres, catastrophique sur une peau (avant-bras vrillé, coude tordu). Le mode
charnière oriente bras et avant-bras autour de l'axe du coude mesuré en pose de liaison (repli sur le
pôle, même signe, fondu quand le bras se tend), et un **os de torsion** encaisse la moitié de la
rotation du poignet, mesurée depuis la pose de liaison (la main de liaison est à 92° de l'avant-bras).
`HandRig` reçoit une rotation « poing » par doigt (resserrement, pivot du pouce).

### 25.2 Le coup

- **`AttackData.Sample`** : Hermite à tangentes de Steffen (monotones) au lieu d'un smoothstep par
  segment. La vitesse traverse les clés ; le poing ne ralentit qu'aux vrais retournements.
- **`StrikeTarget`** : point de surface visé — zone sous le réticule, sinon adversaire le plus proche
  devant (±55°, 2,7 m). Tête : la mâchoire (centre du crâne − 12 cm + 7 cm vers l'attaquant) ; corps :
  point de capsule le plus proche d'un point 28 cm sous l'épaule, 3 cm dedans. En garde : sur
  l'avant-bras le plus proche de la trajectoire.
- **`AttackExecutor`** (réécrit) : guidage du poignet vers la cible (écart pose écrite ↔ cible, borné
  à 45 cm, poids nul au départ, plein à l'impact, rendu au retour ; suivi complet pour le joueur, 30 %
  pour l'IA) ; élan (`IImpulseReceiver`) calculé pour couvrir l'écart à l'instant d'impact, portée
  comptée avec la rotation du buste ; **gel de contact** (50 ms, 85 ms lourd, allongé par la charge et
  le contre) pendant lequel le geste n'avance plus, puis retour accéléré au lieu de traverser ; armement
  tenu pour l'IA (`_telegraph`) ; montée d'intensité sur l'enchaînement ; ralentis cinéma (contre, K.O.).
- **`FirstPersonHands`** : nouvelle garde (poings au menton, coudes bas) calée sur un bras de 52 cm ;
  **l'épaule s'avance** (rotation de clavicule, 7 cm max) quand la cible dépasse la portée.
- **`CameraPunch`** : pilotage à pleine échelle (11° / 9 cm max) + suivi de la vitesse du poing
  (roulis, lacet, tangage) + coup de zoom et zoom tenu, toujours par ressorts critiques (aucun saut
  d'image) ; le champ de vision du menu et de l'appareil photo est respecté (on n'ajoute qu'un écart).
- **`HitStop`** : vrai arrêt sur les coups lourds (0,45 pendant 55 ms max) et ralenti cinéma à entrée
  et sortie adoucies.
- **`ImpactEffects`** : gouttelettes étirées (sueur ; sang sur gros coup au visage), bouffée de tissu,
  éclair de 60 ms — systèmes de particules fabriqués à l'exécution, branchés sur tous les dégâts.
- **`ImpactAudio`** : claque (bruit clair), masse (grave qui plonge), craquement (impulsions) ;
  trois variantes par poids de coup.
- **Zones touchables collées aux os** (`BoneFollower`) : sphère autour du crâne, capsule du torse,
  capsule des jambes. Le poing guidé les touche au moment où il arrive sur la peau.
- **HUD non arcade** : chiffres de dégâts et barres au-dessus des têtes coupés par défaut
  (`WorldHealthBar.ArcadeHud`), interrupteur dans le menu de triche.

## 26. Les animations capturées de FS Melee Combat System

Demande : intégrer le visuel du paquet acheté (animations, mort…), en gardant NOTRE combat.

### 26.1 Ce qui est pris, ce qui ne l'est pas

Extrait du `.unitypackage` : les clips à mains nues (`Melee Combat System/Animations/Unarmed`), ceux
de `Combat Core/Animations` (réactions gauche/droite, chutes avant/arrière, relevé), la marche et
l'attente de `Third Person Controller`, les trois FBX dont les clips copient l'avatar
(`avatarSetup: CopyFromOther`), et les bruitages. **Aucun script** du paquet (pas de second système
de combat, pas de dépendance), et son `manifest.json` n'écrase pas le nôtre.

Réglage d'import des clips (dans les `.meta`) : rotation et hauteur **cuites** dans la pose,
déplacement horizontal **extrait** et calculé sur le **centre de masse**
(`loopBlendPositionXZ: 0`, `keepOriginalPositionXZ: 0`). Mesuré dans Blender, le crochet au corps
avance le bassin de 1,9 m et la réaction à l'uppercut le recule de 1,3 m : cuit dans la pose, le
corps partirait loin de sa capsule puis reviendrait d'un coup à la fin du clip.

### 26.2 Rejouer sur nos squelettes : l'avatar humanoïde

`CorpsAvatarBuilder` construit un avatar par silhouette. Unity retarget des « muscles », pas des
rotations d'os : il lui faut la correspondance des os (Clavicle → Shoulder, Forearm → LowerArm,
Ankle → Foot…) et la **T-pose** du corps. MakeHuman livre un corps bras écartés, paumes vers les
cuisses : la rotation la plus courte de chaque segment vers sa direction de T-pose est exactement une
abduction, qui amène les paumes vers le sol et les plis des coudes vers l'avant (vérifié sur les
données : axe du coude vertical, dos de la main en haut, pouce devant). Les pieds sont redressés
(5° d'ouverture). Les doigts ne sont pas confiés à l'avatar : `HandRig` ferme les poings.

### 26.3 `MocapDriver` : nos décisions, leurs mouvements

Un graphe Playables par combattant (pas de contrôleur d'animation à maintenir) :

- couche 0, **déplacements** : garde, 4 pas chassés, attente et marche détendues ; poids tirés de la
  vitesse réelle de la racine, lecture calée sur la vitesse de chaque clip (1,66 m/s avant/arrière,
  1,34 m/s de côté) ; « engagé » = cerveau actif et pas de `HoldAll` ;
- couche 1, **garde haute** (masque haut du corps) quand `GuardSystem.IsGuarding` ;
- couche 2, **action** en deux emplacements fondus : coup, réaction, chute, relevé, esquive, mort.

Branchements, sans que le combat sache qu'il est animé :

- `AttackExecutor.AttackStarted` → clip du coup (bibliothèque `MocapLibrary` : attaque, côté,
  tête/corps, membre, instant d'impact mesuré). **Vitesse = impact du clip / `ImpactDelay`** de
  l'exécuteur (armement de l'IA compris). `SideResolver` fait jouer l'uppercut du gauche (seul
  capturé). Pendant le **gel de contact** (`InContact`) le clip s'arrête.
- `CorrectStrike` (LateUpdate) : IK du bras animé vers `StrikeTarget` — l'écart jointures ↔ cible,
  borné à 38 cm, pèse le carré d'une montée douce jusqu'à 1 à l'impact, puis se relâche ; pôle = coude
  de l'animation ; fondu avec les rotations animées pour ne pas imposer le roulis de l'IK. L'os de
  torsion de l'avant-bras est recalé à chaque image (`IkLimb.UpdateTwist`).
- `Combatant.Damaged` → réaction : riposte → contre ; uppercut ; lourd ; sinon légère. Parmi les
  clips d'une famille, celui dont la **tête part dans le sens du coup** — mesuré au démarrage en posant
  le corps sur chaque clip (`graph.Evaluate`). Coup bloqué → `Block Hit` ; au sol → frappe au sol.
- `KnockdownSystem` : `KnockedDown` → chute avant/arrière selon `LastFallDirection`, et
  `SetTimings` cale la durée au sol sur le clip ; `GettingUp` → relevé. La bascule d'un bloc du
  système de chute est coupée (`TiltEnabled`).
- `DeathRagdoll` → `PlayDeath` : mort animée (le ragdoll reste le secours).
- `DodgeSystem.Dodged` en arrière → retrait du buste.
- **Déplacement des clips** : `MocapRootMotion` (sur le corps, là où Unity le donne) le passe au
  pilote, qui le rend à la capsule (`CharacterController.Move`, donc collisions) : 25 % pour un coup
  (notre élan couvre déjà la distance), 100 % pour réactions, chutes, relevé ; rien pour la marche.
- **Bascule** mocap ↔ corps calculé (coup sans clip, menu de triche) : `ProceduralLocomotion` et
  `FirstPersonHands` sont mis en veille / réveillés, et un **fondu de pose** de 0,18 s part de la
  dernière pose affichée (chaque image du fondu repart de la liaison, pour qu'un os écrit par un seul
  des deux systèmes retrouve sa pose).

`MocapLibraryBuilder` range les clips et les bruitages (`ImpactAudio` : banques d'échantillons,
`PlayFall` branché sur `KnockedDown` par `CombatFeedbackRelay`). `SandboxSceneBuilder.AddMocap` pose
l'Animator (désactivé, le pilote l'allume), le pilote et le relais sur chaque combattant de
`BuildFighter` : bac à sable, prologue, fosse.

### 26.4 Limites connues

- Impossible de lancer Unity ici : compilé hors ligne contre les références Unity, clips et
  squelettes analysés dans Blender, mais **pas vu tourner**. Si un clip se comporte mal : menu de
  triche, « ANIMATIONS CAPTUREES », pour revenir aux poses calculées ; `_log` sur `MocapDriver`
  trace chaque coup (clip, vitesse, impact).
- Pas de clip pour le coup de pied bas, le coup de tête, la bousculade : joués par le corps calculé.
- Le relevé capturé part du dos : après une chute sur le ventre, le fondu retourne le corps.
- Le joueur reste en première personne calculée (ses bras, sa garde, ses coups) : c'est voulu.

## 27. Les mains du joueur, la photo du K.O., le monde ouvert

Demande : « prends les animations des mains du pack », « les photos quand le gars meurt ne marchent
pas », « maintenant fais une map open world avec tout ça ».

### 27.1 La photo du K.O.

La mort animée (§26) ne construisait plus le ragdoll : le corps n'avait plus AUCUN collider (zones
touchables coupées, capsule partie). Le viseur (`PhoneCamera`, un lancer de sphère) passait à travers
et la preuve était impossible. `DeathRagdoll.AddCorpseShapes` pose, à la mort animée, des
déclencheurs cinématiques sur les os (bassin, torse, tête, membres) : ils suivent la chute, ne poussent
rien, et le lancer (qui voit les déclencheurs) les trouve.

### 27.2 Les bras du joueur, en gestes capturés

Pas d'Animator sur un corps vu de l'intérieur : la caméra suivrait la tête du boxeur capturé, qui
tourne de 90° et avance de 40 cm à chaque jab. Les clips sont lus hors ligne dans Blender
(`Tools/mocap/extrait_fs.py`) et réduits (`Tools/mocap/cuisson_bras.py`) à ce que verraient SES
YEUX, regard tenu sur la cible : position du poignet par rapport aux yeux, orientation de la main
reconstruite comme nos os (`rig.py` : doigts → +Z, dos de la main → +Y), en coordonnées Unity. Mesuré :
le crochet capturé arrive à 14–25° de l'orientation qu'on avait réglée à la main.

- Les armés des clips FS sont faits pour être LUS de loin (le poing du cross passe 20 cm derrière la
  tête, celui du crochet 54 cm) : la piste commence au poing armé, notre garde tient lieu d'armé.
- `MocapArms` (IHandMotion) rejoue la piste : écart depuis NOTRE garde, amplitude calée sur l'impact
  des poses écrites du coup, orientation fondue de la garde vers la capture ; l'avant de l'impact sur
  la fenêtre de frappe, la suite du geste sur le reste ; la main libre bouge un peu (35 %) et reste en
  garde ; miroir pour l'autre main (x → −x, quaternion (x, −y, −z, w)).
- `AttackExecutor` : `_handMotionSource` ; s'il fournit un geste, il remplace la trajectoire écrite du
  poing — ciblage, guidage (`_designImpact` = impact du geste), élan, gel de contact, caméra et buste
  restent ceux de l'exécuteur.
- Directs et crochets seulement : l'uppercut et les coups au corps capturés partent de la hanche,
  tête plongée — rendus en vue subjective, ils ne ressemblaient à rien (vérifié en images).
- Les adversaires : `MocapDriver` pilote la fermeture de leurs `HandRig` (poing en combat, 0,28 au
  repos, 0,3 au sol) — la forme du poing reste la nôtre (pouce en travers).

### 27.3 Le monde ouvert

`OpenWorldSceneBuilder` (menu 3b) assemble `MondeOuvert.unity` en réutilisant tout : la rue du Vertigo
(`NightStreetBuilder.Build(palette, openWorld: true)` : sans les pâtés qui la fermaient ni la ville
peinte), la planque (`HouseBuilder`, murs invisibles « Limite » retirés), le parking (tourné de 180°
derrière le club), la salle (à l'écart, derrière `DoorPortal`), le joueur, le téléphone, le menu, la
triche.

**`CityBuilder`** : quadrillage (boulevard = rue du Vertigo prolongée, rues Nord / Sud, 4 avenues),
20 îlots (les 6 du centre sont des lieux ou des pâtés ; ceux du bord ferment la ville). Tout passe par
**`CityMeshBuilder`** : volumes fusionnés en un maillage par matière et par îlot, **UV en mètres**
projetées sur chaque face (une dalle, une brique, un étage ont la même taille partout), collisions en
boîtes. Le sol est une grille qui évite les sols des lieux (rue, parking, planque). Immeubles le long
des côtés de chaque îlot (largeur, profondeur, hauteur tirées avec une graine fixe), socle en béton,
corniche, machines sur le toit, vitrines + enseigne néon + lampe sur les rues passantes ; pavillons à
toit à deux pentes autour de la planque ; square (herbe, allées, arbres, bancs, lampes). Réservés
(aucun immeuble) : les lieux, et des passages garantis (ruelle du club, sortie de la rue de la planque).
Lampadaires tous les 26 m (projecteur sans ombre), barrières au bout des rues, mur de tours aux
limites, tours lointaines, voitures garées (deux modèles fusionnés, recopiés).

**Exécution** (`Scripts/World`) :
- `OpenWorldDirector` : attente → commande (fiche réécrite via `MissionBriefing.Configure`, écran
  Accueil, bandeau) → acceptée (cible tirée d'un modèle inactif, cerveau éteint, GPS) → engagement à
  6,5 m ou au premier coup (présentation, badauds) → au sol : écran Photo (seul écran où l'appareil
  envoie) → paiement (`PlayerProgress.CompleteContract`), nettoyage du corps quand on est loin. K.O. du
  joueur : réveil à la planque, 15 % d'hôpital. Récupération hors combat. Filet sous la ville.
- `CityMap` : mini-carte (nord en haut), grande carte (M, nouvelle entrée `openMap`), repère GPS à
  l'écran (au bord s'il est hors champ), colonne de lumière sur la cible.
- `MocapWalker` : passants sur le tour de leur îlot, marche/attente capturées (Playables),
  s'arrêtent devant quelqu'un, pressent le pas près d'une bagarre (`Combatant.AnyDamaged`).
- `TrafficCar` : boucles horaires sur la voie de droite (le boulevard encombré du Vertigo exclu),
  ralentit dans les virages, freine pour ce qui bouge (balayage), passe après 4 s bloquée par une
  voiture, klaxonne le joueur.
- `DistanceCuller` : lampes éteintes au-delà de 75 m (têtes émissives toujours allumées, halos
  volumétriques compris puisqu'ils testent `isActiveAndEnabled`).
- `DoorPortal` : porte = fondu, déplacement par ISpawnReceiver, lieu allumé/éteint ; la coroutine
  tourne sur le fondu (la porte de sortie s'éteint avec la salle pendant le passage).

Caméra du jeu à 700 m (la ville fait 400 m), menu principal : entrée « MONDE OUVERT ».

### 27.4 Limites connues

- Toujours pas vu tourner dans Unity : compilé hors ligne, plan de la ville et gestes des bras
  vérifiés en images (Python / Blender). Les réglages (densité, hauteurs, vitesses) sont des
  premiers jets.
- Pas de conduite : les voitures roulent, on ne les prend pas (encore).
- Les passants traversent le petit mobilier de la rue du Vertigo (pas de collision entre eux et le
  décor) ; ils suivent le tour de leur îlot, sans traverser les rues.

