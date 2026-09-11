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
