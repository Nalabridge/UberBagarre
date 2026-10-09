using System.Reflection;
using UnityEngine;

namespace UberBagarre.Core
{
    /// <summary>
    /// Les touches choisies par le joueur dans les réglages, gardées d'une partie à l'autre.
    ///
    /// Les touches d'origine restent dans l'asset <see cref="InputBindings"/> ; le lecteur
    /// d'entrées en fait une copie au lancement et lui applique les choix du joueur. L'asset
    /// lui-même n'est jamais modifié (dans l'éditeur, le modifier en jeu le changerait pour de
    /// bon) et « Par défaut » sait toujours revenir aux touches d'origine.
    /// </summary>
    public static class KeyRemap
    {
        private const string Prefix = "UberBagarre.Touches.";

        /// <summary>Une action qu'on peut réassigner.</summary>
        public struct Action
        {
            public readonly string Field;
            public readonly string Label;
            public readonly string Group;

            /// <summary>
            /// Les touches de deux actions du même contexte ne peuvent pas se chevaucher. Le
            /// téléphone et les menus ont le leur : les flèches n'y gênent pas le déplacement.
            /// </summary>
            public readonly int Context;

            public readonly string Hint;

            public Action(string field, string label, string group, int context, string hint)
            {
                Field = field;
                Label = label;
                Group = group;
                Context = context;
                Hint = hint;
            }
        }

        public const int Game = 0;
        public const int Menus = 1;

        public static readonly Action[] Actions =
        {
            new Action("moveForward", "Avancer", "DÉPLACEMENT", Game, null),
            new Action("moveBackward", "Reculer", "DÉPLACEMENT", Game, null),
            new Action("moveLeft", "Aller à gauche", "DÉPLACEMENT", Game, null),
            new Action("moveRight", "Aller à droite", "DÉPLACEMENT", Game, null),
            new Action("sprint", "Courir", "DÉPLACEMENT", Game, "Maintenue."),
            new Action("jump", "Sauter", "DÉPLACEMENT", Game, "Au volant : le frein à main."),
            new Action("crouch", "S'accroupir", "DÉPLACEMENT", Game, "En pleine course : une glissade."),

            new Action("attackStraight", "Direct", "COMBAT", Game, "Le coup le plus rapide. Maintenu, il enchaîne. Au volant : le klaxon."),
            new Action("attackHook", "Crochet", "COMBAT", Game, "Plus lent, plus lourd : il passe sur le côté de la garde."),
            new Action("attackUppercut", "Uppercut", "COMBAT", Game, "Sous la garde. Sonne quand il touche le menton."),
            new Action("attackKick", "Coup de pied", "COMBAT", Game, null),
            new Action("attackLowKick", "Balayette", "COMBAT", Game, "Fauche les jambes : l'adversaire tombe."),
            new Action("attackHeadbutt", "Coup de tête", "COMBAT", Game, "Tout près seulement. Il fait mal aux deux."),
            new Action("attackShove", "Bousculer", "COMBAT", Game, "Repousse et casse la garde. Provoque un passant."),
            new Action("guard", "Garde", "COMBAT", Game, "Maintenue. Levée juste avant le coup : une parade, et la riposte est ouverte."),
            new Action("dodge", "Esquive", "COMBAT", Game, "Un pas de côté, dans la direction du déplacement."),

            new Action("interact", "Interagir", "EN VILLE", Game, "Portes, voitures, magasins, et passer une réplique."),
            new Action("phone", "Téléphone", "EN VILLE", Game, null),
            new Action("openMap", "Carte", "EN VILLE", Game, "Ouvre et ferme la grande carte."),

            new Action("phoneUp", "Haut", "TÉLÉPHONE ET MENUS", Menus, null),
            new Action("phoneDown", "Bas", "TÉLÉPHONE ET MENUS", Menus, null),
            new Action("phoneLeft", "Gauche", "TÉLÉPHONE ET MENUS", Menus, null),
            new Action("phoneRight", "Droite", "TÉLÉPHONE ET MENUS", Menus, null),
            new Action("phoneSelect", "Valider", "TÉLÉPHONE ET MENUS", Menus, null),
            new Action("phoneBack", "Retour", "TÉLÉPHONE ET MENUS", Menus, null),

            new Action("toggleObserver", "Caméra libre", "AUTRES", Game, "Une caméra qui tourne autour du joueur ; le jeu continue."),
            new Action("toggleSandboxMenu", "Menu du bac à sable", "AUTRES", Game, "Adversaires, réglages du combat, triche."),
            new Action("toggleDebugOverlay", "Infos de combat", "AUTRES", Game, "Les chiffres du combat, pour comprendre ce qui se passe."),
            new Action("restartFight", "Relancer le combat", "AUTRES", Game, "Bac à sable seulement."),
        };

        /// <summary>Les touches qu'on peut capter (les mêmes que celles que les deux systèmes d'entrée savent lire).</summary>
        public static readonly KeyCode[] Capturable = BuildCapturable();

        private static InputBindings _defaults;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _defaults = null;
        }

