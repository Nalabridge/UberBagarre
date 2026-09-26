using System;
using System.Collections.Generic;
using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Les coups du JOUEUR joués avec les mouvements capturés du paquet FS Melee Combat System.
    ///
    /// On ne pose pas un Animator sur un corps vu de l'intérieur : la caméra suivrait la tête du
    /// boxeur capturé, qui tourne de 90 degrés et avance de 40 cm à chaque jab. Les clips ont donc
    /// été lus hors ligne (Tools/mocap) et réduits à ce que VERRAIENT SES YEUX, regard tenu sur la
    /// cible : la trajectoire de chaque poing et son orientation, image par image
    /// (Art/Animations/BrasCaptures.json).
    ///
    /// Ce composant rejoue ces pistes dans l'espace des bras :
    /// - le geste part de NOTRE garde (écart depuis le poing armé du clip ; l'armé lui-même, fait
    ///   pour être lu de loin — le poing passe derrière la tête — est laissé au clip) ;
    /// - son amplitude se cale sur l'impact des poses écrites du coup, puis l'exécuteur guide le
    ///   poing sur la cible comme avant ;
    /// - l'orientation du poing est celle de la capture (le crochet capturé arrive à 14–25 degrés
    ///   de celui qu'on avait réglé à la main) ;
    /// - l'avant de l'impact est calé sur la fenêtre de frappe du coup, l'après (la suite du geste,
    ///   le retour) sur le reste ; la main libre bouge un peu avec le corps et reste en garde.
    ///
    /// L'uppercut, les coups au corps et le coup par-dessus partent de sous la hanche, buste tourné
    /// de 90 degrés : vus de ses propres yeux, tels quels, ils ne ressemblent à rien. On n'en garde
    /// que la POUSSÉE (du point le plus bas jusqu'à l'impact), dont la courbe est tournée et mise à
    /// l'échelle pour aller de notre garde à l'impact écrit (pistes « fit ») ; le retour est un
    /// simple rappel vers la garde. Une main sans piste prend celle de l'autre main, en miroir.
    ///
    /// Hors des coups, trois boucles capturées s'ajoutent à la garde (<see cref="Ambient"/>) : le
    /// balancement des poings à la marche, calé sur le pas des jambes ; la garde fermée du
    /// blocage, qui se cale et respire ; l'encaisse, les avant-bras qui absorbent un coup bloqué.
    /// </summary>
    public class MocapArms : MonoBehaviour, IHandMotion
    {
        [Serializable]
        private class TrackFile
        {
            public int version;
            public Track[] tracks;
            public Loop[] loops;
        }

        [Serializable]
        private class Track
        {
            public string name;
            public string attack;
            public int side;
            public bool body;
            public bool fit;
            public float fps;
            public float impact;
            public int frames;
            public float[] strike;
            public float[] off;
        }

        /// <summary>Une boucle de garde : écarts des deux mains autour de leur pose moyenne.</summary>
        [Serializable]
        private class Loop
        {
            public string name;
            public float fps;
            public int frames;
            public float phase;
            public float[] left;
            public float[] right;
        }

        [SerializeField]
        [Tooltip("Pistes cuites par Tools/mocap/cuisson_bras.py.")]
        private TextAsset _tracks;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part du mouvement de la main libre gardee : le corps la bouge, la garde la retient.")]
        private float _offHandShare = 0.35f;

        [SerializeField] private Vector2 _amplitudeRange = new Vector2(0.35f, 1.3f);

        [SerializeField]
        [Tooltip("Pistes « fit » (poussee seule, recalee sur l'impact ecrit) : leur amplitude peut descendre plus bas.")]
        private Vector2 _fitAmplitudeRange = new Vector2(0.12f, 1.3f);

        [Header("Boucles de garde")]
        [SerializeField]
        [Tooltip("Pour l'encaisse : la garde du joueur, dont les blocages secouent les avant-bras.")]
        private GuardSystem _guard;

        [SerializeField, Range(0f, 1.5f)] private float _walkShare = 1f;
        [SerializeField, Range(0f, 1.5f)] private float _guardShare = 1f;
        [SerializeField, Range(0f, 1.5f)] private float _absorbShare = 1f;

        [SerializeField, Min(0.2f)]
        [Tooltip("Vitesse de lecture de l'encaisse : le clip est joue pour etre lu de loin, en vue subjective il traine.")]
        private float _absorbRate = 1.6f;

        [SerializeField] private bool _log;

        private Track[] _loaded;
        private readonly List<Track> _candidates = new List<Track>();
        private Track _track;
        private Track _previous;
        private bool _mirror;
        private float _scale = 1f;
        private float _impactTime = 0.4f;
        private Quaternion _fit = Quaternion.identity;

        private Loop _walkLoop;
        private Loop _guardLoop;
        private Loop _absorbLoop;
        private float _guardClock;
        private float _absorbTime = -1f;
        private float _absorbStrength;

        /// <summary>Vrai si les boucles de garde capturées sont disponibles et actives.</summary>
        public bool AmbientActive
        {
            get { return isActiveAndEnabled && MocapDriver.GloballyEnabled && (_walkLoop != null || _guardLoop != null); }
        }

        private void Awake()
        {
            Load();
        }

        private void OnEnable()
        {
            if (_guard != null) _guard.Blocked += OnBlocked;
        }

        private void OnDisable()
        {
            if (_guard != null) _guard.Blocked -= OnBlocked;
            _absorbTime = -1f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _guardClock += dt;

            if (_absorbTime >= 0f && _absorbLoop != null)
            {
                _absorbTime += dt * _absorbRate;
                if (_absorbTime * _absorbLoop.fps >= _absorbLoop.frames - 1) _absorbTime = -1f;
            }
        }

        private void OnBlocked(DamageInfo info)
        {
            if (_absorbLoop == null) return;

            // Un coup lourd secoue la garde en entier ; un jab bloqué la fait à peine bouger.
            float strength = info.IsHeavy ? 1f : Mathf.Clamp(0.45f + info.Amount * 0.03f, 0.45f, 0.85f);
            if (_absorbTime >= 0f && strength < _absorbStrength) return;

            _absorbStrength = strength;
            _absorbTime = 0f;
        }

        private void Load()
        {
            if (_loaded != null || _tracks == null) return;

            Loop[] loops;
            try
            {
                TrackFile file = JsonUtility.FromJson<TrackFile>(_tracks.text);
                _loaded = file != null && file.tracks != null ? file.tracks : new Track[0];
                loops = file != null ? file.loops : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UberBagarre] Pistes de bras illisibles (" + e.Message + ") : coups ecrits.", this);
                _loaded = new Track[0];
                return;
            }

            _walkLoop = FindLoop(loops, "marche");
            _guardLoop = FindLoop(loops, "garde");
            _absorbLoop = FindLoop(loops, "encaisse");
        }

        private static Loop FindLoop(Loop[] loops, string name)
        {
            if (loops == null) return null;

            for (int i = 0; i < loops.Length; i++)
            {
                Loop loop = loops[i];
                if (loop == null || loop.name != name || loop.frames < 2) continue;
                if (loop.left == null || loop.right == null) continue;
                if (loop.left.Length < loop.frames * 7 || loop.right.Length < loop.frames * 7) continue;
                return loop;
            }

            return null;
        }

        // ------------------------------------------------------------------ IHandMotion

        public bool Begin(AttackData attack, HandSide side, bool bodyShot, HandPose guard, Vector3 designImpact,
            float impactTime)
        {
            _track = null;
            if (!isActiveAndEnabled || !MocapDriver.GloballyEnabled || attack == null) return false;

            Load();
            if (_loaded == null || _loaded.Length == 0) return false;

            int wanted = side == HandSide.Left ? 0 : 1;
            _mirror = false;
            Collect(attack.name, wanted, bodyShot);
            if (_candidates.Count == 0)
            {
                _mirror = true;
                Collect(attack.name, 1 - wanted, bodyShot);
            }

            if (_candidates.Count == 0) return false;

            Track track = _candidates[UnityEngine.Random.Range(0, _candidates.Count)];
            if (_candidates.Count > 1 && track == _previous)
            {
                track = _candidates[(_candidates.IndexOf(track) + 1) % _candidates.Count];
            }

            _track = track;
            _previous = track;
            _impactTime = Mathf.Clamp(impactTime, 0.05f, 0.95f);

            // Amplitude : celle des poses écrites du coup, pour que le geste capturé tienne dans la
            // même garde et la même portée (le guidage vers la cible fait le reste).
            _fit = Quaternion.identity;
            Vector3 capturedImpact;
            Quaternion unused;
            Read(_track.strike, _track.impact, out capturedImpact, out unused);
            float captured = capturedImpact.magnitude;
            Vector3 wanted3 = designImpact - guard.position;
            float designed = wanted3.magnitude;
            Vector2 range = _track.fit ? _fitAmplitudeRange : _amplitudeRange;
            _scale = captured > 0.01f
                ? Mathf.Clamp(designed / captured, range.x, range.y)
                : 1f;

            // Poussée seule : sa courbe est tournée pour partir de la garde vers l'impact écrit.
            if (_track.fit && captured > 0.01f && designed > 0.01f)
            {
                _fit = Quaternion.FromToRotation(capturedImpact / captured, wanted3 / designed);
            }

            if (_log)
            {
                Debug.Log("[UberBagarre] Coup capture : " + _track.name + (_mirror ? " (miroir)" : "") + ", amplitude x" +
                          _scale.ToString("0.00"), this);
            }

            return true;
        }

        public void Sample(float normalized, HandPose strikeGuard, HandPose offGuard, out HandPose strike, out HandPose off)
        {
            strike = strikeGuard;
            off = offGuard;
            if (_track == null) return;

            float n = Mathf.Clamp01(normalized);
            float clip = ClipTime(n);

            // Le geste rend la main à la garde sur la fin : aucun saut quand le coup se termine.
            float fade = 1f - Smooth((n - 0.85f) / 0.15f);
            float turn = n <= _impactTime ? Smooth(n / (_impactTime * 0.8f)) : 1f;

            Vector3 delta;
            Quaternion rotation;
            Read(_track.strike, clip, out delta, out rotation);
            delta = _fit * delta;
            strike = new HandPose(strikeGuard.position + delta * (_scale * fade),
                Quaternion.Slerp(strikeGuard.Rotation, rotation, turn * fade).eulerAngles);

            Vector3 offDelta;
            Quaternion offRotation;
            Read(_track.off, clip, out offDelta, out offRotation);
            off = new HandPose(offGuard.position + offDelta * (_scale * _offHandShare * fade), offGuard.euler);
        }

        public Vector3 ImpactPosition(HandPose strikeGuard)
        {
            if (_track == null) return strikeGuard.position;

            Vector3 delta;
            Quaternion unused;
            Read(_track.strike, _track.impact, out delta, out unused);
            return strikeGuard.position + _fit * delta * _scale;
        }

        // ------------------------------------------------------------------ boucles de garde

        /// <summary>
        /// Ce que les boucles capturées ajoutent à la garde d'une main : un écart (espace des
        /// bras) et une petite rotation (à composer à gauche de celle de la garde).
        /// <paramref name="walkPhase"/> est la phase du pas (0 = pose du pied droit, comme
        /// <see cref="ProceduralLocomotion"/>), les poids vont de 0 à 1.
        /// </summary>
        public bool Ambient(HandSide side, float walkPhase, float walkWeight, float guardWeight,
            out Vector3 offset, out Quaternion turn)
        {
            offset = Vector3.zero;
            turn = Quaternion.identity;
            if (!AmbientActive) return false;

            bool left = side == HandSide.Left;

            if (_walkLoop != null && walkWeight > 0.001f)
            {
                float fraction = Mathf.Repeat(walkPhase + _walkLoop.phase, 1f);
                Accumulate(_walkLoop, left, fraction, walkWeight * _walkShare, ref offset, ref turn);
            }

            if (_guardLoop != null && guardWeight > 0.001f)
            {
                float length = (_guardLoop.frames - 1) / Mathf.Max(1f, _guardLoop.fps);
                float fraction = Mathf.Repeat(_guardClock / length, 1f);
                Accumulate(_guardLoop, left, fraction, guardWeight * _guardShare, ref offset, ref turn);
            }

            if (_absorbLoop != null && _absorbTime >= 0f)
            {
                float fraction = Mathf.Clamp01(_absorbTime * _absorbLoop.fps / (_absorbLoop.frames - 1));
                float weight = _absorbShare * _absorbStrength * (1f - Smooth((fraction - 0.75f) / 0.25f));
                Accumulate(_absorbLoop, left, fraction, weight, ref offset, ref turn);
            }

            return true;
        }

        private static void Accumulate(Loop loop, bool left, float fraction, float weight, ref Vector3 offset,
            ref Quaternion turn)
        {
            float[] data = left ? loop.left : loop.right;
            float f = Mathf.Clamp01(fraction) * (loop.frames - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), loop.frames - 2);
            float u = f - i;

            offset += Vector3.Lerp(Position(data, i), Position(data, i + 1), u) * weight;
            Quaternion q = Quaternion.Slerp(Rotation(data, i), Rotation(data, i + 1), u);
            turn = Quaternion.SlerpUnclamped(Quaternion.identity, q, weight) * turn;
        }

        // ------------------------------------------------------------------ pistes

        private void Collect(string attack, int side, bool body)
        {
            _candidates.Clear();

            for (int pass = 0; pass < 2 && _candidates.Count == 0; pass++)
            {
                for (int i = 0; i < _loaded.Length; i++)
                {
                    Track track = _loaded[i];
                    if (track == null || track.frames < 2 || track.attack != attack || track.side != side) continue;
                    if (pass == 0 && track.body != body) continue;
                    _candidates.Add(track);
                }
            }
        }

        /// <summary>Avant l'impact : la poussée du clip ; après : la suite du geste et le retour.</summary>
        private float ClipTime(float n)
        {
            float impact = _track.impact;
            if (n <= _impactTime) return n / _impactTime * impact;
            return impact + (n - _impactTime) / (1f - _impactTime) * (1f - impact);
        }

        /// <summary>Écart de position et orientation d'une piste à une fraction du clip (interpolées).</summary>
        private void Read(float[] data, float fraction, out Vector3 delta, out Quaternion rotation)
        {
            delta = Vector3.zero;
            rotation = Quaternion.identity;

            int frames = _track.frames;
            if (data == null || data.Length < frames * 7 || frames < 2) return;

            float f = Mathf.Clamp01(fraction) * (frames - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), frames - 2);
            float u = f - i;

            Vector3 a = Position(data, i);
            Vector3 b = Position(data, i + 1);
            delta = Vector3.Lerp(a, b, u);
            rotation = Quaternion.Slerp(Rotation(data, i), Rotation(data, i + 1), u);

            if (_mirror)
            {
                // Miroir gauche/droite (plan YZ) : x change de signe ; la rotation, conjuguée par le
                // miroir, devient (x, -y, -z, w) — exactement ce que fait AttackData.Mirror aux angles.
                delta.x = -delta.x;
                rotation = new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
            }
        }

        private static Vector3 Position(float[] data, int frame)
        {
            int o = frame * 7;
            return new Vector3(data[o], data[o + 1], data[o + 2]);
        }

        private static Quaternion Rotation(float[] data, int frame)
        {
            int o = frame * 7;
            Quaternion q = new Quaternion(data[o + 3], data[o + 4], data[o + 5], data[o + 6]);
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (length < 1e-6f) return Quaternion.identity;
            return new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length);
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
