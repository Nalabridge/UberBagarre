using System;
using UberBagarre.Player;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.Phone
{
    /// <summary>
    /// Le téléphone : l'objet le plus important du jeu.
    ///
    /// Tout le concept passe par lui. On ne trouve pas les bagarres, on les REÇOIT — comme une
    /// course. Le téléphone n'est donc pas une interface posée devant la caméra, c'est un objet
    /// tenu en main : il occupe de la place, il s'incline, il éclaire les mains dans le noir, il
    /// apparaît dans les flaques de la rue. C'est cette présence physique qui fait la différence
    /// entre « un menu » et « mon téléphone ».
    ///
    /// Deux conséquences de conception, toutes les deux volontaires :
    ///
    /// - **On peut marcher en le regardant.** C'est la vérité du concept, et c'est aussi ce qui
    ///   évite de figer le joueur à chaque notification.
    /// - **On ne peut pas frapper en le tenant.** Sortir le téléphone en plein combat est donc
    ///   une décision, pas un réflexe — exactement comme dans la rue.
    ///
    /// L'écran, lui, est dessiné par PhoneDisplay : ce composant ne s'occupe que de l'état et
    /// du mouvement. Mélanger les deux donnerait un fichier où changer l'inclinaison du poignet
    /// demande de relire la mise en page d'une fiche de mission.
    /// </summary>
    [DefaultExecutionOrder(95)]
    public class PhoneDevice : MonoBehaviour
    {
        /// <summary>Les écrans du téléphone, dans l'ordre du prologue.</summary>
        public enum Screen
        {
            /// <summary>Verrouillé : heure, date, batterie. L'état par défaut.</summary>
            Verrouille = 0,

            /// <summary>Quelqu'un appelle.</summary>
            AppelEntrant = 1,

            /// <summary>Appel en cours.</summary>
            EnAppel = 2,

            /// <summary>Le lien reçu par message, avec son avertissement.</summary>
            Lien = 3,

            /// <summary>Installation en cours.</summary>
            Installation = 4,

            /// <summary>Accueil de l'application.</summary>
            Accueil = 5,

            /// <summary>Fiche du suspect : signalement, localisation, antécédents.</summary>
            Cible = 6,

            /// <summary>Mission acceptée : la directive en cours.</summary>
            Mission = 7,

            /// <summary>Mode appareil photo, pour la preuve.</summary>
            Photo = 8,

            /// <summary>Mission validée : paiement, expérience, avis du client.</summary>
            Valide = 9,

            /// <summary>Profil : niveau, expérience, réputation, avis, capacités.</summary>
            Profil = 10
        }

        [Header("References")]
        [SerializeField]
        [Tooltip("Repere de la main : en pratique la camera. Les deux poses sont exprimees dedans.")]
        private Transform _anchor;

        [SerializeField]
        [Tooltip("Le quad de l'ecran. PhoneDisplay projette son contour pour y dessiner l'interface.")]
        private Transform _screenTransform;

        [SerializeField]
        [Tooltip("Rendu de l'ecran : sa couleur d'emission suit ce qui est affiche.")]
        private Renderer _screenRenderer;

        [SerializeField]
        [Tooltip("Petite lampe a hauteur d'ecran. C'est elle qui eclaire les mains dans le noir " +
                 "et qui fait exister le telephone comme objet plutot que comme menu.")]
        private Light _screenLight;

        [SerializeField] private PlayerInputReader _input;

        [SerializeField]
        [Tooltip("Les mains du personnage : la main droite tient le telephone quand il est leve.")]
        private FirstPersonHands _hands;

        [Header("Poses")]
        [SerializeField] private Vector3 _loweredPosition = new Vector3(0.17f, -0.31f, 0.20f);
        [SerializeField] private Vector3 _loweredEuler = new Vector3(74f, 14f, -22f);

        [SerializeField]
        [Tooltip("Pose levee. L'inclinaison reste FAIBLE : l'interface est dessinee sur le contour " +
                 "projete de l'ecran, et une approximation affine ne suit exactement qu'un " +
                 "parallelogramme. Au-dela de quelques degres, le dessin deborderait du cadre.")]
        private Vector3 _raisedPosition = new Vector3(0.062f, -0.058f, 0.25f);

        [SerializeField] private Vector3 _raisedEuler = new Vector3(3f, -2.5f, -3.5f);

        [SerializeField, Min(1f)] private float _raiseSpeed = 7.5f;

        [Header("Vie")]
        [SerializeField, Min(0f)]
        [Tooltip("Amplitude du balancement au repos, en metres.")]
        private float _swayAmount = 0.004f;

        [SerializeField, Min(0f)] private float _swaySpeed = 1.3f;

        [SerializeField, Min(0f)]
        [Tooltip("Amplitude de la vibration pendant une sonnerie.")]
        private float _ringShake = 0.006f;

        [Header("Ecran")]
        [SerializeField] private Screen _screen = Screen.Verrouille;

        [SerializeField]
        [Tooltip("Le telephone peut-il etre sorti ? Coupe pendant les transitions.")]
        private bool _available = true;

        [SerializeField]
        [Tooltip("L'appli UBER BAGARRE est-elle installee ? Le prologue commence sans elle.")]
        private bool _appInstalled = true;

        private float _raise;
        private bool _wantRaised;
        private bool _ringing;
        private string _caller = "INCONNU";
        private float _callTime;
        private float _downloadProgress;
        private float _screenAge;

        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock _block;
        private Renderer[] _renderers;
        private PhoneOS _os;
        private bool _hidden;
        private float _cameraBlend;

        /// <summary>Levé pour de bon (au-delà de la moitié de la course).</summary>
        public bool IsRaised { get { return _raise > 0.5f; } }

        /// <summary>0 = rangé, 1 = complètement levé. PhoneDisplay s'en sert pour son opacité.</summary>
        public float RaiseAmount { get { return _raise; } }

        public Screen Current { get { return _screen; } }
        public bool IsRinging { get { return _ringing; } }
        public string Caller { get { return _caller; } }
        public float CallTime { get { return _callTime; } }
        public float ScreenAge { get { return _screenAge; } }
        public Transform ScreenTransform { get { return _screenTransform; } }

        /// <summary>
        /// L'OS affiche-t-il en ce moment l'écran que l'histoire attend ? Posé par PhoneOS à
        /// chaque image. Une validation (E) ne compte que dans ce cas : ouvrir une autre appli
        /// avec E ne doit jamais accepter une course par accident.
        /// </summary>
        public bool ShowingStoryScreen { get; set; }

        /// <summary>Mode appareil photo plein écran : le téléphone et les mains sortent du champ.</summary>
        public bool CameraMode { get; set; }

        /// <summary>Luminosité de l'écran, 0 à 1 (réglages du téléphone).</summary>
        public float ScreenBrightness { get; set; }

        public bool AppInstalled
        {
            get { return _appInstalled; }
            set { _appInstalled = value; }
        }

        public float DownloadProgress
        {
            get { return _downloadProgress; }
            set { _downloadProgress = Mathf.Clamp01(value); }
        }

        public bool Available
        {
            get { return _available; }
            set
            {
                _available = value;
                if (!_available) _wantRaised = false;
            }
        }

        /// <summary>Déclenché quand le joueur répond à un appel.</summary>
        public event Action Answered;

        /// <summary>Déclenché quand une photo est prise, avec la cible visée si elle est valide.</summary>
        public event Action<Transform> PhotoTaken;

        /// <summary>
        /// Le joueur valide l'écran de l'histoire (installer le lien, accepter une course) depuis
        /// l'OS : Entrée ou clic gauche. E passe toujours, lui, par la touche d'interaction.
        /// </summary>
        public event Action StoryConfirmed;

        public void ConfirmStory()
        {
            if (StoryConfirmed != null) StoryConfirmed();
        }

        // ------------------------------------------------------------------ commandes

        public void Raise()
        {
            if (!_available) return;
            _wantRaised = true;
        }

        public void Lower()
        {
            _wantRaised = false;
        }

        public void Toggle()
        {
            if (_wantRaised) Lower();
            else Raise();
        }

        public void SetScreen(Screen screen)
        {
            if (_screen == screen) return;

            _screen = screen;
            _screenAge = 0f;
        }

        /// <summary>Fait sonner le téléphone. Il se lève tout seul : une sonnerie qu'on peut rater n'en est pas une.</summary>
        public void Ring(string caller)
        {
            _caller = string.IsNullOrEmpty(caller) ? "INCONNU" : caller;
            _ringing = true;
            _callTime = 0f;

            SetScreen(Screen.AppelEntrant);
        }

        public void Answer()
        {
            if (!_ringing) return;

            _ringing = false;
            _callTime = 0f;
            SetScreen(Screen.EnAppel);

            if (Answered != null) Answered();
        }

        public void HangUp()
        {
            _ringing = false;
            SetScreen(Screen.Verrouille);
        }

        /// <summary>Prend une photo. Renvoie la cible visée, ou null si le cadre est vide.</summary>
        public Transform TakePhoto(Transform aimed)
        {
            if (PhotoTaken != null) PhotoTaken(aimed);
            return aimed;
        }

        // ------------------------------------------------------------------ cycle

        private void OnDisable()
        {
            // Un telephone desactive ne doit pas laisser les poings bloques derriere lui.
            if (_input != null) _input.SetCombatLock(this, false);
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _os = GetComponent<PhoneOS>();
            if (ScreenBrightness <= 0f) ScreenBrightness = 0.6f;

            if (_anchor == null)
            {
                Camera camera = Camera.main;
                if (camera != null) _anchor = camera.transform;
            }
        }

        private void Update()
        {
            _screenAge += Time.unscaledDeltaTime;

            if (_input != null && _input.PhonePressed && _available) Toggle();

            // Scene sans systeme de telephone (generee avant lui) : E, telephone leve, valide
            // l'ecran affiche, comme avant.
            if (_os == null && !_ringing && _input != null && _input.InteractPressed && IsRaised) ConfirmStory();

            // Un appel sort le téléphone tout seul. Le joueur n'a pas à deviner qu'une touche
            // existe au moment précis où le jeu lui apprend qu'elle existe.
            if (_ringing)
            {
                _callTime += Time.unscaledDeltaTime;
                _wantRaised = _available;

                if (_input != null && _input.InteractPressed && IsRaised) Answer();
            }
            else if (_screen == Screen.EnAppel)
            {
                _callTime += Time.unscaledDeltaTime;
            }

            float target = _wantRaised ? 1f : 0f;
            _raise = Mathf.MoveTowards(_raise, target, _raiseSpeed * Time.unscaledDeltaTime);

            ApplyScreenLight();
            ApplyCombatLock();
        }

        /// <summary>
        /// La pose est appliquée APRÈS les secousses et le balancement de la caméra (ordre 95) :
        /// placé dans Update, le téléphone avait une image de retard sur la vue et tremblait
        /// pendant les combats.
        /// </summary>
        private void LateUpdate()
        {
            // Mode photo : le telephone monte vers l'oeil puis disparait, les mains descendent.
            // L'image devient le viseur, comme sur un vrai telephone tenu a bout de bras.
            float cameraTarget = CameraMode && _wantRaised ? 1f : 0f;
            _cameraBlend = Mathf.MoveTowards(_cameraBlend, cameraTarget, Time.unscaledDeltaTime * 5f);

            ApplyPose();
            ApplyGrip();
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            bool hide = _cameraBlend > 0.6f;
            if (hide == _hidden || _renderers == null) return;

            _hidden = hide;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _renderers[i].enabled = !hide;
            }
        }

        // La prise, doigt par doigt : (base, milieu, bout) en degres, dans l'ordre index, majeur,
        // annulaire, auriculaire, pouce. Trouvee par optimisation sur une reproduction exacte de
        // la main et du telephone (aucun doigt ne traverse l'appareil, les bouts de doigts
        // enveloppent le bord gauche, la paume porte le dos, le pouce se pose en bas de l'ecran),
        // puis verifiee au rendu depuis la camera du joueur.
        private static readonly Vector3[] GripCurls =
        {
            new Vector3(-0.1f, 21.9f, 60.5f),
            new Vector3(1.2f, 53.9f, 83.9f),
            new Vector3(18f, 100f, 62.7f),
            new Vector3(42.2f, 85.4f, 40.6f),
            new Vector3(26.9f, 80f, 72f)
        };

        private static readonly Vector3 GripThumb = new Vector3(23.1f, 75.6f, 1.8f);

        /// <summary>
        /// La main droite tient le téléphone comme on tient le sien : l'appareil en travers de la
        /// paume, les doigts enroulés autour du bord gauche (on en voit le bout), le talon de la
        /// main et le pouce en bas à droite. Avant, le poignet était sous le téléphone, les doigts
        /// pointés vers le haut, et une fermeture globale les faisait passer À TRAVERS l'écran.
        /// </summary>
        private void ApplyGrip()
        {
            if (_hands == null) return;

            _hands.Lowered = Mathf.SmoothStep(0f, 1f, _cameraBlend);

            float weight = Mathf.SmoothStep(0f, 1f, _raise) * (1f - _cameraBlend);
            if (weight <= 0.001f)
            {
                _hands.SetHold(HandSide.Right, transform.position, transform.rotation, 0f, 0f);
                return;
            }

            Vector3 right = transform.right;
            Vector3 up = transform.up;
            Vector3 back = transform.forward;

            // Les doigts partent en diagonale vers le haut a gauche ; le dos de la main regarde
            // a l'oppose de l'ecran, legerement roule.
            const float slant = 51.4f * Mathf.Deg2Rad;
            const float roll = 6.35f * Mathf.Deg2Rad;

            Vector3 fingers = -right * Mathf.Cos(slant) + up * Mathf.Sin(slant);
            Vector3 handBack = back * Mathf.Cos(roll) + Vector3.Cross(back, fingers) * Mathf.Sin(roll);
            Quaternion rotation = Quaternion.LookRotation(fingers, handBack);

            // Les articulations des doigts, posees derriere le telephone ; le poignet s'en deduit.
            Vector3 knuckles = transform.position - right * 0.03f - up * 0.06f + back * 0.023f;
            Vector3 wrist = knuckles - rotation * new Vector3(0f, -0.005f, 0.068f);

            _hands.SetHold(HandSide.Right, wrist, rotation, weight, GripCurls, GripThumb);
        }

        private void ApplyPose()
        {
            if (_anchor == null) return;

            // Courbe d'accélération : un déplacement linéaire entre deux poses se lit comme un
            // objet téléporté image par image. Le lissage en cosinus donne le poids du poignet.
            float t = Mathf.SmoothStep(0f, 1f, _raise);

            Vector3 position = Vector3.Lerp(_loweredPosition, _raisedPosition, t);
            Quaternion rotation = Quaternion.Slerp(Quaternion.Euler(_loweredEuler),
                Quaternion.Euler(_raisedEuler), t);

            // Vers l'oeil en mode photo.
            float lift = Mathf.SmoothStep(0f, 1f, _cameraBlend);
            position = Vector3.Lerp(position, new Vector3(0f, -0.01f, 0.16f), lift);

            // Balancement de repos. Il n'existe que quand le téléphone est levé : une main
            // baissée hors du champ n'a aucune raison de respirer.
            float time = Time.time;
            Vector3 sway = new Vector3(
                Mathf.Sin(time * _swaySpeed) * _swayAmount,
                Mathf.Sin(time * _swaySpeed * 1.37f + 1.1f) * _swayAmount * 0.75f,
                0f) * t;

            if (_ringing)
            {
                // Vibration : haute fréquence, faible amplitude, et coupée dès qu'on décroche.
                sway += new Vector3(
                    Mathf.Sin(time * 47f) * _ringShake,
                    Mathf.Sin(time * 61f) * _ringShake,
                    0f) * t;
            }

            transform.position = _anchor.TransformPoint(position + sway);
            transform.rotation = _anchor.rotation * rotation;
        }

        private void ApplyScreenLight()
        {
            Color tint = ScreenTint(_screen);
            float power = _raise * _raise * (1f - _cameraBlend);
            float brightness = Mathf.Clamp01(ScreenBrightness);

            if (_screenLight != null)
            {
                // Faible : a quelques centimetres des mains, une lampe plus forte les brulait et
                // le bloom faisait du telephone un flash en pleine nuit.
                _screenLight.color = tint;
                _screenLight.intensity = 0.14f * power * (0.4f + 0.6f * brightness);
                _screenLight.enabled = power > 0.02f;
            }

            if (_screenRenderer == null) return;

            // La dalle elle-meme est du VERRE SOMBRE. C'etait elle, la « flashbang » : le
            // materiau de neon gardait sa couleur blanc bleute (la teinte etait envoyee dans une
            // propriete que le shader ne lit pas), soit un rectangle presque blanc qui couvrait
            // un quart de l'image a chaque sortie du telephone, avant que l'interface — sombre —
            // ne s'affiche par-dessus. L'interface est dessinee par PhoneDisplay ; la dalle n'a
            // qu'a etre noire et a peine teintee.
            _screenRenderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, new Color(0.025f, 0.026f, 0.034f) + tint * 0.018f);
            _block.SetFloat(IntensityId, Mathf.Lerp(0.3f, 1f, power));
            _screenRenderer.SetPropertyBlock(_block);
        }

        /// <summary>Chaque écran a sa dominante, et la lampe la reprend : la couleur du reflet
        /// sur les mains dit ce qui est affiché avant même qu'on ait lu.</summary>
        private static Color ScreenTint(Screen screen)
        {
            switch (screen)
            {
                case Screen.AppelEntrant: return new Color(0.42f, 1f, 0.55f);
                case Screen.EnAppel: return new Color(0.55f, 0.88f, 1f);
                case Screen.Installation: return new Color(1f, 0.72f, 0.28f);
                case Screen.Cible: return new Color(1f, 0.42f, 0.38f);
                case Screen.Mission: return new Color(1f, 0.30f, 0.62f);
                case Screen.Photo: return new Color(0.85f, 0.92f, 1f);
                case Screen.Valide: return new Color(0.48f, 1f, 0.62f);
                case Screen.Profil: return new Color(1f, 0.78f, 0.36f);
                case Screen.Verrouille: return new Color(0.70f, 0.78f, 0.95f);
                default: return new Color(1f, 0.36f, 0.66f);
            }
        }

        /// <summary>
        /// Téléphone en main = pas de coup. Le déplacement, lui, reste libre : marcher en
        /// regardant son écran est la posture même du personnage.
        /// </summary>
        private void ApplyCombatLock()
        {
            if (_input == null) return;

            _input.SetCombatLock(this, _raise >= 0.35f);
        }
    }
}
