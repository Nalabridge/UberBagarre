using UberBagarre.UI;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// L'heure de la ville. Une minute de jeu passe en une seconde (une journée en vingt-quatre
    /// minutes, comme dans Schedule I) : le soleil se lève, traverse le ciel, se couche ; les
    /// lampadaires et les fenêtres s'allument le soir et s'éteignent au matin.
    ///
    /// Elle pilote le curseur jour / nuit du rendu (<see cref="GraphicsDirector.Day"/>) et la
    /// course du soleil (<see cref="TimeOfDay.SunAngles"/>). On se réveille le matin : une nuit
    /// de sommeil remet l'horloge à 7 h 30.
    /// </summary>
    public class WorldClock : MonoBehaviour
    {
        [SerializeField] private GraphicsDirector _graphics;
        [SerializeField] private TimeOfDay _time;
        [SerializeField] private OpenWorldDirector _director;

        [SerializeField, Range(0f, 24f)]
        [Tooltip("Heure au lancement.")]
        private float _hour = 8f;

        [SerializeField, Min(0f)]
        [Tooltip("Minutes de jeu par seconde reelle.")]
        private float _minutesPerSecond = 1f;

        [SerializeField, Range(0f, 24f)] private float _wakeHour = 7.5f;

        private float _appliedDay = -1f;
        private float _appliedNight = -1f;
        private float _nextApply;

        public static WorldClock Instance { get; private set; }

        /// <summary>L'heure (0 à 24).</summary>
        public float Hour { get { return _hour; } }

        /// <summary>« 08:15 ».</summary>
        public string Label
        {
            get
            {
                int minutes = Mathf.FloorToInt(_hour * 60f) % (24 * 60);
                return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
            }
        }

        /// <summary>0 la nuit, 1 en plein jour.</summary>
        public float Daylight { get { return DaylightAt(_hour); } }

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            if (_director != null) _director.Slept += OnSlept;
            Apply(true);
        }

        private void OnDisable()
        {
            if (_director != null) _director.Slept -= OnSlept;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Après les réglages rechargés du rendu : c'est l'horloge qui décide de l'heure.
            Apply(true);
        }

        private void OnSlept()
        {
            SetHour(_wakeHour);
        }

        public void SetHour(float hour)
        {
            _hour = Mathf.Repeat(hour, 24f);
            Apply(true);
        }

        private void Update()
        {
            // Le temps ne passe pas quand le jeu est en pause (menu, écran de magasin ouvert…).
            if (!GameMenu.IsOpen && !ModalScreen.Active && Time.timeScale > 0f)
            {
                _hour = Mathf.Repeat(_hour + Time.deltaTime * _minutesPerSecond / 60f, 24f);
            }

            if (Time.unscaledTime >= _nextApply) Apply(false);
        }

        private void Apply(bool force)
        {
            _nextApply = Time.unscaledTime + 0.25f;

            float day = DaylightAt(_hour);
            if (force || Mathf.Abs(day - _appliedDay) > 0.004f)
            {
                _appliedDay = day;
                if (_time != null) _time.SunAngles = SunAngles(_hour);
                if (_graphics != null) _graphics.Day = day;
                else if (_time != null) _time.Day = day;
            }
            else if (_time != null)
            {
                // Le soleil avance même en plein jour (les ombres tournent).
                _time.SunAngles = SunAngles(_hour);
            }

            // Lampadaires et fenêtres : allumés quand le jour baisse.
            float night = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.65f, day));
            if (force || Mathf.Abs(night - _appliedNight) > 0.02f)
            {
                _appliedNight = night;
                MapStreamer.SetNight(night);
            }
        }

        /// <summary>La lumière du jour selon l'heure : aube de 5 h 30 à 8 h, crépuscule de 18 h 30 à 21 h.</summary>
        public static float DaylightAt(float hour)
        {
            if (hour < 5.5f || hour > 21f) return 0f;
            if (hour < 8f) return Mathf.SmoothStep(0f, 1f, (hour - 5.5f) / 2.5f);
            if (hour > 18.5f) return Mathf.SmoothStep(1f, 0f, (hour - 18.5f) / 2.5f);
            return 1f;
        }

        /// <summary>
        /// La course du soleil : il se lève à l'est vers 6 h, culmine à 60° à midi, se couche à
        /// l'ouest vers 18 h. Jamais sous 6° (une ombre infinie ne ressemble à rien).
        /// </summary>
        public static Vector3 SunAngles(float hour)
        {
            float t = Mathf.Clamp01((hour - 5.5f) / 15.5f);
            float elevation = Mathf.Max(6f, 60f * Mathf.Sin(Mathf.PI * t));
            float azimuth = Mathf.Lerp(-100f, 100f, t);
            return new Vector3(elevation, azimuth, 0f);
        }

        public void Configure(GraphicsDirector graphics, TimeOfDay time, OpenWorldDirector director, float hour)
        {
            _graphics = graphics;
            _time = time;
            _director = director;
            _hour = hour;
        }
    }
}
