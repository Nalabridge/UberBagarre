using System;
using System.Collections.Generic;
using UberBagarre.Player;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.Story
{
    /// <summary>
    /// Le courrier, lu pour de vrai.
    ///
    /// La pile d'enveloppes sur la table est la première chose que le jeu montre et la seule
    /// qui explique pourquoi le personnage va accepter. Une réplique « Loyer. Électricité.
    /// Banque. » le dit ; une lettre qu'on OUVRE le fait sentir. Chaque enveloppe arrive à
    /// l'écran, se décachette, la lettre en sort pliée en trois, se déplie, puis le texte
    /// s'imprime ligne à ligne jusqu'au montant — et le tampon rouge tombe.
    ///
    /// Tout est dessiné ici (textures générées, sons synthétisés) : pas d'asset à fournir,
    /// pas d'import à régler, et le contenu des lettres se change dans ce fichier.
    ///
    /// Commandes : E, Espace, Entrée ou clic pour avancer (un premier appui termine
    /// l'animation en cours), Retour arrière pour tout reposer.
    /// </summary>
    [DisallowMultipleComponent]
    public class LetterReader : MonoBehaviour
    {
        private struct Letter
        {
            public string Sender;
            public string SenderLine;
            public Color Brand;
            public string Reference;
            public string Subject;
            public string Body;
            public string AmountLabel;
            public string Amount;
            public string Stamp;
            public string Footer;
        }

        // Chronologie d'une lettre, en secondes.
        private const float EnvelopeIn = 0.45f;
        private const float FlapOpen = 0.85f;
        private const float PullOut = 1.35f;
        private const float Unfold = 2.0f;

        private const float FoldedAspect = 2.12f;   // A4 plie en trois dans une enveloppe DL
        private const float PageAspect = 0.707f;     // A4 deplie

        [SerializeField] private PlayerInputReader _input;

        [SerializeField, Min(10f)]
        [Tooltip("Vitesse d'impression du texte, en caracteres par seconde.")]
        private float _typeSpeed = 190f;

        [SerializeField, Range(0f, 1f)] private float _volume = 0.7f;

        [SerializeField]
        [Tooltip("Adresse du personnage, en tete de chaque lettre.")]
        private string _address = "Monsieur\n14, impasse des Tanneurs\nAppartement 3";

        [SerializeField] private string _date = "Le 12 mars";

        private Letter[] _letters;
        private int _index;
        private float _time;
        private float _open;
        private bool _isOpen;
        private bool _closing;
        private float _reveal;
        private float _typedAt = -1f;
        private float _stampAt = -1f;
        private Action _onClosed;

        private Texture2D _paper;
        private Texture2D _pocket;
        private Texture2D _flap;

        private AudioSource _audio;
        private AudioClip _slide;
        private AudioClip _tear;
        private AudioClip _crinkle;
        private AudioClip _thump;
        private int _lastSound = -1;

        private readonly Dictionary<int, GUIStyle> _styles = new Dictionary<int, GUIStyle>(16);
        private string _bodyCache;
        private int _bodyCacheLength = -1;
        private int _bodyCacheIndex = -1;

        /// <summary>Une lettre est-elle à l'écran ?</summary>
        public bool IsOpen
        {
            get { return _isOpen; }
        }

        private void Awake()
        {
            _letters = BuildLetters();

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.ignoreListenerPause = true;

            _slide = Paper("Glissement", 0.38f, 0.10f, 0.012f, 1);
            _tear = Paper("Dechirure", 0.42f, 0.55f, 0.006f, 2);
            _crinkle = Paper("Depliage", 0.34f, 0.35f, 0.01f, 3);
            _thump = Thump();
        }

        private void OnDisable()
        {
            if (_isOpen) Finish();
        }

        private void OnDestroy()
        {
            DestroyAsset(_paper);
            DestroyAsset(_pocket);
            DestroyAsset(_flap);
            DestroyAsset(_slide);
            DestroyAsset(_tear);
            DestroyAsset(_crinkle);
            DestroyAsset(_thump);
        }

        private static void DestroyAsset(UnityEngine.Object asset)
        {
            if (asset != null) Destroy(asset);
        }

        /// <summary>Ouvre le courrier. <paramref name="onClosed"/> est appelé quand le joueur le repose.</summary>
        public void Open(Action onClosed)
        {
            if (_isOpen)
            {
                _onClosed += onClosed;
                return;
            }

            EnsureTextures();

            _onClosed = onClosed;
            _isOpen = true;
            _closing = false;
            StartLetter(0);

            if (_input != null) _input.SetGameplayLock(this, true);
            ModalScreen.Set(this, true);
        }

        private void StartLetter(int index)
        {
            _index = index;
            _time = 0f;
            _reveal = 0f;
            _typedAt = -1f;
            _stampAt = -1f;
            _lastSound = -1;
            _bodyCacheIndex = -1;
        }

        private void Close()
        {
            _closing = true;
            Play(_slide, 0.5f);
        }

        private void Finish()
        {
            _isOpen = false;
            _closing = false;
            _open = 0f;

            if (_input != null) _input.SetGameplayLock(this, false);
            ModalScreen.Set(this, false);

            Action closed = _onClosed;
            _onClosed = null;
            if (closed != null) closed();
        }

        private void Update()
        {
            // Jeu en pause : rien ne bouge derriere le menu, pas meme ce qui compte en temps reel.
            if (UberBagarre.UI.GameMenu.IsPaused) return;

            if (!_isOpen) return;

            float dt = Time.unscaledDeltaTime;

            if (_closing)
            {
                _open = Mathf.MoveTowards(_open, 0f, dt / 0.3f);
                if (_open <= 0f) Finish();
                return;
            }

            _open = Mathf.MoveTowards(_open, 1f, dt / 0.25f);
            _time += dt;

            Sounds();

            Letter letter = _letters[_index];
            int total = letter.Subject.Length + letter.Body.Length;

            if (_time >= Unfold)
            {
                _reveal = Mathf.Min(total, _reveal + dt * _typeSpeed);
                if (_reveal >= total && _typedAt < 0f) _typedAt = _time;
            }

            // Le tampon tombe un temps apres le montant : c'est le dernier mot de la lettre.
            if (_typedAt >= 0f && _stampAt < 0f && _time >= _typedAt + 0.45f) Stamp();

            if (BackPressed())
            {
                Close();
                return;
            }

            if (_time < 0.25f || !NextPressed()) return;

            if (_time < Unfold)
            {
                _time = Unfold;
                _lastSound = 3;
            }
            else if (_stampAt < 0f)
            {
                _reveal = total;
                if (_typedAt < 0f) _typedAt = _time;
                Stamp();
            }
            else if (_index + 1 < _letters.Length)
            {
                StartLetter(_index + 1);
            }
            else
            {
                Close();
            }
        }

        private void Stamp()
        {
            _stampAt = _time;
            Play(_thump, 1f);
        }

        private void Sounds()
        {
            int step = _time < EnvelopeIn ? 0 : _time < FlapOpen ? 1 : _time < PullOut ? 2 : 3;
            if (step == _lastSound) return;

            _lastSound = step;
            switch (step)
            {
                case 0: Play(_slide, 0.55f); break;
                case 1: Play(_tear, 0.8f); break;
                case 2: Play(_slide, 0.4f); break;
                default: Play(_crinkle, 0.6f); break;
            }
        }

        private bool NextPressed()
        {
            if (_input == null) return false;
            if (_input.InteractPressed) return true;
            if (_input.Provider == null || _input.Bindings == null) return false;

            return _input.Provider.GetPressedThisFrame(_input.Bindings.jump) ||
                   _input.Provider.GetPressedThisFrame(_input.Bindings.attackStraight) ||
                   _input.Provider.GetPressedThisFrame(_input.Bindings.phoneSelect);
        }

        private bool BackPressed()
        {
            if (_input == null || _input.Provider == null || _input.Bindings == null) return false;
            return _input.Provider.GetPressedThisFrame(_input.Bindings.phoneBack);
        }

        // --------------------------------------------------------------- dessin

        private void OnGUI()
        {
            if (!_isOpen || _open <= 0.001f) return;

            GUI.depth = -20;

            float sw = UnityEngine.Screen.width;
            float sh = UnityEngine.Screen.height;

            float previousAlpha = GuiKit.Alpha;
            Matrix4x4 matrix = GUI.matrix;
            Color color = GUI.color;

            GuiKit.Alpha = _open;
            GuiKit.Fill(new Rect(0f, 0f, sw, sh), new Color(0.02f, 0.02f, 0.03f, 0.78f));

            // Un halo chaud derriere la feuille : une lampe de table, pas un ecran d'ordinateur.
            GuiKit.Disc(new Rect(sw * 0.5f - sh * 0.75f, sh * 0.5f - sh * 0.62f, sh * 1.5f, sh * 1.24f),
                new Color(1f, 0.78f, 0.5f, 0.1f));

            Draw(sw, sh);

            GUI.matrix = matrix;
            GUI.color = color;

            DrawHint(sw, sh);
            GuiKit.Alpha = previousAlpha;
        }

        private void Draw(float sw, float sh)
        {
            Letter letter = _letters[_index];

            // Enveloppe : taille et position de repos.
            float ew = Mathf.Min(sw * 0.7f, sh * 0.66f);
            float eh = ew * 0.5f;
            Vector2 rest = new Vector2(sw * 0.5f, sh * 0.62f);

            // Feuille : pliee (taille de l'enveloppe) puis depliee (taille de lecture).
            float fw = ew * 0.94f;
            float fh = fw / FoldedAspect;
            float ph = sh * 0.9f;
            float pw = ph * PageAspect;

            float t = _time;

            // --- l'enveloppe arrive par le bas, en tournant un peu
            float arrive = Ease(Mathf.Clamp01(t / EnvelopeIn));
            Vector2 envelope = Vector2.Lerp(new Vector2(rest.x, sh + eh), rest, arrive);
            float envelopeAngle = Mathf.Lerp(-11f, -2f, arrive);
            float envelopeAlpha = 1f;

            // --- puis repart vers le bas quand la lettre se deplie
            if (t > PullOut)
            {
                float leave = Mathf.Clamp01((t - PullOut) / (Unfold - PullOut));
                envelope.y += leave * leave * sh * 0.7f;
                envelopeAngle += leave * 9f;
                envelopeAlpha = 1f - leave;
            }

            float open = Mathf.Clamp01((t - EnvelopeIn) / (FlapOpen - EnvelopeIn));
            float flap = Mathf.Cos(Ease(open) * Mathf.PI);   // 1 ferme, -1 ouvert

            float pull = Ease(Mathf.Clamp01((t - FlapOpen) / (PullOut - FlapOpen)));
            float unfold = Ease(Mathf.Clamp01((t - PullOut) / (Unfold - PullOut)));

            Rect envelopeRect = new Rect(envelope.x - ew * 0.5f, envelope.y - eh * 0.5f, ew, eh);

            // Une fois la lettre sortie, l'enveloppe entiere passe derriere elle.
            bool behind = t > PullOut;

            if (envelopeAlpha > 0.01f)
            {
                Rotate(envelopeAngle, envelope);

                // Dos de l'enveloppe (l'interieur, plus sombre) et rabat ouvert derriere la lettre.
                Tint(new Color(0.8f, 0.78f, 0.74f, envelopeAlpha));
                GUI.DrawTexture(envelopeRect, GuiKit.Pixel);
                if (flap < 0f) DrawFlap(envelopeRect, flap, envelopeAlpha);
                if (behind) DrawPocket(envelopeRect, flap, envelopeAlpha);

                GUI.matrix = Matrix4x4.identity;
            }

            // --- la lettre
            Vector2 foldedCentre = envelope + new Vector2(0f, Mathf.Lerp(eh * 0.04f, -eh * 0.78f, pull));
            Vector2 pageCentre = new Vector2(sw * 0.5f, sh * 0.5f);

            float width = Mathf.Lerp(fw, pw, unfold);
            float fullHeight = Mathf.Lerp(fw / PageAspect, ph, unfold);
            float height = Mathf.Lerp(fh, fullHeight, unfold);
            Vector2 centre = Vector2.Lerp(foldedCentre, pageCentre, unfold);
            float angle = Mathf.Lerp(envelopeAngle, 0f, unfold);

            if (t >= FlapOpen || flap < 0f)
            {
                Rect page = new Rect(centre.x - width * 0.5f, centre.y - height * 0.5f, width, height);

                Rotate(angle, centre);
                DrawPage(page, letter, unfold, t >= Unfold);
                GUI.matrix = Matrix4x4.identity;
            }

            if (envelopeAlpha > 0.01f && !behind)
            {
                Rotate(envelopeAngle, envelope);
                DrawPocket(envelopeRect, flap, envelopeAlpha);
                GUI.matrix = Matrix4x4.identity;
            }
        }

        /// <summary>Face avant (la poche) par-dessus le bas de la lettre, et rabat fermé par-dessus tout.</summary>
        private void DrawPocket(Rect envelope, float flap, float alpha)
        {
            Tint(new Color(0.96f, 0.95f, 0.92f, alpha));
            GUI.DrawTexture(envelope, _pocket);

            // Le bandeau rouge de la relance, le meme que sur la table.
            GuiKit.Fill(new Rect(envelope.x + envelope.width * 0.07f, envelope.y + envelope.height * 0.62f,
                envelope.width * 0.2f, envelope.height * 0.08f), new Color(0.72f, 0.1f, 0.1f, 0.9f * alpha));

            if (flap >= 0f) DrawFlap(envelope, flap, alpha);
        }

        private void DrawFlap(Rect envelope, float flap, float alpha)
        {
            float h = envelope.height * 0.6f * Mathf.Abs(flap);
            if (h < 0.5f) return;

            if (flap >= 0f)
            {
                Tint(new Color(0.93f, 0.92f, 0.89f, alpha));
                GUI.DrawTexture(new Rect(envelope.x, envelope.y, envelope.width, h), _flap);
            }
            else
            {
                // Rabat ouvert : on voit sa face interieure, retournee au-dessus de l'enveloppe.
                Tint(new Color(0.78f, 0.76f, 0.72f, alpha));
                GUI.DrawTextureWithTexCoords(new Rect(envelope.x, envelope.y - h, envelope.width, h), _flap,
                    new Rect(0f, 1f, 1f, -1f));
            }
        }

        private void DrawPage(Rect page, Letter letter, float unfold, bool reading)
        {
            // Ombre douce sous la feuille.
            for (int i = 1; i <= 4; i++)
            {
                float grow = i * page.height * 0.006f;
                GuiKit.Fill(new Rect(page.x - grow + page.height * 0.01f, page.y - grow + page.height * 0.015f,
                    page.width + grow * 2f, page.height + grow * 2f), new Color(0f, 0f, 0f, 0.07f));
            }

            Tint(Color.white);
            GUI.DrawTexture(page, _paper);

            // Les deux plis : la feuille a ete pliee en trois, et ca se voit encore.
            float crease = Mathf.Lerp(0.05f, 0.14f, unfold);
            for (int i = 1; i <= 2; i++)
            {
                float y = page.y + page.height * i / 3f;
                GuiKit.Fill(new Rect(page.x, y - 1f, page.width, 1f), new Color(0f, 0f, 0f, crease));
                GuiKit.Fill(new Rect(page.x, y, page.width, 1f), new Color(1f, 1f, 1f, crease * 1.6f));
            }

            float u = page.height / 1000f;
            float m = 64f * u;

            // Tant qu'elle est pliee, la lettre ne montre que son en-tete, de loin.
            if (!reading)
            {
                GuiKit.Fill(new Rect(page.x + m, page.y + m * 0.8f, 46f * u, 46f * u), letter.Brand);
                for (int i = 0; i < 9; i++)
                {
                    float y = page.y + page.height * (0.36f + i * 0.045f);
                    float w = (page.width - 2f * m) * (i % 3 == 2 ? 0.62f : 0.95f);
                    GuiKit.Fill(new Rect(page.x + m, y, w, 6f * u), new Color(0.2f, 0.2f, 0.22f, 0.18f * unfold));
                }

                return;
            }

            Color ink = new Color(0.1f, 0.1f, 0.12f);
            Color grey = new Color(0.42f, 0.42f, 0.45f);

            // --- en-tete de l'expediteur
            GuiKit.Fill(new Rect(page.x + m, page.y + m, 52f * u, 52f * u), letter.Brand);
            GuiKit.Fill(new Rect(page.x + m + 14f * u, page.y + m + 14f * u, 24f * u, 24f * u), new Color(1f, 1f, 1f, 0.85f));

            Text(new Rect(page.x + m + 68f * u, page.y + m - 4f * u, page.width, 34f * u), letter.Sender,
                Size(26f * u), true, TextAnchor.UpperLeft, false, letter.Brand);
            Text(new Rect(page.x + m + 68f * u, page.y + m + 30f * u, page.width, 22f * u), letter.SenderLine,
                Size(14f * u), false, TextAnchor.UpperLeft, false, grey);

            // --- destinataire, date, reference
            Text(new Rect(page.x + page.width * 0.55f, page.y + m + 110f * u, page.width * 0.4f, 80f * u), _address,
                Size(16f * u), false, TextAnchor.UpperLeft, false, ink);
            Text(new Rect(page.x + m, page.y + m + 205f * u, page.width - 2f * m, 24f * u), _date,
                Size(16f * u), false, TextAnchor.UpperRight, false, ink);
            Text(new Rect(page.x + m, page.y + m + 240f * u, page.width - 2f * m, 22f * u), letter.Reference,
                Size(14f * u), false, TextAnchor.UpperLeft, false, grey);

            // --- l'objet puis le corps, imprimes
            int subject = Mathf.Min(letter.Subject.Length, Mathf.FloorToInt(_reveal));
            int body = Mathf.Clamp(Mathf.FloorToInt(_reveal) - letter.Subject.Length, 0, letter.Body.Length);

            Text(new Rect(page.x + m, page.y + m + 272f * u, page.width - 2f * m, 30f * u),
                Revealed(letter.Subject, subject, -1),
                Size(18f * u), true, TextAnchor.UpperLeft, true, ink);

            Text(new Rect(page.x + m, page.y + m + 322f * u, page.width - 2f * m, 400f * u),
                Revealed(letter.Body, body, _index),
                Size(16.5f * u), false, TextAnchor.UpperLeft, true, ink);

            // --- le montant, une fois le texte imprime
            if (_typedAt >= 0f)
            {
                float a = Mathf.Clamp01((_time - _typedAt) / 0.3f);
                float previous = GuiKit.Alpha;
                GuiKit.Alpha *= a;

                Rect box = new Rect(page.x + m, page.yMax - m - 150f * u, page.width - 2f * m, 92f * u);
                GuiKit.Fill(box, new Color(letter.Brand.r, letter.Brand.g, letter.Brand.b, 0.07f));
                GuiKit.Outline(box, Mathf.Max(1f, 2f * u), letter.Brand);

                Text(new Rect(box.x + 18f * u, box.y, box.width * 0.55f, box.height), letter.AmountLabel,
                    Size(15f * u), true, TextAnchor.MiddleLeft, true, grey);
                Text(new Rect(box.x, box.y, box.width - 18f * u, box.height), letter.Amount,
                    Size(38f * u), true, TextAnchor.MiddleRight, false, new Color(0.72f, 0.08f, 0.08f));

                Text(new Rect(page.x + m, page.yMax - m - 34f * u, page.width - 2f * m, 30f * u), letter.Footer,
                    Size(11f * u), false, TextAnchor.UpperCenter, true, grey);

                GuiKit.Alpha = previous;
            }

            // --- le tampon, qui tombe
            if (_stampAt >= 0f) DrawStamp(page, letter, u);
        }

        private void DrawStamp(Rect page, Letter letter, float u)
        {
            float t = _time - _stampAt;
            float land = Mathf.Clamp01(t / 0.11f);
            float scale = Mathf.Lerp(1.9f, 1f, land * land);
            float alpha = Mathf.Lerp(0f, 0.82f, land);

            Vector2 centre = new Vector2(page.x + page.width * 0.63f, page.y + page.height * 0.19f);

            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(-13f, centre);
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), centre);

            GUIStyle style = Style(Size(34f * u), true, TextAnchor.MiddleCenter, false);
            float width = style.CalcSize(new GUIContent(letter.Stamp)).x + 44f * u;
            Rect rect = new Rect(centre.x - width * 0.5f, centre.y - 34f * u, width, 68f * u);

            Color red = new Color(0.78f, 0.06f, 0.08f, alpha);
            float previous = GuiKit.Alpha;
            GuiKit.Outline(rect, Mathf.Max(2f, 5f * u), red);
            GuiKit.Outline(new Rect(rect.x + 8f * u, rect.y + 8f * u, rect.width - 16f * u, rect.height - 16f * u),
                Mathf.Max(1f, 1.6f * u), red);
            GuiKit.Alpha = previous;

            Text(rect, letter.Stamp, Size(34f * u), true, TextAnchor.MiddleCenter, false, red);

            GUI.matrix = saved;
        }

        private void DrawHint(float sw, float sh)
        {
            if (_closing) return;

            float unit = sh / 1080f;
            string text;

            if (_stampAt < 0f) text = "E : passer";
            else if (_index + 1 < _letters.Length) text = "E : lettre suivante  (" + (_index + 1) + "/" + _letters.Length + ")";
            else text = "E : reposer le courrier";

            GUIStyle style = GuiKit.Style(Mathf.Max(10, Mathf.RoundToInt(18f * unit)), FontStyle.Bold, TextAnchor.MiddleRight);
            GuiKit.OutlinedLabel(new Rect(sw - 520f * unit, sh - 58f * unit, 490f * unit, 30f * unit), text, style,
                new Color(1f, 1f, 1f, 0.75f), new Color(0f, 0f, 0f, 0.9f), 1f);

            GUIStyle small = GuiKit.Style(Mathf.Max(9, Mathf.RoundToInt(15f * unit)), FontStyle.Normal, TextAnchor.MiddleRight);
            GuiKit.OutlinedLabel(new Rect(sw - 520f * unit, sh - 32f * unit, 490f * unit, 24f * unit), "RETOUR ARRIÈRE : tout reposer",
                small, new Color(1f, 1f, 1f, 0.45f), new Color(0f, 0f, 0f, 0.9f), 1f);
        }

        // --------------------------------------------------------------- texte

        /// <summary>
        /// Le texte entier, dont la fin est rendue transparente : les mots ne sautent pas d'une
        /// ligne à l'autre pendant qu'ils s'impriment, puisque la mise en page est déjà celle
        /// du texte complet.
        /// </summary>
        private string Revealed(string text, int count, int cacheIndex)
        {
            count = Mathf.Clamp(count, 0, text.Length);
            if (count >= text.Length) return text;

            if (cacheIndex >= 0 && cacheIndex == _bodyCacheIndex && count == _bodyCacheLength) return _bodyCache;

            string result = text.Substring(0, count) + "<color=#00000000>" + text.Substring(count) + "</color>";

            if (cacheIndex >= 0)
            {
                _bodyCache = result;
                _bodyCacheIndex = cacheIndex;
                _bodyCacheLength = count;
            }

            return result;
        }

        private static int Size(float pixels)
        {
            return Mathf.Max(8, Mathf.RoundToInt(pixels));
        }

        private void Text(Rect rect, string text, int size, bool bold, TextAnchor anchor, bool wrap, Color color)
        {
            GUIStyle style = Style(size, bold, anchor, wrap);
            Color previous = GUI.contentColor;
            color.a *= GuiKit.Alpha;
            GUI.contentColor = color;
            GUI.Label(rect, text, style);
            GUI.contentColor = previous;
        }

        private GUIStyle Style(int size, bool bold, TextAnchor anchor, bool wrap)
        {
            int key = Mathf.Clamp(size, 0, 4095) | (bold ? 1 << 12 : 0) | ((int)anchor << 13) | (wrap ? 1 << 20 : 0);

            GUIStyle style;
            if (_styles.TryGetValue(key, out style) && style != null) return style;

            style = new GUIStyle(GUI.skin.label);
            style.fontSize = size;
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            style.alignment = anchor;
            style.wordWrap = wrap;
            style.richText = true;
            style.padding = new RectOffset(0, 0, 0, 0);
            style.normal.textColor = Color.white;

            _styles[key] = style;
            return style;
        }

        private static void Tint(Color color)
        {
            color.a *= GuiKit.Alpha;
            GUI.color = color;
        }

        private static void Rotate(float angle, Vector2 pivot)
        {
            GUI.matrix = Matrix4x4.identity;
            if (Mathf.Abs(angle) > 0.01f) GUIUtility.RotateAroundPivot(angle, pivot);
        }

        private static float Ease(float t)
        {
            return t * t * (3f - 2f * t);
        }

        // --------------------------------------------------------------- contenu

        /// <summary>
        /// Les trois lettres. Les montants sont ceux du reste du jeu : le loyer de 640 € de la
        /// progression, le découvert de 1 240,18 € affiché par la banque sur le téléphone, et
        /// la fin de la trêve hivernale qui fait dire à Sami « t'es dehors en avril ».
        /// </summary>
        private static Letter[] BuildLetters()
        {
            return new[]
            {
                new Letter
                {
                    Sender = "AGENCE IMMOBILIÈRE DU CANAL",
                    SenderLine = "Gestion locative  ·  8, quai des Tanneurs",
                    Brand = new Color(0.13f, 0.3f, 0.52f),
                    Reference = "Dossier LOC-2291  ·  Lot n° 14",
                    Subject = "Objet : 3e RELANCE — LOYER IMPAYÉ",
                    Body = "Monsieur,\n\n" +
                           "Malgré nos courriers du 3 et du 10 mars, votre loyer du mois de février reste impayé à ce jour.\n\n" +
                           "Nous vous rappelons que le bail vous oblige au paiement du loyer à son terme. Sans règlement " +
                           "de la totalité des sommes dues sous huit jours, votre dossier sera transmis à notre huissier " +
                           "en vue d'une procédure d'expulsion, exécutable dès la fin de la trêve hivernale, le 31 mars.\n\n" +
                           "Veuillez agréer, Monsieur, nos salutations distinguées.\n\n" +
                           "Le service contentieux",
                    AmountLabel = "RESTE DÛ\nloyer de février + frais de relance",
                    Amount = "655,00 €",
                    Stamp = "DERNIER AVIS",
                    Footer = "Agence Immobilière du Canal — SARL au capital de 8 000 € — Carte professionnelle G-0417",
                },
                new Letter
                {
                    Sender = "VOLTA ÉNERGIE",
                    SenderLine = "Service clients particuliers",
                    Brand = new Color(0.9f, 0.45f, 0.05f),
                    Reference = "Contrat n° 4471 0892  ·  Facture de février",
                    Subject = "Objet : AVIS AVANT COUPURE",
                    Body = "Monsieur,\n\n" +
                           "Votre facture d'électricité n'a pas été réglée à sa date d'échéance, malgré notre premier rappel.\n\n" +
                           "Sans paiement de votre part avant le 18 mars, la puissance de votre compteur sera réduite, " +
                           "puis la fourniture d'électricité de votre logement pourra être suspendue. Des frais " +
                           "d'intervention vous seront alors facturés.\n\n" +
                           "Si vous rencontrez des difficultés de paiement, le service social de votre commune peut " +
                           "vous accompagner.",
                    AmountLabel = "À RÉGLER AVANT LE 18/03",
                    Amount = "187,43 €",
                    Stamp = "IMPAYÉ",
                    Footer = "Volta Énergie — fournisseur d'électricité — Ce courrier ne nécessite pas de réponse s'il a été réglé.",
                },
                new Letter
                {
                    Sender = "CRÉDIT DU FAUBOURG",
                    SenderLine = "Votre agence  ·  2, place de la Mairie",
                    Brand = new Color(0.5f, 0.07f, 0.12f),
                    Reference = "Compte courant n° •••• 0417",
                    Subject = "Objet : INCIDENT DE PAIEMENT — CARTE SUSPENDUE",
                    Body = "Monsieur,\n\n" +
                           "Le solde de votre compte courant est débiteur de 1 240,18 €, au-delà de l'autorisation " +
                           "de découvert de 400,00 € qui vous avait été consentie.\n\n" +
                           "Deux prélèvements ont été rejetés ce mois-ci, entraînant des frais de 20,00 € chacun. Par " +
                           "mesure de précaution, votre carte bancaire est suspendue jusqu'à la régularisation de " +
                           "votre situation.\n\n" +
                           "Nous vous invitons à prendre contact avec votre conseiller dans les meilleurs délais.",
                    AmountLabel = "SOLDE DU COMPTE",
                    Amount = "-1 240,18 €",
                    Stamp = "URGENT",
                    Footer = "Crédit du Faubourg — société coopérative de banque — Médiateur : voir conditions générales.",
                },
            };
        }

        // --------------------------------------------------------------- textures

        private void EnsureTextures()
        {
            if (_paper == null) _paper = PaperTexture();
            if (_pocket == null) _pocket = ShapeTexture(true);
            if (_flap == null) _flap = ShapeTexture(false);
        }

        /// <summary>Papier blanc cassé, un grain très fin et les bords à peine plus sombres.</summary>
        private static Texture2D PaperTexture()
        {
            const int w = 256;
            const int h = 362;

            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;

            System.Random random = new System.Random(12);
            Color[] pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = x / (w - 1f) - 0.5f;
                    float v = y / (h - 1f) - 0.5f;
                    float edge = 1f - 0.05f * Mathf.Pow(Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) * 2f, 6f);
                    float grain = 1f + ((float)random.NextDouble() - 0.5f) * 0.025f;
                    float k = edge * grain;

                    pixels[y * w + x] = new Color(0.975f * k, 0.965f * k, 0.935f * k, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// La poche de l'enveloppe (<paramref name="pocket"/>) : un rectangle entaillé en V par
        /// le haut, avec les plis des rabats latéraux. Sinon le rabat : un triangle, pointe en bas.
        /// </summary>
        private static Texture2D ShapeTexture(bool pocket)
        {
            const int w = 512;
            const int h = 256;
            const float depth = 0.52f;

            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;

            Color[] pixels = new Color[w * h];
            float soft = 1.5f / h;

            for (int y = 0; y < h; y++)
            {
                float top = 1f - y / (h - 1f);   // 0 en haut, 1 en bas

                for (int x = 0; x < w; x++)
                {
                    float u = x / (w - 1f);
                    float tent = 1f - Mathf.Abs(2f * u - 1f);
                    float alpha;
                    float shade = 1f;

                    if (pocket)
                    {
                        // Transparent au-dessus du V.
                        alpha = Mathf.Clamp01((top - depth * tent) / soft + 0.5f);

                        // Plis des rabats lateraux : des coins du bas vers le fond du V.
                        float fold = Mathf.Abs(top - (1f - (1f - depth - 0.06f) * tent));
                        if (fold < 0.006f) shade = 0.9f;
                        else if (top > 1f - (1f - depth - 0.06f) * tent) shade = 0.97f;
                    }
                    else
                    {
                        alpha = Mathf.Clamp01((tent - top) / soft + 0.5f);
                        shade = Mathf.Lerp(1f, 0.93f, top);
                    }

                    pixels[y * w + x] = new Color(shade, shade, shade, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // --------------------------------------------------------------- sons

        private void Play(AudioClip clip, float volume)
        {
            if (_audio != null && clip != null) _audio.PlayOneShot(clip, volume * _volume);
        }

        private const int SampleRate = 22050;

        /// <summary>
        /// Papier : du bruit filtré, découpé en grains. Peu de grains = un glissement ; beaucoup
        /// de grains serrés et secs = une déchirure ; entre les deux = une feuille qu'on déplie.
        /// </summary>
        private static AudioClip Paper(string name, float seconds, float crackle, float grain, int seed)
        {
            int length = Mathf.RoundToInt(seconds * SampleRate);
            float[] data = new float[length];
            System.Random random = new System.Random(seed);

            float low = 0f;
            float band = 0f;
            float burst = 0f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)length;
                float noise = (float)random.NextDouble() * 2f - 1f;

                low += (noise - low) * 0.35f;
                band += (low - band) * 0.05f;
                float hiss = low - band;

                // Nouveaux grains : amplitude au hasard, retombee rapide.
                if (random.NextDouble() < crackle * 0.02f) burst = 0.4f + (float)random.NextDouble() * 0.6f;
                burst *= 1f - 1f / (grain * SampleRate);

                float envelope = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                data[n] = hiss * (0.35f + burst * crackle * 1.6f) * envelope;
            }

            AudioClip clip = AudioClip.Create(name, length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Le tampon : un choc sourd sur la table, et le claquement du caoutchouc.</summary>
        private static AudioClip Thump()
        {
            int length = Mathf.RoundToInt(0.3f * SampleRate);
            float[] data = new float[length];
            System.Random random = new System.Random(4);
            float phase = 0f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)SampleRate;
                float frequency = 70f + 80f * Mathf.Exp(-t / 0.02f);
                phase += frequency / SampleRate;
                phase -= Mathf.Floor(phase);

                float body = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t / 0.06f);
                float click = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t / 0.004f) * 0.6f;
                data[n] = (body + click) * 0.9f;
            }

            AudioClip clip = AudioClip.Create("Tampon", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
