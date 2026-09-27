using System;
using System.Collections.Generic;
using UberBagarre.Story;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// La tenue du joueur : le haut (t-shirt, veste, débardeur) et les couleurs du haut, du
    /// pantalon et des chaussures. Ce qu'on voit de soi (les bras, le torse en baissant les
    /// yeux) et son ombre changent ensemble.
    ///
    /// Les trois coupes sont construites d'avance par l'éditeur (même squelette, même corps) :
    /// changer de haut, c'est changer de maillage sur le même rendu. Les couleurs sont des
    /// copies des matériaux de vêtements, teintées ici.
    ///
    /// Le catalogue (noms, prix, niveau requis) vit aussi ici : c'est la « voie » de la tenue,
    /// affichée par le vestiaire.
    /// </summary>
    public class PlayerWardrobe : MonoBehaviour
    {
        [Serializable]
        public class TopVariant
        {
            public string name;
            public Mesh body;
            public Mesh shadow;
            public Material[] bodyMaterials = new Material[0];
            public Material[] shadowMaterials = new Material[0];
            public string[] bodySlots = new string[0];
            public string[] shadowSlots = new string[0];
        }

        /// <summary>Un article du catalogue : ce qu'il faut pour le porter.</summary>
        public struct Item
        {
            public string Name;
            public Color Color;
            public int Level;
            public int Price;

            public Item(string name, Color color, int level, int price)
            {
                Name = name;
                Color = color;
                Level = level;
                Price = price;
            }
        }

        public static readonly Item[] Tops =
        {
            new Item("T-shirt", Color.white, 1, 0),
            new Item("Veste", Color.white, 2, 180),
            new Item("Débardeur", Color.white, 3, 120)
        };

        public static readonly Item[] ShirtColors =
        {
            new Item("Gris chiné", new Color(0.36f, 0.36f, 0.38f), 1, 0),
            new Item("Noir", new Color(0.05f, 0.05f, 0.06f), 1, 40),
            new Item("Blanc", new Color(0.82f, 0.82f, 0.80f), 1, 40),
            new Item("Rouge", new Color(0.60f, 0.07f, 0.06f), 2, 80),
            new Item("Bleu nuit", new Color(0.08f, 0.12f, 0.30f), 2, 80),
            new Item("Kaki", new Color(0.24f, 0.27f, 0.15f), 3, 110),
            new Item("Bordeaux", new Color(0.32f, 0.04f, 0.08f), 4, 150),
            new Item("Rose Über", new Color(0.92f, 0.18f, 0.52f), 6, 400)
        };

        public static readonly Item[] PantsColors =
        {
            new Item("Jean brut", new Color(0.10f, 0.15f, 0.30f), 1, 0),
            new Item("Noir", new Color(0.05f, 0.05f, 0.06f), 1, 50),
            new Item("Gris", new Color(0.26f, 0.26f, 0.28f), 2, 60),
            new Item("Treillis", new Color(0.20f, 0.22f, 0.13f), 3, 90),
            new Item("Beige", new Color(0.50f, 0.44f, 0.33f), 4, 120),
            new Item("Blanc", new Color(0.80f, 0.79f, 0.76f), 5, 200)
        };

        public static readonly Item[] ShoeColors =
        {
            new Item("Baskets usées", new Color(0.30f, 0.30f, 0.32f), 1, 0),
            new Item("Noires", new Color(0.04f, 0.04f, 0.05f), 1, 60),
            new Item("Blanches", new Color(0.86f, 0.86f, 0.84f), 2, 90),
            new Item("Rouges", new Color(0.62f, 0.06f, 0.05f), 4, 160),
            new Item("Dorées", new Color(0.78f, 0.60f, 0.20f), 7, 600)
        };

        /// <summary>Les quatre familles du vestiaire, dans l'ordre de l'écran.</summary>
        public static Item[] Family(int family)
        {
            switch (family)
            {
                case 0: return Tops;
                case 1: return ShirtColors;
                case 2: return PantsColors;
                default: return ShoeColors;
            }
        }

        public static string FamilyName(int family)
        {
            switch (family)
            {
                case 0: return "HAUT";
                case 1: return "COULEUR DU HAUT";
                case 2: return "PANTALON";
                default: return "CHAUSSURES";
            }
        }

        /// <summary>La clé de déblocage d'un article (ce que la sauvegarde retient).</summary>
        public static string Key(int family, int index)
        {
            switch (family)
            {
                case 0: return "haut:" + index;
                case 1: return "couleur-haut:" + index;
                case 2: return "couleur-bas:" + index;
                default: return "chaussures:" + index;
            }
        }

        [SerializeField] private PlayerProgress _progress;
        [SerializeField] private SkinnedMeshRenderer _body;
        [SerializeField] private SkinnedMeshRenderer _shadow;
        [SerializeField] private TopVariant[] _tops = new TopVariant[0];

        private readonly Dictionary<Material, Material> _tinted = new Dictionary<Material, Material>();
        private string _worn;

        private void OnEnable()
        {
            if (_progress != null) _progress.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            if (_progress != null) _progress.Changed -= Apply;
        }

        private void OnDestroy()
        {
            foreach (Material m in _tinted.Values)
            {
                if (m != null) Destroy(m);
            }

            _tinted.Clear();
        }

        /// <summary>Essayer une tenue sans l'acheter (aperçu du vestiaire).</summary>
        public void Preview(PlayerProgress.Outfit outfit)
        {
            Wear(outfit);
        }

        public void Apply()
        {
            if (_progress == null) return;
            Wear(_progress.CurrentOutfit);
        }

        private void Wear(PlayerProgress.Outfit outfit)
        {
            if (outfit == null || _tops == null || _tops.Length == 0) return;

            string signature = outfit.top + "/" + outfit.shirt + "/" + outfit.pants + "/" + outfit.shoes;
            if (signature == _worn) return;
            _worn = signature;

            TopVariant top = _tops[Mathf.Clamp(outfit.top, 0, _tops.Length - 1)];
            if (top == null) return;

            Color shirt = ShirtColors[Mathf.Clamp(outfit.shirt, 0, ShirtColors.Length - 1)].Color;
            Color pants = PantsColors[Mathf.Clamp(outfit.pants, 0, PantsColors.Length - 1)].Color;
            Color shoes = ShoeColors[Mathf.Clamp(outfit.shoes, 0, ShoeColors.Length - 1)].Color;

            Dress(_body, top.body, top.bodyMaterials, top.bodySlots, shirt, pants, shoes);
            Dress(_shadow, top.shadow, top.shadowMaterials, top.shadowSlots, shirt, pants, shoes);
        }

        private void Dress(SkinnedMeshRenderer renderer, Mesh mesh, Material[] materials, string[] slots, Color shirt, Color pants,
            Color shoes)
        {
            if (renderer == null || mesh == null || materials == null) return;

            Material[] dressed = new Material[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                string slot = slots != null && i < slots.Length ? slots[i] : string.Empty;
                switch (slot)
                {
                    case "Haut": dressed[i] = Tint(materials[i], shirt, "Haut"); break;
                    case "Jean": dressed[i] = Tint(materials[i], pants, "Jean"); break;
                    case "Chaussures": dressed[i] = Tint(materials[i], shoes, "Chaussures"); break;
                    default: dressed[i] = materials[i]; break;
                }
            }

            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = dressed;
        }

        private Material Tint(Material source, Color color, string slot)
        {
            if (source == null) return null;

            Material tinted;
            if (!_tinted.TryGetValue(source, out tinted) || tinted == null)
            {
                tinted = new Material(source);
                tinted.name = source.name + " (" + slot + ")";
                _tinted[source] = tinted;
            }

            if (tinted.HasProperty("_Color")) tinted.SetColor("_Color", color);
            if (tinted.HasProperty("_BaseColor")) tinted.SetColor("_BaseColor", color);
            return tinted;
        }

        public void Configure(SkinnedMeshRenderer body, SkinnedMeshRenderer shadow, TopVariant[] tops)
        {
            _body = body;
            _shadow = shadow;
            _tops = tops ?? new TopVariant[0];
        }
    }
}
