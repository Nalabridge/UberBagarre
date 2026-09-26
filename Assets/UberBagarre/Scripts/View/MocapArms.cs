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
    /// Directs et crochets seulement : l'uppercut et les coups au corps capturés partent de la
    /// hanche, tête plongée — vus de ses propres yeux, ils ne ressemblent à rien. Ceux-là gardent
    /// leurs poses écrites. Une main sans piste prend celle de l'autre main, en miroir.
    /// </summary>
    public class MocapArms : MonoBehaviour, IHandMotion
    {
        [Serializable]
        private class TrackFile
        {
            public int version;
            public Track[] tracks;
        }

        [Serializable]
        private class Track
        {
            public string name;
            public string attack;
            public int side;
            public bool body;
            public float fps;
            public float impact;
            public int frames;
            public float[] strike;
            public float[] off;
        }

        [SerializeField]
        [Tooltip("Pistes cuites par Tools/mocap/cuisson_bras.py.")]
        private TextAsset _tracks;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part du mouvement de la main libre gardee : le corps la bouge, la garde la retient.")]
        private float _offHandShare = 0.35f;

        [SerializeField] private Vector2 _amplitudeRange = new Vector2(0.35f, 1.3f);

        [SerializeField] private bool _log;

        private Track[] _loaded;
        private readonly List<Track> _candidates = new List<Track>();
        private Track _track;
        private Track _previous;
        private bool _mirror;
        private float _scale = 1f;
        private float _impactTime = 0.4f;

        private void Awake()
        {
            Load();
        }

        private void Load()
        {
            if (_loaded != null || _tracks == null) return;

            try
            {
                TrackFile file = JsonUtility.FromJson<TrackFile>(_tracks.text);
                _loaded = file != null && file.tracks != null ? file.tracks : new Track[0];
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UberBagarre] Pistes de bras illisibles (" + e.Message + ") : coups ecrits.", this);
                _loaded = new Track[0];
            }
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
            Vector3 capturedImpact;
            Quaternion unused;
            Read(_track.strike, _track.impact, out capturedImpact, out unused);
            float captured = capturedImpact.magnitude;
            float designed = (designImpact - guard.position).magnitude;
            _scale = captured > 0.01f
                ? Mathf.Clamp(designed / captured, _amplitudeRange.x, _amplitudeRange.y)
                : 1f;

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
            return strikeGuard.position + delta * _scale;
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
