using System;
using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Une caméra de plan, pour les moments où le jeu quitte les yeux du joueur : le comptoir
    /// d'un magasin (le vendeur accoudé, cadré à gauche, le menu à droite), le fauteuil du
    /// barbier (le visage du joueur, comme dans un miroir).
    ///
    /// Elle part EXACTEMENT de la vue du joueur et y revient : la transition glisse, sans coupe.
    /// Pendant le plan, la caméra du joueur est éteinte (rien ne se rend deux fois) ; un léger
    /// mouvement de caméra à l'épaule évite l'image figée.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class ShotCamera : MonoBehaviour
    {
        private static ShotCamera _instance;

        private Camera _camera;
        private Camera _source;

        private Vector3 _fromPosition;
        private Quaternion _fromRotation;
        private float _fromFov;

        private Vector3 _toPosition;
        private Vector3 _lookAt;
        private Transform _follow;
        private Vector3 _followOffset;
        private float _toFov;

        private float _t;
        private float _duration;
        private bool _returning;
        private Action _done;

        /// <summary>Un plan est à l'écran (ou en transition).</summary>
        public static bool Active
        {
            get { return _instance != null && _instance._camera != null && _instance._camera.enabled; }
        }

        public static Camera Current
        {
            get { return Active ? _instance._camera : null; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        /// <summary>
        /// Passe de la caméra <paramref name="source"/> au plan : la caméra en
        /// <paramref name="position"/>, visant <paramref name="lookAt"/> (ou, si
        /// <paramref name="follow"/> est donné, ce point qui suit l'objet).
        /// </summary>
        public static void Begin(Camera source, Vector3 position, Vector3 lookAt, Transform follow, float fov, float duration)
        {
            if (source == null) return;
            ShotCamera shot = Ensure();
            bool already = shot._camera.enabled;

            shot._source = source;
            if (!already)
            {
                shot._camera.CopyFrom(source);
                shot._camera.depth = source.depth + 1f;
                shot._camera.targetTexture = null;
                shot._fromPosition = source.transform.position;
                shot._fromRotation = source.transform.rotation;
                shot._fromFov = source.fieldOfView;
                shot.transform.SetPositionAndRotation(shot._fromPosition, shot._fromRotation);
            }
            else
            {
                shot._fromPosition = shot.transform.position;
                shot._fromRotation = shot.transform.rotation;
                shot._fromFov = shot._camera.fieldOfView;
            }

            shot.Aim(position, lookAt, follow, fov);
            shot._duration = Mathf.Max(0.01f, duration);
            shot._t = 0f;
            shot._returning = false;
            shot._done = null;
            shot._camera.enabled = true;
            source.enabled = false;
            shot.Focus();
        }

        /// <summary>Change de cadrage pendant le plan (gros plan, plan large), en glissant.</summary>
        public static void Retarget(Vector3 position, Vector3 lookAt, Transform follow, float fov, float duration)
        {
            if (!Active) return;
            _instance._fromPosition = _instance.transform.position;
            _instance._fromRotation = _instance.transform.rotation;
            _instance._fromFov = _instance._camera.fieldOfView;
            _instance.Aim(position, lookAt, follow, fov);
            _instance._duration = Mathf.Max(0.01f, duration);
            _instance._t = 0f;
            _instance.Focus();
        }

        /// <summary>Revient à la caméra du joueur, puis appelle <paramref name="done"/>.</summary>
        public static void End(float duration, Action done)
        {
            if (!Active)
            {
                if (done != null) done();
                return;
            }

            ShotCamera shot = _instance;
            shot._fromPosition = shot.transform.position;
            shot._fromRotation = shot.transform.rotation;
            shot._fromFov = shot._camera.fieldOfView;
            shot._duration = Mathf.Max(0.01f, duration);
            shot._t = 0f;
            shot._returning = true;
            shot._done = done;
            UrpBridge.SetFocus(false, 2f);
        }

        /// <summary>Coupe tout de suite (une scène qui s'interrompt).</summary>
        public static void Cancel()
        {
            if (_instance == null || _instance._camera == null) return;
            _instance.Finish();
        }

        private static ShotCamera Ensure()
        {
            if (_instance != null && _instance._camera != null) return _instance;
            GameObject go = new GameObject("Camera de plan");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ShotCamera>();
            _instance._camera = go.AddComponent<Camera>();
            _instance._camera.enabled = false;
            return _instance;
        }

        private void Aim(Vector3 position, Vector3 lookAt, Transform follow, float fov)
        {
            _toPosition = position;
            _follow = follow;
            _followOffset = follow != null ? lookAt - follow.position : Vector3.zero;
            _lookAt = lookAt;
            _toFov = fov;
        }

        private void LateUpdate()
        {
            if (_camera == null || !_camera.enabled) return;
            if (_source == null)
            {
                Finish();
                return;
            }

            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / _duration);
            float e = _t * _t * (3f - 2f * _t);

            Vector3 targetPosition;
            Quaternion targetRotation;
            float targetFov;
            if (_returning)
            {
                targetPosition = _source.transform.position;
                targetRotation = _source.transform.rotation;
                targetFov = _source.fieldOfView;
            }
            else
            {
                Vector3 look = _follow != null ? _follow.position + _followOffset : _lookAt;
                targetPosition = _toPosition;
                Vector3 dir = look - _toPosition;
                targetRotation = dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : _fromRotation;

                // Caméra à l'épaule : un souffle, à peine perceptible.
                float time = Time.unscaledTime;
                float yaw = (Mathf.PerlinNoise(time * 0.23f, 3.1f) - 0.5f) * 0.6f;
                float pitch = (Mathf.PerlinNoise(7.7f, time * 0.19f) - 0.5f) * 0.4f;
                targetRotation = targetRotation * Quaternion.Euler(pitch, yaw, 0f);
                targetFov = _toFov;
            }

            transform.SetPositionAndRotation(Vector3.Lerp(_fromPosition, targetPosition, e),
                Quaternion.Slerp(_fromRotation, targetRotation, e));
            _camera.fieldOfView = Mathf.Lerp(_fromFov, targetFov, e);

            if (_returning && _t >= 1f) Finish();
        }

        /// <summary>La mise au point sur le sujet du plan (profondeur de champ, si elle est voulue).</summary>
        private void Focus()
        {
            Vector3 look = _follow != null ? _follow.position + _followOffset : _lookAt;
            UrpBridge.SetFocus(Core.GameSettings.DepthOfField, Vector3.Distance(_toPosition, look));
        }

        private void Finish()
        {
            UrpBridge.SetFocus(false, 2f);
            _camera.enabled = false;
            if (_source != null) _source.enabled = true;
            _returning = false;
            Action done = _done;
            _done = null;
            if (done != null) done();
        }
    }
}
