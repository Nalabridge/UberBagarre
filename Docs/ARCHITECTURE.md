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
