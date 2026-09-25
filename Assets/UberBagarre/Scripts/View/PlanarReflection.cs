using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Reflet planaire : une seconde camera rend la scene en miroir dans une texture.
    ///
    /// Pourquoi pas une sonde de reflexion : une sonde capture la scene une fois, depuis
    /// un point fixe. Elle ne contient donc ni les combattants, ni les phares allumes, ni
    /// rien qui bouge — exactement ce qu'on veut voir se refleter sur une chaussee mouillee
    /// pendant une bagarre. Ici le reflet est un VRAI rendu, recalcule a chaque image :
    /// l'adversaire qui tombe se voit tomber dans la flaque.
    ///
    /// Cout : un second rendu de la scene. C'est le prix a payer, et c'est pour cette
    /// raison que la texture est en demi-resolution et que les couches refletees sont
    /// filtrables. Le sol etant plat, un seul plan suffit — ce qui rend l'effet exact,
    /// contrairement a une reflexion en espace ecran qui perd tout ce qui sort du champ.
    ///
    /// Le composant vit sur l'objet qui porte le rendu du sol. Il se declenche via
    /// OnWillRenderObject, donc uniquement quand ce sol est effectivement visible.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    [DisallowMultipleComponent]
    public class PlanarReflection : MonoBehaviour
    {
        [Header("Qualite")]
        [SerializeField]
        [Tooltip("Diviseur de resolution. 2 = moitie de l'ecran. Au-dela de 4 les neons " +
                 "refletes deviennent des taches carrees.")]
        [Range(1, 8)]
        private int _downsample = 2;

        [SerializeField]
        [Tooltip("Couches visibles dans le reflet. En exclure les plus lourdes est le " +
                 "reglage de performance le plus direct.")]
        private LayerMask _reflectLayers = -1;

        [SerializeField]
        [Tooltip("Rendre le reflet avec des ombres. Les desactiver double presque la vitesse " +
                 "et ne se voit quasiment pas dans une flaque.")]
        private bool _reflectShadows;

        [Header("Plan")]
        [SerializeField]
        [Tooltip("Decalage du plan miroir vers le haut. Un tout petit decalage evite que le " +
                 "sol lui-meme apparaisse dans son propre reflet le long de l'horizon.")]
        private float _clipPlaneOffset = 0.015f;

        [SerializeField]
        [Tooltip("Hauteur du plan d'eau. Laisser a 0 pour utiliser la hauteur de l'objet.")]
        private float _planeHeightOverride;

        [SerializeField] private bool _usePlaneHeightOverride;

        [Header("Activation")]
        [SerializeField] private bool _enabled = true;

        [SerializeField, Min(1f)]
        [Tooltip("Au-dela de cette distance entre la camera et le sol, le reflet n'est plus " +
                 "calcule : il n'occuperait que quelques pixels.")]
        private float _maxCameraHeight = 60f;

        private Camera _reflectionCamera;
        private RenderTexture _texture;
        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private bool _rendering;
        private int _textureWidth;
        private int _textureHeight;

        private static readonly int ReflectionTexId = Shader.PropertyToID("_ReflectionTex");

        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                if (_enabled == value) return;
                _enabled = value;

                if (!_enabled) PushTexture(null);
            }
        }

        public int Downsample
        {
            get { return _downsample; }
            set
            {
                int clamped = Mathf.Clamp(value, 1, 8);
                if (clamped == _downsample) return;

                _downsample = clamped;
                ReleaseTexture();
            }
        }

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            ReleaseTexture();

            if (_reflectionCamera == null) return;

            // DestroyImmediate hors du mode Play : a la sortie du mode Play, Destroy ne
            // s'execute jamais et la camera cachee survivrait d'une session a l'autre.
            if (Application.isPlaying) Destroy(_reflectionCamera.gameObject);
            else DestroyImmediate(_reflectionCamera.gameObject);

            _reflectionCamera = null;
        }

        private void OnWillRenderObject()
        {
            if (!_enabled) return;

            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (_renderer == null || !_renderer.enabled) return;

            Camera source = Camera.current;
            if (source == null) return;

            // Sans ce garde-fou, la camera de reflet declencherait a son tour un rendu
            // de reflet, et ainsi de suite jusqu'au blocage complet de l'editeur.
            if (_rendering) return;
            if (_reflectionCamera != null && source == _reflectionCamera) return;

            float planeHeight = _usePlaneHeightOverride ? _planeHeightOverride : transform.position.y;

            // Sous le plan, le miroir n'a pas de sens : on garde la derniere image plutot
            // que de rendre une scene retournee.
            float cameraHeight = source.transform.position.y - planeHeight;
            if (cameraHeight < 0.02f || cameraHeight > _maxCameraHeight) return;

            _rendering = true;

            try
            {
                EnsureTexture();
                EnsureCamera();

                RenderReflection(source, planeHeight);
                PushTexture(_texture);
            }
            finally
            {
                _rendering = false;
            }
        }

        // ------------------------------------------------------------------ rendu

        private void RenderReflection(Camera source, float planeHeight)
        {
            CopySettings(source, _reflectionCamera);

            Vector3 planePosition = new Vector3(transform.position.x, planeHeight, transform.position.z);
            Vector3 planeNormal = Vector3.up;

            float distance = -Vector3.Dot(planeNormal, planePosition) - _clipPlaneOffset;
            Vector4 plane = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, distance);

            Matrix4x4 reflection = Matrix4x4.identity;
            BuildReflectionMatrix(ref reflection, plane);

            _reflectionCamera.worldToCameraMatrix = source.worldToCameraMatrix * reflection;

            // Projection oblique : le plan proche de la camera de reflet EST le plan du
            // miroir. Tout ce qui se trouve sous la chaussee est donc rejete avant meme
            // d'etre rasterise — sinon les fondations des batiments apparaissent dans les
            // flaques.
            Vector4 clipPlane = CameraSpacePlane(_reflectionCamera, planePosition, planeNormal, 1f);
            _reflectionCamera.projectionMatrix = source.CalculateObliqueMatrix(clipPlane);

            _reflectionCamera.targetTexture = _texture;

            // Un miroir inverse l'orientation : sans inversion du culling, toutes les
            // faces visibles deviennent des faces arriere et la scene refletee disparait.
            bool previousCulling = GL.invertCulling;
            GL.invertCulling = !previousCulling;

            _reflectionCamera.transform.position = reflection.MultiplyPoint(source.transform.position);

            Vector3 euler = source.transform.eulerAngles;
            _reflectionCamera.transform.eulerAngles = new Vector3(-euler.x, euler.y, euler.z);

            // Les ombres dans un reflet de flaque sont pratiquement invisibles et coutent
            // un second passage d'ombres complet. Les couper pendant ce rendu est le levier
            // de performance le plus rentable de l'effet.
            float previousShadowDistance = QualitySettings.shadowDistance;
            if (!_reflectShadows) QualitySettings.shadowDistance = 0f;

            _reflectionCamera.Render();

            if (!_reflectShadows) QualitySettings.shadowDistance = previousShadowDistance;

            GL.invertCulling = previousCulling;
        }

        private void CopySettings(Camera source, Camera destination)
        {
            destination.clearFlags = source.clearFlags == CameraClearFlags.Nothing
                ? CameraClearFlags.SolidColor
                : source.clearFlags;

            destination.backgroundColor = source.backgroundColor;
            destination.farClipPlane = source.farClipPlane;
            destination.nearClipPlane = source.nearClipPlane;
            destination.orthographic = source.orthographic;
            destination.fieldOfView = source.fieldOfView;
            destination.orthographicSize = source.orthographicSize;
            destination.aspect = source.aspect;
            destination.allowHDR = source.allowHDR;
            destination.cullingMask = _reflectLayers.value;

            // Toujours en rendu AVANT, quel que soit celui de la camera de jeu : le reflet
            // utilise une matrice de projection oblique (le plan de coupe suit le sol), que le
            // rendu differe ne gere pas. Le rendu avant autorise en plus le MSAA sur la texture
            // de reflet — sans lui, chaque arete du reflet scintille des que l'on bouge.
            destination.renderingPath = RenderingPath.Forward;
            destination.allowMSAA = true;
        }

        // ------------------------------------------------------------------ ressources

        private void EnsureTexture()
        {
            int width = Mathf.Max(64, Screen.width / _downsample);
            int height = Mathf.Max(64, Screen.height / _downsample);

            if (_texture != null && _textureWidth == width && _textureHeight == height) return;

            ReleaseTexture();

            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR)
                ? RenderTextureFormat.DefaultHDR
                : RenderTextureFormat.Default;

            _texture = new RenderTexture(width, height, 24, format);
            _texture.name = "RefletPlanaire";
            _texture.hideFlags = HideFlags.DontSave;
            _texture.filterMode = FilterMode.Bilinear;
            _texture.wrapMode = TextureWrapMode.Clamp;
            _texture.antiAliasing = 4;

            _textureWidth = width;
            _textureHeight = height;
        }

        private void ReleaseTexture()
        {
            if (_texture == null) return;

            if (_reflectionCamera != null) _reflectionCamera.targetTexture = null;

            _texture.Release();

            if (Application.isPlaying) Destroy(_texture);
            else DestroyImmediate(_texture);

            _texture = null;
            _textureWidth = 0;
            _textureHeight = 0;
        }

        private void EnsureCamera()
        {
            if (_reflectionCamera != null) return;

            GameObject go = new GameObject("CameraReflet (" + name + ")");
            go.hideFlags = HideFlags.HideAndDontSave;

            _reflectionCamera = go.AddComponent<Camera>();
            _reflectionCamera.enabled = false;
            _reflectionCamera.depthTextureMode = DepthTextureMode.None;
            _reflectionCamera.useOcclusionCulling = false;
        }

        private void PushTexture(Texture texture)
        {
            if (_renderer == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();

            _renderer.GetPropertyBlock(_block);
            _block.SetTexture(ReflectionTexId, texture == null ? Texture2D.blackTexture : texture);
            _renderer.SetPropertyBlock(_block);
        }

        // ------------------------------------------------------------------ maths

        /// <summary>Matrice de symetrie par rapport au plan (nx, ny, nz, d).</summary>
        private static void BuildReflectionMatrix(ref Matrix4x4 matrix, Vector4 plane)
        {
            matrix.m00 = 1f - 2f * plane.x * plane.x;
            matrix.m01 = -2f * plane.x * plane.y;
            matrix.m02 = -2f * plane.x * plane.z;
            matrix.m03 = -2f * plane.w * plane.x;

            matrix.m10 = -2f * plane.y * plane.x;
            matrix.m11 = 1f - 2f * plane.y * plane.y;
            matrix.m12 = -2f * plane.y * plane.z;
            matrix.m13 = -2f * plane.w * plane.y;

            matrix.m20 = -2f * plane.z * plane.x;
            matrix.m21 = -2f * plane.z * plane.y;
            matrix.m22 = 1f - 2f * plane.z * plane.z;
            matrix.m23 = -2f * plane.w * plane.z;

            matrix.m30 = 0f;
            matrix.m31 = 0f;
            matrix.m32 = 0f;
            matrix.m33 = 1f;
        }

        private Vector4 CameraSpacePlane(Camera camera, Vector3 position, Vector3 normal, float sideSign)
        {
            Vector3 offset = position + normal * _clipPlaneOffset;
            Matrix4x4 worldToCamera = camera.worldToCameraMatrix;

            Vector3 cameraPosition = worldToCamera.MultiplyPoint(offset);
            Vector3 cameraNormal = worldToCamera.MultiplyVector(normal).normalized * sideSign;

            return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z,
                -Vector3.Dot(cameraPosition, cameraNormal));
        }

        private void OnValidate()
        {
            _downsample = Mathf.Clamp(_downsample, 1, 8);
        }

        /// <summary>Exposé pour le menu graphique : les ombres dans le reflet coutent cher.</summary>
        public bool ReflectShadows
        {
            get { return _reflectShadows; }
            set { _reflectShadows = value; }
        }
    }
}
