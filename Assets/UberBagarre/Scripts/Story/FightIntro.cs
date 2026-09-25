using System;
using UberBagarre.Combat;
using UberBagarre.Enemy;
using UberBagarre.Player;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.Story
{
    /// <summary>
    /// La mini-cinématique d'avant-combat : quatre secondes où la caméra quitte les yeux du
    /// joueur pour montrer à qui il a affaire.
    ///
    /// Un combat qui commence parce qu'une IA s'allume n'a pas de début. Celui-ci en a un :
    /// les bandes noires descendent, la caméra tourne autour des deux hommes pendant que les
    /// badauds s'approchent, un gros plan sur l'adversaire avec son nom en grand, un plan large
    /// par en dessous, puis retour dans les yeux du joueur sur un « BAGARRE ! » et un coup sourd.
    /// Personne ne bouge pendant ce temps (adversaires tenus, commandes coupées) ; on peut
    /// passer avec E, Espace ou un clic.
    ///
    /// La caméra de cinéma est celle d'observation (F3) : elle a déjà sa chaîne d'image
    /// (bloom, anticrénelage, lumière volumétrique), et une troisième caméra n'apporterait
    /// qu'une troisième chaîne à tenir à jour. Chaque plan vérifie qu'aucun mur ne se met entre
    /// l'objectif et le sujet : dans une rue étroite, sinon, on filmerait une façade.
    /// </summary>
    [DisallowMultipleComponent]
    public class FightIntro : MonoBehaviour
    {
        private const float ShotA = 1.5f;
        private const float ShotB = 3.1f;
        private const float ShotC = 3.9f;
        private const float End = 4.5f;

        [Header("Cameras")]
        [SerializeField] private Camera _gameCamera;
        [SerializeField] private Camera _cinematicCamera;

        [SerializeField]
        [Tooltip("La camera d'observation (F3) : coupee pendant la cinematique, pour ne pas bouger la vue.")]
        private ObserverCamera _observer;

        [Header("Joueur")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Transform _player;

        [SerializeField, Range(0f, 1f)] private float _volume = 0.6f;

        private bool _playing;
        private float _time;
        private int _shot = -1;
        private Combatant _opponent;
        private string _title;
        private string _subtitle;
        private Action _finished;
        private float _previousFov = 60f;
        private bool _previousHold;

        private AudioSource _audio;
        private AudioClip _whoosh;
        private AudioClip _boom;

        /// <summary>Une cinématique est-elle en cours ? Les affichages de jeu se cachent pendant ce temps.</summary>
        public static bool AnyPlaying { get; private set; }

        public bool IsPlaying
        {
            get { return _playing; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyPlaying = false;
        }

        private void Awake()
        {
            if (_player == null && _input != null) _player = _input.transform;

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _whoosh = Whoosh();
            _boom = Boom();
        }

        private void OnDisable()
        {
            if (_playing) Finish();
        }

        private void OnDestroy()
        {
            if (_whoosh != null) Destroy(_whoosh);
            if (_boom != null) Destroy(_boom);
        }

        /// <summary>
        /// Lance la présentation de <paramref name="opponent"/>. <paramref name="onFinished"/> est
        /// appelé à la fin (ou tout de suite si la cinématique ne peut pas se jouer).
        /// </summary>
        public void Play(Combatant opponent, string title, string subtitle, Action onFinished)
        {
            if (opponent == null || _player == null || _gameCamera == null || _cinematicCamera == null)
            {
                if (onFinished != null) onFinished();
                return;
            }

            if (_playing) Finish();

            _opponent = opponent;
            _title = string.IsNullOrEmpty(title) ? opponent.DisplayName : title;
            _subtitle = subtitle;
            _finished = onFinished;
            _time = 0f;
            _shot = -1;
            _playing = true;
            AnyPlaying = true;

            _previousHold = EnemyBrain.HoldAll;
            EnemyBrain.HoldAll = true;

            if (_input != null) _input.SetGameplayLock(this, true);
            if (_observer != null) _observer.enabled = false;

            _previousFov = _cinematicCamera.fieldOfView;
            _gameCamera.enabled = false;
            _cinematicCamera.enabled = true;

            Update();
        }

        private void Update()
        {
            if (!_playing) return;

            _time += Time.unscaledDeltaTime;

            if (_time > 0.6f && SkipPressed()) _time = Mathf.Max(_time, ShotC);

            if (_time >= ShotC)
            {
                // Retour dans les yeux du joueur : le « BAGARRE ! » s'affiche encore un instant.
                if (_cinematicCamera.enabled)
                {
                    _cinematicCamera.enabled = false;
                    _gameCamera.enabled = true;
                    Play(_boom, 1f);
                }

                if (_time >= End) Finish();
                return;
            }

            int shot = _time < ShotA ? 0 : _time < ShotB ? 1 : 2;
            if (shot != _shot)
            {
                _shot = shot;
                if (shot > 0) Play(_whoosh, 0.6f);
            }

            PoseCamera(shot);
        }

        private bool SkipPressed()
        {
            if (_input == null || _input.Provider == null || _input.Bindings == null) return false;

            return _input.InteractPressed ||
                   _input.Provider.GetPressedThisFrame(_input.Bindings.jump) ||
                   _input.Provider.GetPressedThisFrame(_input.Bindings.attackStraight) ||
                   _input.Provider.GetPressedThisFrame(_input.Bindings.phoneSelect);
        }

        private void Finish()
        {
            _playing = false;
            AnyPlaying = false;

            if (_cinematicCamera != null)
            {
                _cinematicCamera.enabled = false;
                _cinematicCamera.fieldOfView = _previousFov;
            }

            if (_gameCamera != null) _gameCamera.enabled = true;
            if (_observer != null) _observer.enabled = true;
            if (_input != null) _input.SetGameplayLock(this, false);

            EnemyBrain.HoldAll = _previousHold;

            Action finished = _finished;
            _finished = null;
            if (finished != null) finished();
        }

        // --------------------------------------------------------------- plans

        private void PoseCamera(int shot)
        {
            Vector3 player = _player.position;
            Vector3 opponent = _opponent.transform.position;
            Vector3 head = _opponent.AimPosition;

            Vector3 axis = opponent - player;
            axis.y = 0f;
            float distance = Mathf.Max(0.5f, axis.magnitude);
            axis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, axis);

            Vector3 middle = (player + opponent) * 0.5f + Vector3.up * 1.25f;

            Vector3 eye;
            Vector3 look;
            float fov;

            switch (shot)
            {
                case 0:
                {
                    // Plan large tournant : les deux hommes face a face, la foule qui arrive.
                    float t = Mathf.SmoothStep(0f, 1f, _time / ShotA);
                    float angle = Mathf.Lerp(-55f, -18f, t) * Mathf.Deg2Rad;
                    float radius = Mathf.Max(3.6f, distance * 1.6f + 1.8f);

                    eye = middle + (side * Mathf.Cos(angle) + axis * Mathf.Sin(angle)) * radius + Vector3.up * 0.55f;
                    look = middle;
                    fov = 50f;
                    break;
                }

                case 1:
                {
                    // Gros plan sur l'adversaire, legerement par en dessous : il domine.
                    float t = Mathf.SmoothStep(0f, 1f, (_time - ShotA) / (ShotB - ShotA));
                    float back = Mathf.Lerp(1.5f, 1.2f, t);

                    eye = head - axis * back + side * 0.32f - Vector3.up * 0.16f;
                    look = head + Vector3.up * 0.02f;
                    fov = 38f;
                    break;
                }

                default:
                {
                    // Plan large en contre-plongee, de l'autre cote.
                    float t = Mathf.SmoothStep(0f, 1f, (_time - ShotB) / (ShotC - ShotB));
                    eye = middle - side * Mathf.Lerp(3.4f, 3.0f, t) + axis * 0.4f - Vector3.up * 0.75f;
                    look = middle + Vector3.up * 0.1f;
                    fov = 55f;
                    break;
                }
            }

            eye = Unblock(look, eye);

            Transform camera = _cinematicCamera.transform;
            camera.position = eye;
            camera.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
            _cinematicCamera.fieldOfView = fov;
        }

        /// <summary>Rapproche l'objectif si un mur (pas un personnage) se glisse entre lui et le sujet.</summary>
        private static Vector3 Unblock(Vector3 subject, Vector3 eye)
        {
            RaycastHit hit;
            Vector3 direction = eye - subject;
            float length = direction.magnitude;
            if (length < 0.01f) return eye;

            if (!Physics.Raycast(subject, direction / length, out hit, length, ~0, QueryTriggerInteraction.Ignore)) return eye;

            // Les corps (combattants, badauds) ne bouchent pas la vue : on filme a travers la foule.
            if (hit.collider.GetComponentInParent<Combatant>() != null) return eye;
            if (hit.collider.GetComponent<CharacterController>() != null) return eye;
            if (hit.collider is CapsuleCollider) return eye;

            return subject + direction / length * Mathf.Max(0.6f, hit.distance - 0.3f);
        }

        // --------------------------------------------------------------- affichage

        private void OnGUI()
        {
            if (!_playing) return;

            float sw = UnityEngine.Screen.width;
            float sh = UnityEngine.Screen.height;
            float unit = sh / 1080f;

            // Bandes noires : elles descendent au debut, remontent a la fin.
            float bars = Mathf.Clamp01(_time / 0.35f) * Mathf.Clamp01((End - _time) / 0.35f);
            float height = sh * 0.11f * Mathf.SmoothStep(0f, 1f, bars);
            GuiKit.Fill(new Rect(0f, 0f, sw, height), Color.black);
            GuiKit.Fill(new Rect(0f, sh - height, sw, height), Color.black);

            Color outline = new Color(0f, 0f, 0f, 0.9f);

            // Carte de l'adversaire pendant le gros plan.
            if (_time >= ShotA && _time < ShotB)
            {
                float t = Mathf.Clamp01((_time - ShotA) / 0.3f) * Mathf.Clamp01((ShotB - _time) / 0.25f);
                float slide = (1f - Mathf.SmoothStep(0f, 1f, t)) * 80f * unit;

                GUIStyle name = GuiKit.Style(Mathf.Max(14, Mathf.RoundToInt(74f * unit)), FontStyle.Bold, TextAnchor.MiddleLeft);
                GUIStyle line = GuiKit.Style(Mathf.Max(11, Mathf.RoundToInt(26f * unit)), FontStyle.Bold, TextAnchor.MiddleLeft);

                Rect band = new Rect(90f * unit - slide, sh * 0.62f, sw * 0.6f, 150f * unit);
                GuiKit.Fill(new Rect(band.x - 20f * unit, band.y + 10f * unit, 8f * unit, band.height - 20f * unit),
                    new Color(1f, 0.2f, 0.55f, t));

                GuiKit.OutlinedLabel(new Rect(band.x, band.y, band.width, 90f * unit), _title, name,
                    new Color(1f, 1f, 1f, t), new Color(0f, 0f, 0f, 0.9f * t), 2f);

                if (!string.IsNullOrEmpty(_subtitle))
                {
                    GuiKit.OutlinedLabel(new Rect(band.x + 4f * unit, band.y + 88f * unit, band.width, 40f * unit), _subtitle,
                        line, new Color(1f, 0.82f, 0.35f, t), new Color(0f, 0f, 0f, 0.9f * t), 1.5f);
                }
            }

            // « BAGARRE ! » au retour dans les yeux du joueur.
            if (_time >= ShotC)
            {
                float t = (_time - ShotC) / (End - ShotC);
                float pop = 1f + (1f - Mathf.Clamp01(t * 5f)) * 0.35f;
                float alpha = Mathf.Clamp01((1f - t) * 2.2f);

                GUIStyle big = GuiKit.Style(Mathf.Max(16, Mathf.RoundToInt(130f * unit * pop)), FontStyle.Bold, TextAnchor.MiddleCenter);
                GuiKit.OutlinedLabel(new Rect(0f, sh * 0.5f - 110f * unit, sw, 220f * unit), "BAGARRE !", big,
                    new Color(1f, 0.92f, 0.85f, alpha), new Color(0.35f, 0f, 0.08f, 0.95f * alpha), 3f);
            }

            if (_time > 0.6f && _time < ShotC)
            {
                GUIStyle hint = GuiKit.Style(Mathf.Max(10, Mathf.RoundToInt(18f * unit)), FontStyle.Normal, TextAnchor.MiddleRight);
                GuiKit.OutlinedLabel(new Rect(sw - 420f * unit, sh - height * 0.5f - 15f * unit, 390f * unit, 30f * unit),
                    "E / ESPACE : passer", hint, new Color(1f, 1f, 1f, 0.55f), outline, 1f);
            }
        }

        // --------------------------------------------------------------- sons

        private void Play(AudioClip clip, float volume)
        {
            if (_audio != null && clip != null) _audio.PlayOneShot(clip, volume * _volume);
        }

        private const int SampleRate = 22050;

        /// <summary>Souffle de coupe : un bruit filtré dont la hauteur monte puis retombe.</summary>
        private static AudioClip Whoosh()
        {
            int length = Mathf.RoundToInt(0.45f * SampleRate);
            float[] data = new float[length];
            System.Random random = new System.Random(5);
            float low = 0f;
            float band = 0f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)length;
                float cutoff = Mathf.Lerp(0.02f, 0.25f, Mathf.Sin(t * Mathf.PI));
                float noise = (float)random.NextDouble() * 2f - 1f;

                low += (noise - low) * cutoff;
                band += (low - band) * 0.02f;

                data[n] = (low - band) * Mathf.Sin(t * Mathf.PI) * 0.9f;
            }

            AudioClip clip = AudioClip.Create("Coupe", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Coup sourd : une grosse caisse de cinéma, un sinus qui plonge.</summary>
        private static AudioClip Boom()
        {
            int length = Mathf.RoundToInt(0.9f * SampleRate);
            float[] data = new float[length];
            System.Random random = new System.Random(9);
            float phase = 0f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)SampleRate;
                float frequency = 38f + 90f * Mathf.Exp(-t / 0.05f);
                phase += frequency / SampleRate;
                phase -= Mathf.Floor(phase);

                float body = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t / 0.32f);
                float click = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t / 0.006f) * 0.5f;

                data[n] = (body + click) * 0.9f;
            }

            AudioClip clip = AudioClip.Create("Coup sourd", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
