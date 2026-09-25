using UnityEngine;
using UnityEngine.Rendering;

namespace UberBagarre.View
{
    /// <summary>
    /// Chaine de post-traitement : bloom, tonemap, etalonnage, vignette, grain.
    ///
    /// Pourquoi ce composant existe : le projet tourne en Built-in Render Pipeline sans
    /// le paquet Post Processing. Sans lui, la valeur d'un pixel est plafonnee a 1 avant
    /// meme d'arriver a l'ecran, donc une enseigne au neon, un phare et un mur blanc ont
    /// rigoureusement la meme luminosite affichee. C'est le vrai responsable de l'aspect
    /// « plat » d'une scene de nuit — pas le nombre de triangles.
    ///
    /// Le bloom est construit en pyramide (reduction puis agrandissement progressif)
    /// plutot qu'en un seul flou large : un flou unique coute cher et donne un halo de
    /// taille unique, alors qu'une source reelle a un coeur serre ET une nappe tres
    /// large. Ce sont les deux ensemble qui font lire une lumiere.
    ///
    /// Aucune reference de scene n'est obligatoire : si le materiau n'est pas assigne,
    /// il est reconstruit a partir du nom du shader, et si le shader lui-meme manque,
    /// l'image passe telle quelle. Un post-traitement casse ne doit jamais donner un
    /// ecran noir.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class UberPostProcess : MonoBehaviour
    {
        public const string ShaderName = "UberBagarre/Post";

        private const int PassPrefilter = 0;
        private const int PassDownsample = 1;
        private const int PassUpsample = 2;
        private const int PassComposite = 3;
        private const int PassVolumetric = 4;
        private const int PassFxaa = 5;
        private const int PassTaa = 6;

        /// <summary>Les méthodes d'anticrénelage disponibles.</summary>
        public enum AntiAliasingMode
        {
            /// <summary>Aucun lissage.</summary>
            Aucun = 0,

            /// <summary>Lissage de l'image finale : rapide, mais les aretes fines scintillent en mouvement.</summary>
            Fxaa = 1,

            /// <summary>Temporel : accumule plusieurs images decalees. Le plus stable en mouvement.</summary>
            Taa = 2,

            /// <summary>MSAA x8 en rendu AVANT : aretes nettes, mais moins de lampes calculees par pixel.</summary>
            Msaa = 3
        }

        /// <summary>Nombre maximal de lampes volumétriques prises en compte par image (les plus proches).</summary>
        public const int MaxVolumetricLights = 12;

        private const int MaxLevels = 10;

        [Header("Materiau")]
        [SerializeField]
        [Tooltip("Materiau utilisant le shader UberBagarre/Post. Reconstruit automatiquement s'il manque.")]
        private Material _material;

        [Header("Activation")]
        [SerializeField] private bool _enabled = true;

        [SerializeField]
        [Tooltip("Rendre aussi dans les vues d'edition. Pratique, mais recalcule la pyramide a chaque repaint.")]
        private bool _renderInEditMode = true;

        [Header("Bloom")]
        [SerializeField, Min(0f)]
        [Tooltip("Quantite de bloom ajoutee a l'image. 0 = aucun.")]
        private float _bloomIntensity = 1.15f;

        [SerializeField, Min(0f)]
        [Tooltip("Au-dela de cette luminosite, un pixel deborde. Sous 1, meme les surfaces " +
                 "normalement eclairees se mettent a briller et l'image devient laiteuse.")]
        private float _threshold = 1.05f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Adoucit l'entree en seuil. A 0, un contour net apparait autour des sources.")]
        private float _softKnee = 0.6f;

        [SerializeField, Range(1, MaxLevels)]
        [Tooltip("Nombre de niveaux de la pyramide. Plus il y en a, plus la nappe lumineuse est large.")]
        private int _iterations = 7;

        [SerializeField, Range(0.5f, 3f)]
        [Tooltip("Ecartement des taps a l'agrandissement. Au-dela de 2, le halo devient carre.")]
        private float _spread = 1.15f;

        [SerializeField]
        [ColorUsage(false, true)]
        private Color _bloomTint = Color.white;

        [Header("Etalonnage")]
        [SerializeField, Range(0.1f, 4f)] private float _exposure = 1f;
        [SerializeField, Range(0.5f, 2f)] private float _contrast = 1.08f;
        [SerializeField, Range(0f, 2f)] private float _saturation = 1.06f;
        [SerializeField] private Color _colorFilter = Color.white;

        [SerializeField]
        [Tooltip("Teinte poussee dans les ombres. Le bleu froid dans les noirs est la signature " +
                 "d'une nuit : des ombres grises se lisent comme une image sous-exposee, pas comme la nuit.")]
        private Color _shadowTint = new Color(0.62f, 0.74f, 1f);

        [SerializeField, Range(0f, 1f)] private float _shadowTintAmount = 0.45f;

        [SerializeField] private Color _highlightTint = new Color(1f, 0.93f, 0.82f);
        [SerializeField, Range(0f, 1f)] private float _highlightTintAmount = 0.25f;

        [Header("Objectif")]
        [SerializeField, Range(0f, 1f)] private float _vignette = 0.42f;
        [SerializeField, Range(0f, 1f)] private float _vignetteSmoothness = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _vignetteRoundness = 0.75f;
        [SerializeField] private Color _vignetteColor = Color.black;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Aberration chromatique, en pixels au bord de l'image. Desactivee par defaut : " +
                 "sur une image de nuit pleine de points lumineux, elle se lit comme du flou sale.")]
        private float _aberration;

        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Grain de pellicule. Desactive par defaut : anime a chaque image, il se lit " +
                 "comme du bruit numerique, pas comme une texture de film.")]
        private float _grain;

        [SerializeField, Range(1f, 4f)] private float _grainSize = 1.4f;

        [Header("Lumiere volumetrique")]
        [SerializeField]
        [Tooltip("La lumiere des lampes marquees VolumetricLight se voit dans l'air humide.")]
        private bool _volumetric = true;

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Multiplicateur global de la lumiere diffusee.")]
        private float _volumetricIntensity = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("Densite de l'air. 0,03 = nuit humide ; 0,1 = fumee de club.")]
        private float _volumetricDensity = 0.032f;

        [SerializeField, Min(5f)] private float _volumetricDistance = 90f;

        [SerializeField, Min(0f)]
        [Tooltip("Decroissance de la brume avec la hauteur. 0 = brume uniforme.")]
        private float _volumetricHeightFalloff = 0.07f;

        [SerializeField, Range(1, 4)]
        [Tooltip("Resolution du calcul : 2 = demi-resolution. Le resultat est lisse, la perte ne se voit pas.")]
        private int _volumetricDownsample = 2;

        [Header("Anticrenelage")]
        [SerializeField]
        [Tooltip("MSAA = aretes nettes, image stable (defaut). TAA = lisse aussi les reflets, mais " +
                 "l'image tremble si les vecteurs de mouvement ne suivent pas. FXAA = rapide.")]
        private AntiAliasingMode _antiAliasing = AntiAliasingMode.Msaa;

        [SerializeField, Range(0.5f, 0.98f)]
        [Tooltip("Part de l'historique gardee a chaque image quand rien ne bouge. Plus haut = plus lisse.")]
        private float _taaStationaryBlend = 0.93f;

        [SerializeField, Range(0.3f, 0.95f)]
        [Tooltip("Part de l'historique en mouvement rapide. Plus bas = moins de trainees.")]
        private float _taaMotionBlend = 0.78f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Accentuation apres accumulation.")]
        private float _taaSharpness = 0.22f;

        [SerializeField, Range(0.3f, 1f)]
        [Tooltip("Amplitude du decalage de camera, en pixels.")]
        private float _taaJitterSpread = 0.75f;

        private Camera _camera;
        private Material _runtimeMaterial;
        private readonly RenderTexture[] _chain = new RenderTexture[MaxLevels];

        private readonly Vector4[] _volPositions = new Vector4[MaxVolumetricLights];
        private readonly Vector4[] _volDirections = new Vector4[MaxVolumetricLights];
        private readonly Vector4[] _volColors = new Vector4[MaxVolumetricLights];
        private readonly VolumetricLight[] _volPicked = new VolumetricLight[MaxVolumetricLights];
        private readonly float[] _volScores = new float[MaxVolumetricLights];
        private readonly Vector3[] _frustum = new Vector3[4];

        private RenderTexture _history;
        private bool _historyValid;
        private int _jitterIndex;
        private Vector2 _jitter;
        private bool _jittered;
        private AntiAliasingMode _appliedMode = (AntiAliasingMode)(-1);

        private static readonly int HistoryTexId = Shader.PropertyToID("_HistoryTex");
        private static readonly int JitterId = Shader.PropertyToID("_Jitter");
        private static readonly int TaaParamsId = Shader.PropertyToID("_TaaParams");

        private static readonly int VolumetricTexId = Shader.PropertyToID("_VolumetricTex");
        private static readonly int VolumetricOnId = Shader.PropertyToID("_VolumetricOn");
        private static readonly int VolPosId = Shader.PropertyToID("_VolPos");
        private static readonly int VolDirId = Shader.PropertyToID("_VolDir");
        private static readonly int VolColorId = Shader.PropertyToID("_VolColor");
        private static readonly int VolCountId = Shader.PropertyToID("_VolCount");
        private static readonly int VolParamsId = Shader.PropertyToID("_VolParams");
        private static readonly int FrustumBLId = Shader.PropertyToID("_FrustumBL");
        private static readonly int FrustumTLId = Shader.PropertyToID("_FrustumTL");
        private static readonly int FrustumTRId = Shader.PropertyToID("_FrustumTR");
        private static readonly int FrustumBRId = Shader.PropertyToID("_FrustumBR");

        private static readonly int FilterId = Shader.PropertyToID("_Filter");
        private static readonly int SampleScaleId = Shader.PropertyToID("_SampleScale");
        private static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
        private static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        private static readonly int BloomTintId = Shader.PropertyToID("_BloomTint");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int ColorFilterId = Shader.PropertyToID("_ColorFilter");
        private static readonly int ShadowsId = Shader.PropertyToID("_Shadows");
        private static readonly int HighlightsId = Shader.PropertyToID("_Highlights");
        private static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        private static readonly int VignetteColorId = Shader.PropertyToID("_VignetteColor");
        private static readonly int AberrationId = Shader.PropertyToID("_Aberration");
        private static readonly int GrainId = Shader.PropertyToID("_GrainIntensity");
        private static readonly int GrainScaleId = Shader.PropertyToID("_GrainScale");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int LinearModeId = Shader.PropertyToID("_LinearMode");

        // ------------------------------------------------------------------ reglages publics

        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        public float BloomIntensity
        {
            get { return _bloomIntensity; }
            set { _bloomIntensity = Mathf.Max(0f, value); }
        }

        public float Threshold
        {
            get { return _threshold; }
            set { _threshold = Mathf.Max(0f, value); }
        }

        public float Exposure
        {
            get { return _exposure; }
            set { _exposure = Mathf.Clamp(value, 0.1f, 4f); }
        }

        public float Saturation
        {
            get { return _saturation; }
            set { _saturation = Mathf.Clamp(value, 0f, 2f); }
        }

        public float Contrast
        {
            get { return _contrast; }
            set { _contrast = Mathf.Clamp(value, 0.5f, 2f); }
        }

        public float Vignette
        {
            get { return _vignette; }
            set { _vignette = Mathf.Clamp01(value); }
        }

        public bool Volumetric
        {
            get { return _volumetric; }
            set { _volumetric = value; }
        }

        public float VolumetricIntensity
        {
            get { return _volumetricIntensity; }
            set { _volumetricIntensity = Mathf.Clamp(value, 0f, 3f); }
        }

        public AntiAliasingMode AntiAliasing
        {
            get { return _antiAliasing; }
            set { _antiAliasing = value; }
        }

        public float Grain
        {
            get { return _grain; }
            set { _grain = Mathf.Clamp(value, 0f, 0.5f); }
        }

        public float Aberration
        {
            get { return _aberration; }
            set { _aberration = Mathf.Clamp(value, 0f, 4f); }
        }

        /// <summary>
        /// Le materiau reellement utilise.
        ///
        /// C'est TOUJOURS une instance jetable, jamais le materiau d'asset lui-meme, et ce
        /// n'est pas un detail : la composition ecrit une vingtaine d'uniformes a chaque
        /// image. Ecrire dans l'asset le marquerait modifie en permanence, donc le projet
        /// aurait un fichier a sauvegarder a chaque seconde de jeu en mode edition.
        ///
        /// Le materiau d'asset ne sert qu'a deux choses : garantir que le shader est compile
        /// dans une build (un shader que rien ne reference n'y entre pas), et fournir le
        /// shader ici. S'il manque, on retombe sur Shader.Find ; s'il n'y a rien du tout,
        /// on renvoie null et l'image passe sans traitement plutot que de devenir noire.
        /// </summary>
        public Material EffectMaterial
        {
            get
            {
                if (_runtimeMaterial != null) return _runtimeMaterial;

                Shader shader = _material != null ? _material.shader : Shader.Find(ShaderName);
                if (shader == null || !shader.isSupported) return null;

                _runtimeMaterial = new Material(shader);
                _runtimeMaterial.name = "UberPost (instance)";
                _runtimeMaterial.hideFlags = HideFlags.HideAndDontSave;
                return _runtimeMaterial;
            }
        }

        // ------------------------------------------------------------------ cycle de vie

        private void OnEnable()
        {
            _camera = GetComponent<Camera>();

            // Sans HDR, la couleur est ecretee a 1 AVANT le post-traitement : le seuil de
            // bloom ne peut alors plus distinguer une source lumineuse d'un mur blanc, et
            // tout l'effet s'effondre en un halo uniforme. C'est le reglage le plus
            // important de toute la chaine.
            if (_camera != null)
            {
                _camera.allowHDR = true;

                // La lumiere volumetrique s'arrete sur la premiere surface : il lui faut la
                // profondeur. Gratuit en rendu differe, une passe de plus en rendu avant.
                _camera.depthTextureMode |= DepthTextureMode.Depth;
            }

            _appliedMode = (AntiAliasingMode)(-1);
            ApplyCameraMode();
        }

        private void OnDisable()
        {
            ReleaseChain();
            ReleaseHistory();

            if (_camera != null && _jittered)
            {
                _camera.ResetProjectionMatrix();
                _jittered = false;
            }

            if (_runtimeMaterial != null)
            {
                if (Application.isPlaying) Destroy(_runtimeMaterial);
                else DestroyImmediate(_runtimeMaterial);

                _runtimeMaterial = null;
            }
        }

        // ------------------------------------------------------------------ mode de rendu

        /// <summary>
        /// Rendu différé pour tout sauf le MSAA : le différé calcule chaque lampe par pixel,
        /// mais ne sait pas faire de MSAA. Le mode MSAA repasse donc la caméra en rendu avant.
        /// </summary>
        private void ApplyCameraMode()
        {
            if (_camera == null) return;

            // Un autre composant (VisualQuality) peut remettre le MSAA global a zero apres nous :
            // en mode MSAA, on le reverifie a chaque image.
            bool stale = _antiAliasing == AntiAliasingMode.Msaa && QualitySettings.antiAliasing != 8;
            if (_appliedMode == _antiAliasing && !stale) return;

            _appliedMode = _antiAliasing;
            bool msaa = _antiAliasing == AntiAliasingMode.Msaa;

            _camera.renderingPath = msaa ? RenderingPath.Forward : RenderingPath.DeferredShading;
            _camera.allowMSAA = msaa;

            if (msaa) QualitySettings.antiAliasing = 8;
            else if (QualitySettings.antiAliasing > 1) QualitySettings.antiAliasing = 0;

            if (_antiAliasing == AntiAliasingMode.Taa && SystemInfo.supportsMotionVectors)
            {
                _camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;
            }

            _historyValid = false;
        }

        private bool TaaActive
        {
            get
            {
                return _enabled && _antiAliasing == AntiAliasingMode.Taa && Application.isPlaying
                       && SystemInfo.supportsMotionVectors && _camera != null && !_camera.orthographic;
            }
        }

        /// <summary>
        /// Décale la caméra d'une fraction de pixel, différente à chaque image (suite de Halton).
        /// C'est ce décalage qui donne au TAA des échantillons nouveaux à accumuler.
        /// </summary>
        private void OnPreCull()
        {
            ApplyCameraMode();

            if (!TaaActive) return;

            _camera.ResetProjectionMatrix();
            _camera.nonJitteredProjectionMatrix = _camera.projectionMatrix;

            _jitterIndex = (_jitterIndex + 1) % 8;
            _jitter = new Vector2(Halton(_jitterIndex + 1, 2) - 0.5f, Halton(_jitterIndex + 1, 3) - 0.5f) * _taaJitterSpread;

            Matrix4x4 projection = _camera.projectionMatrix;
            projection[0, 2] += _jitter.x * 2f / Mathf.Max(1, _camera.pixelWidth);
            projection[1, 2] += _jitter.y * 2f / Mathf.Max(1, _camera.pixelHeight);

            _camera.projectionMatrix = projection;
            _camera.useJitteredProjectionMatrixForTransparentRendering = false;
            _jittered = true;
        }

        private void OnPostRender()
        {
            if (!_jittered || _camera == null) return;

            _camera.ResetProjectionMatrix();
            _jittered = false;
        }

        private static float Halton(int index, int radix)
        {
            float result = 0f;
            float fraction = 1f / radix;

            while (index > 0)
            {
                result += fraction * (index % radix);
                index /= radix;
                fraction /= radix;
            }

            return result;
        }

        /// <summary>
        /// Accumule l'image courante dans l'historique. Renvoie l'image lissée (temporaire, à
        /// libérer par l'appelant), ou null si le TAA ne s'applique pas.
        /// </summary>
        private RenderTexture ResolveTemporal(Material material, RenderTexture source)
        {
            if (!TaaActive || source.width < 8 || source.height < 8) return null;

            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                ? RenderTextureFormat.ARGBHalf
                : RenderTextureFormat.DefaultHDR;

            if (_history == null || _history.width != source.width || _history.height != source.height)
            {
                ReleaseHistory();

                _history = new RenderTexture(source.width, source.height, 0, format, RenderTextureReadWrite.Linear);
                _history.name = "Historique TAA";
                _history.hideFlags = HideFlags.HideAndDontSave;
                _history.filterMode = FilterMode.Bilinear;
                _history.wrapMode = TextureWrapMode.Clamp;
                _history.Create();
                _historyValid = false;
            }

            RenderTexture resolved = RenderTexture.GetTemporary(source.width, source.height, 0, format,
                RenderTextureReadWrite.Linear);
            resolved.filterMode = FilterMode.Bilinear;

            material.SetTexture(HistoryTexId, _historyValid ? (Texture)_history : source);
            material.SetVector(JitterId, new Vector4(_jitter.x / source.width, _jitter.y / source.height, 0f, 0f));
            material.SetVector(TaaParamsId, new Vector4(_historyValid ? _taaStationaryBlend : 0f,
                _historyValid ? _taaMotionBlend : 0f, _taaSharpness, 0f));

            Graphics.Blit(source, resolved, material, PassTaa);
            Graphics.Blit(resolved, _history);
            _historyValid = true;

            return resolved;
        }

        private void ReleaseHistory()
        {
            if (_history == null) return;

            _history.Release();
            if (Application.isPlaying) Destroy(_history);
            else DestroyImmediate(_history);

            _history = null;
            _historyValid = false;
        }

        /// <summary>
        /// Oublie l'historique : à appeler après une téléportation, sinon l'image précédente
        /// (un autre lieu) traînerait une image dans la nouvelle.
        /// </summary>
        public void ResetHistory()
        {
            _historyValid = false;
        }

        // ------------------------------------------------------------------ rendu

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Material material = EffectMaterial;

            bool skip = !_enabled
                        || material == null
                        || source == null
                        || (!Application.isPlaying && !_renderInEditMode);

            if (skip)
            {
                Graphics.Blit(source, destination);
                return;
            }

            PushUniforms(material);

            // La lumiere dans l'air est calculee sur l'image d'ORIGINE : c'est elle qui porte
            // l'orientation de la profondeur (retournee sur certaines plateformes).
            RenderTexture volumetric = RenderVolumetric(material, source);
            material.SetTexture(VolumetricTexId, volumetric != null ? (Texture)volumetric : Texture2D.blackTexture);
            material.SetFloat(VolumetricOnId, volumetric != null ? 1f : 0f);

            // Puis le TAA, sur l'image HDR : le bloom et l'etalonnage travaillent ensuite sur
            // une image deja stable.
            RenderTexture temporal = ResolveTemporal(material, source);
            if (temporal != null) source = temporal;

            int levels = BuildBloomPyramid(material, source);

            if (levels <= 0)
            {
                // Pas assez de pixels pour une pyramide (vignette de previsualisation,
                // fenetre reduite) : on compose quand meme, sans bloom.
                material.SetTexture(BloomTexId, Texture2D.blackTexture);
                material.SetFloat(BloomIntensityId, 0f);
            }
            else
            {
                material.SetTexture(BloomTexId, _chain[0]);
            }

            Compose(material, source, destination);

            ReleaseChain();
            if (volumetric != null) RenderTexture.ReleaseTemporary(volumetric);
            if (temporal != null) RenderTexture.ReleaseTemporary(temporal);
        }

        /// <summary>Composition finale, suivie du FXAA s'il est actif.</summary>
        private void Compose(Material material, RenderTexture source, RenderTexture destination)
        {
            if (_antiAliasing != AntiAliasingMode.Fxaa || source.width < 8 || source.height < 8)
            {
                Graphics.Blit(source, destination, material, PassComposite);
                return;
            }

            // Le FXAA travaille sur l'image TONEMAPPEE : c'est la que les contrastes sont ceux
            // que l'oeil verra. Sur l'image HDR, un neon a 40 rendrait chaque arete voisine
            // « contrastee » et tout serait lisse.
            RenderTexture composed = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            composed.filterMode = FilterMode.Bilinear;

            Graphics.Blit(source, composed, material, PassComposite);
            Graphics.Blit(composed, destination, material, PassFxaa);

            RenderTexture.ReleaseTemporary(composed);
        }

        // ------------------------------------------------------------------ lumiere volumetrique

        /// <summary>
        /// Calcule la lumière diffusée dans une texture à basse résolution, ou renvoie null s'il
        /// n'y a rien à calculer (effet coupé, aucune lampe proche).
        /// </summary>
        private RenderTexture RenderVolumetric(Material material, RenderTexture source)
        {
            if (!_volumetric || _volumetricIntensity <= 0.001f || _volumetricDensity <= 0f) return null;
            if (_camera == null || source.width < 16 || source.height < 16) return null;

            int count = CollectVolumetricLights();
            if (count == 0) return null;

            material.SetVectorArray(VolPosId, _volPositions);
            material.SetVectorArray(VolDirId, _volDirections);
            material.SetVectorArray(VolColorId, _volColors);
            material.SetFloat(VolCountId, count);
            material.SetVector(VolParamsId, new Vector4(_volumetricDensity * _volumetricIntensity,
                _volumetricDistance, _volumetricHeightFalloff, 0f));

            // Coins du frustum a une profondeur de 1 : le shader multiplie par la profondeur
            // lue pour retrouver le point de la surface, sans inverser de matrice.
            _camera.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), 1f,
                Camera.MonoOrStereoscopicEye.Mono, _frustum);

            Transform view = _camera.transform;
            material.SetVector(FrustumBLId, view.TransformVector(_frustum[0]));
            material.SetVector(FrustumTLId, view.TransformVector(_frustum[1]));
            material.SetVector(FrustumTRId, view.TransformVector(_frustum[2]));
            material.SetVector(FrustumBRId, view.TransformVector(_frustum[3]));

            int divisor = Mathf.Clamp(_volumetricDownsample, 1, 4);
            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                ? RenderTextureFormat.ARGBHalf
                : RenderTextureFormat.DefaultHDR;

            RenderTexture target = RenderTexture.GetTemporary(Mathf.Max(8, source.width / divisor),
                Mathf.Max(8, source.height / divisor), 0, format, RenderTextureReadWrite.Linear);

            target.filterMode = FilterMode.Bilinear;
            target.wrapMode = TextureWrapMode.Clamp;

            Graphics.Blit(source, target, material, PassVolumetric);
            return target;
        }

        /// <summary>
        /// Choisit les lampes volumétriques qui comptent pour cette image : allumées, pas trop
        /// loin, et les plus proches d'abord. Remplit les tableaux envoyés au shader.
        /// </summary>
        private int CollectVolumetricLights()
        {
            int count = 0;
            Vector3 eye = _camera.transform.position;
            float maxDistance = _volumetricDistance;

            System.Collections.Generic.IReadOnlyList<VolumetricLight> all = VolumetricLight.All;

            for (int i = 0; i < all.Count; i++)
            {
                VolumetricLight candidate = all[i];
                if (candidate == null) continue;

                Light light = candidate.Light;
                if (light == null || !light.isActiveAndEnabled || light.intensity <= 0.01f) continue;
                if (light.type != LightType.Spot && light.type != LightType.Point) continue;

                // Score : distance jusqu'au BORD de la portee. Une grande lampe lointaine peut
                // passer devant une petite lampe proche, puisque son faisceau arrive jusqu'ici.
                float score = Vector3.Distance(eye, light.transform.position) - light.range;
                if (score > maxDistance) continue;

                // Insertion triee dans un tableau fixe : aucune allocation par image.
                int slot = count;
                if (count == MaxVolumetricLights)
                {
                    if (score >= _volScores[count - 1]) continue;
                    slot = count - 1;
                }
                else
                {
                    count++;
                }

                while (slot > 0 && _volScores[slot - 1] > score)
                {
                    _volScores[slot] = _volScores[slot - 1];
                    _volPicked[slot] = _volPicked[slot - 1];
                    slot--;
                }

                _volScores[slot] = score;
                _volPicked[slot] = candidate;
            }

            for (int i = 0; i < MaxVolumetricLights; i++)
            {
                if (i >= count)
                {
                    _volPositions[i] = Vector4.zero;
                    _volDirections[i] = new Vector4(0f, -1f, 0f, -2f);
                    _volColors[i] = Vector4.zero;
                    continue;
                }

                VolumetricLight picked = _volPicked[i];
                Light light = picked.Light;
                Transform t = light.transform;

                _volPositions[i] = new Vector4(t.position.x, t.position.y, t.position.z, Mathf.Max(0.5f, light.range));

                if (light.type == LightType.Spot)
                {
                    float outer = Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
                    float inner = Mathf.Cos(light.spotAngle * 0.5f * 0.55f * Mathf.Deg2Rad);
                    Vector3 axis = t.forward;

                    _volDirections[i] = new Vector4(axis.x, axis.y, axis.z, outer);
                    _volColors[i] = LinearColor(light, picked.Scattering, 1f / Mathf.Max(1e-3f, inner - outer));
                }
                else
                {
                    _volDirections[i] = new Vector4(0f, -1f, 0f, -2f);
                    _volColors[i] = LinearColor(light, picked.Scattering, 0f);
                }

                _volPicked[i] = null;
            }

            return count;
        }

        /// <summary>
        /// La couleur que la lampe envoie réellement, en espace linéaire.
        ///
        /// Le rendu intégré multiplie par défaut l'intensité en GAMMA avant de linéariser : une
        /// lampe à 3,6 éclaire donc environ 3,6^2,2 fois sa couleur. Refaire le même calcul ici
        /// garde le faisceau proportionnel à ce qu'il éclaire — sinon un faisceau très vif sous
        /// une lampe faible, ou l'inverse.
        /// </summary>
        private static Vector4 LinearColor(Light light, float scattering, float softness)
        {
            Color color = GraphicsSettings.lightsUseLinearIntensity
                ? light.color.linear * light.intensity
                : (light.color * light.intensity).linear;

            return new Vector4(color.r * scattering, color.g * scattering, color.b * scattering, softness);
        }

        private void PushUniforms(Material material)
        {
            float knee = Mathf.Max(_threshold * _softKnee, 1e-4f);
            material.SetVector(FilterId, new Vector4(_threshold, _threshold - knee, 2f * knee, 0.25f / knee));

            material.SetFloat(BloomIntensityId, _bloomIntensity);
            material.SetColor(BloomTintId, _bloomTint);

            material.SetFloat(ExposureId, _exposure);
            material.SetFloat(ContrastId, _contrast);
            material.SetFloat(SaturationId, _saturation);
            material.SetColor(ColorFilterId, _colorFilter);

            material.SetColor(ShadowsId, new Color(_shadowTint.r, _shadowTint.g, _shadowTint.b, _shadowTintAmount));
            material.SetColor(HighlightsId, new Color(_highlightTint.r, _highlightTint.g, _highlightTint.b, _highlightTintAmount));

            material.SetVector(VignetteId, new Vector4(_vignette, 1f - _vignetteSmoothness, _vignetteRoundness, 0f));
            material.SetColor(VignetteColorId, _vignetteColor);

            material.SetFloat(AberrationId, _aberration);
            material.SetFloat(GrainId, _grain);
            material.SetFloat(GrainScaleId, _grainSize);

            // Le grain doit changer a chaque image, sinon c'est une texture collee a
            // l'ecran : l'oeil l'identifie immediatement comme un calque, pas comme du bruit.
            material.SetFloat(SeedId, (Time.frameCount % 1024) * 0.0173f);

            material.SetFloat(LinearModeId, QualitySettings.activeColorSpace == ColorSpace.Linear ? 1f : 0f);
        }

        /// <summary>
        /// Construit la pyramide et renvoie le nombre de niveaux effectivement alloues.
        /// Le niveau 0 contient le resultat final a assembler.
        /// </summary>
        private int BuildBloomPyramid(Material material, RenderTexture source)
        {
            int width = source.width / 2;
            int height = source.height / 2;

            if (width < 8 || height < 8) return 0;

            RenderTextureFormat format = source.format;
            int levels = 0;

            for (int i = 0; i < MaxLevels && i < _iterations; i++)
            {
                if (width < 4 || height < 4) break;

                _chain[i] = RenderTexture.GetTemporary(width, height, 0, format, RenderTextureReadWrite.Default);
                _chain[i].filterMode = FilterMode.Bilinear;
                _chain[i].wrapMode = TextureWrapMode.Clamp;
                levels++;

                width = Mathf.Max(1, width / 2);
                height = Mathf.Max(1, height / 2);
            }

            if (levels == 0) return 0;

            Graphics.Blit(source, _chain[0], material, PassPrefilter);

            for (int i = 1; i < levels; i++)
            {
                Graphics.Blit(_chain[i - 1], _chain[i], material, PassDownsample);
            }

            // Remontee : chaque niveau est AJOUTE au niveau juste au-dessus (passe en
            // Blend One One). On accumule donc du plus flou vers le plus net, ce qui
            // donne une decroissance continue plutot que plusieurs halos superposes.
            material.SetFloat(SampleScaleId, _spread);

            for (int i = levels - 2; i >= 0; i--)
            {
                Graphics.Blit(_chain[i + 1], _chain[i], material, PassUpsample);
            }

            return levels;
        }

        private void ReleaseChain()
        {
            for (int i = 0; i < _chain.Length; i++)
            {
                if (_chain[i] == null) continue;

                RenderTexture.ReleaseTemporary(_chain[i]);
                _chain[i] = null;
            }
        }
    }
}