        private static KeyCode[] BuildCapturable()
        {
            System.Collections.Generic.List<KeyCode> keys = new System.Collections.Generic.List<KeyCode>(90);
            for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++) keys.Add(k);
            for (KeyCode k = KeyCode.Alpha0; k <= KeyCode.Alpha9; k++) keys.Add(k);
            for (KeyCode k = KeyCode.F1; k <= KeyCode.F12; k++) keys.Add(k);
            keys.AddRange(new[]
            {
                KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Tab, KeyCode.Backspace,
                KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl,
                KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow,
                KeyCode.RightArrow, KeyCode.Escape
            });
            return keys.ToArray();
        }

        private static FieldInfo Field(string name)
        {
            return typeof(InputBindings).GetField(name, BindingFlags.Public | BindingFlags.Instance);
        }

        public static InputBinding Get(InputBindings bindings, string field)
        {
            FieldInfo info = Field(field);
            return bindings != null && info != null ? (InputBinding)info.GetValue(bindings) : default(InputBinding);
        }

        private static void Put(InputBindings bindings, string field, InputBinding binding)
        {
            FieldInfo info = Field(field);
            if (bindings != null && info != null) info.SetValue(bindings, binding);
        }

        /// <summary>
        /// Rend une copie des touches d'origine avec les choix du joueur. À appeler une fois, au
        /// lancement, par le lecteur d'entrées.
        /// </summary>
        public static InputBindings Prepare(InputBindings source)
        {
            if (source == null) return null;
            if (_defaults == null)
            {
                _defaults = Object.Instantiate(source);
                _defaults.name = source.name + " (origine)";
                _defaults.hideFlags = HideFlags.HideAndDontSave;
            }

            InputBindings copy = Object.Instantiate(source);
            copy.name = source.name + " (joueur)";

            for (int i = 0; i < Actions.Length; i++)
            {
                InputBinding saved;
                if (TryLoad(Actions[i].Field, out saved)) Put(copy, Actions[i].Field, saved);
            }

            return copy;
        }

        /// <summary>La touche d'origine d'une action.</summary>
        public static InputBinding Default(string field)
        {
            return _defaults != null ? Get(_defaults, field) : Get(ScriptableObject.CreateInstance<InputBindings>(), field);
        }

        /// <summary>
        /// Donne <paramref name="binding"/> à l'action. Si une autre action du même contexte
        /// l'avait déjà, elle prend l'ancienne touche de celle-ci (un échange, comme dans la
        /// plupart des jeux) ; son nom est rendu dans <paramref name="swapped"/>.
        /// </summary>
        public static void Assign(InputBindings bindings, string field, InputBinding binding, out string swapped)
        {
            swapped = null;
            if (bindings == null) return;

            InputBinding previous = Get(bindings, field);
            int context = ContextOf(field);

            for (int i = 0; i < Actions.Length; i++)
            {
                if (Actions[i].Field == field || Actions[i].Context != context) continue;
                InputBinding other = Get(bindings, Actions[i].Field);
                if (!Overlaps(other, binding)) continue;

                // L'autre action garde sa touche de secours si elle ne gêne pas ; sinon elle la perd.
                InputBinding given = previous;
                given.alternateKey = other.alternateKey != KeyCode.None && !Overlaps(InputBinding.FromKey(other.alternateKey), binding)
                    ? other.alternateKey : KeyCode.None;
                if (given.source == InputSource.Key && given.key == given.alternateKey) given.alternateKey = KeyCode.None;

                Put(bindings, Actions[i].Field, given);
                Save(Actions[i].Field, given);
                swapped = Actions[i].Label;
                break;
            }

            Put(bindings, field, binding);
            Save(field, binding);
        }

        /// <summary>Remet toutes les touches d'origine.</summary>
        public static void ResetAll(InputBindings bindings)
        {
            for (int i = 0; i < Actions.Length; i++)
            {
                PlayerPrefs.DeleteKey(Prefix + Actions[i].Field);
                if (bindings != null) Put(bindings, Actions[i].Field, Default(Actions[i].Field));
            }

            PlayerPrefs.Save();
        }

        public static bool IsDefault(InputBindings bindings, string field)
        {
            InputBinding a = Get(bindings, field);
            InputBinding b = Default(field);
            return a.source == b.source && a.key == b.key && a.mouseButton == b.mouseButton && a.alternateKey == b.alternateKey;
        }

        private static int ContextOf(string field)
        {
            for (int i = 0; i < Actions.Length; i++)
            {
                if (Actions[i].Field == field) return Actions[i].Context;
            }

            return Game;
        }

        /// <summary>Deux touches qui se déclencheraient ensemble.</summary>
        public static bool Overlaps(InputBinding a, InputBinding b)
        {
            if (a.source == InputSource.MouseButton || b.source == InputSource.MouseButton)
            {
                return a.source == b.source && a.mouseButton == b.mouseButton;
            }

            return Same(a.key, b.key) || Same(a.key, b.alternateKey) || Same(a.alternateKey, b.key) || Same(a.alternateKey, b.alternateKey);
        }

        private static bool Same(KeyCode a, KeyCode b)
        {
            return a != KeyCode.None && a == b;
        }

        private static void Save(string field, InputBinding binding)
        {
            string text = binding.source == InputSource.MouseButton
                ? "m:" + binding.mouseButton
                : "k:" + (int)binding.key + ":" + (int)binding.alternateKey;
            PlayerPrefs.SetString(Prefix + field, text);
            PlayerPrefs.Save();
        }

        private static bool TryLoad(string field, out InputBinding binding)
        {
            binding = default(InputBinding);
            string text = PlayerPrefs.GetString(Prefix + field, null);
            if (string.IsNullOrEmpty(text)) return false;

            string[] parts = text.Split(':');
            int a, b;
            if (parts.Length >= 2 && parts[0] == "m" && int.TryParse(parts[1], out a))
            {
                binding = InputBinding.FromMouse(Mathf.Clamp(a, 0, 6));
                return true;
            }

            if (parts.Length >= 2 && parts[0] == "k" && int.TryParse(parts[1], out a))
            {
                if (parts.Length < 3 || !int.TryParse(parts[2], out b)) b = 0;
                binding = InputBinding.FromKeys((KeyCode)a, (KeyCode)b);
                return true;
            }

            return false;
        }
    }
}
