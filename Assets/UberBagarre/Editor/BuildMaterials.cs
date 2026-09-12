using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Les matériaux de la sandbox, créés une fois et partagés par tout ce qui est généré.
    ///
    /// Ils sont détectés selon le render pipeline actif (voir EditorBuildUtility), donc aucun
    /// matériau rose fluo quel que soit le projet dans lequel on ouvre la scène.
    /// </summary>
    public class BuildMaterials
    {
        private const string MaterialsFolder = "Assets/UberBagarre/Art/Materials";
        private const string TexturesFolder = "Assets/UberBagarre/Art/Textures";

        public Material Floor;
        public Material Wall;
        public Material Prop;
        public Material Skin;
        public Material Shirt;
        public Material Pants;
        public Material Shoe;
        public Material EnemySkin;
        public Material EnemyShirt;

        public static BuildMaterials CreateAll(float floorTiling)
        {
            Texture2D checker = EditorBuildUtility.CreateOrUpdateCheckerTexture(
                TexturesFolder, "CheckerFloor", 256, 8,
                new Color(0.34f, 0.34f, 0.36f), new Color(0.27f, 0.27f, 0.29f));

            BuildMaterials m = new BuildMaterials();

            m.Floor = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_SandboxFloor",
                Color.white, 0.15f, 0f, checker, new Vector2(floorTiling, floorTiling));

            m.Wall = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_SandboxWall",
                new Color(0.52f, 0.51f, 0.49f), 0.1f, 0f);

            m.Prop = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_SandboxProp",
                new Color(0.42f, 0.33f, 0.26f), 0.2f, 0f);

            // Teintes sobres : le jeu vise un rendu credible, pas cartoon.
            m.Skin = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Skin",
                new Color(0.74f, 0.57f, 0.47f), 0.22f, 0f);

            m.Shirt = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Shirt",
                new Color(0.17f, 0.18f, 0.21f), 0.12f, 0f);

            m.Pants = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Pants",
                new Color(0.20f, 0.23f, 0.31f), 0.10f, 0f);

            m.Shoe = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Shoe",
                new Color(0.10f, 0.10f, 0.11f), 0.25f, 0f);

            // L'ennemi est teinte differemment : en plein combat, il faut le distinguer
            // instantanement de ses propres mains.
            m.EnemySkin = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_EnemySkin",
                new Color(0.66f, 0.50f, 0.42f), 0.22f, 0f);

            m.EnemyShirt = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_EnemyShirt",
                new Color(0.36f, 0.12f, 0.13f), 0.14f, 0f);

            return m;
        }
    }
}
