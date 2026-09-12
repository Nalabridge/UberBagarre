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
        public Material Bruise;

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

            Texture2D canvas = EditorBuildUtility.CreateOrUpdateFabricTexture(TexturesFolder, "TissuSac", 256,
                new Color(0.42f, 0.33f, 0.26f), 5, 0.13f, 2024);

            m.Prop = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_SandboxProp",
                Color.white, 0.18f, 0f, canvas, new Vector2(4f, 4f));

            // ----------------------------------------------------------------- matieres
            //
            // Chaque matiere du corps a desormais une TEXTURE, et c'est le changement visuel le
            // plus important de tout le projet. Une couleur plate ne reagit a la lumiere que par
            // son orientation : deux surfaces tournees pareil sont rigoureusement identiques,
            // et l'ensemble se lit comme une maquette en plastique. C'est ca que « trop vieux,
            // trop low poly » decrit en realite — pas le nombre de triangles.
            //
            // Le tiling est serre (8 a 14 repetitions) parce que les maillages sont unitaires et
            // mis a l'echelle : un torse et une phalange partagent le meme materiau, donc la
            // trame doit rester fine pour ne jamais devenir un motif visible.

            Texture2D skinGrain = EditorBuildUtility.CreateOrUpdateGrainTexture(TexturesFolder, "GrainPeau", 256,
                new Color(0.74f, 0.57f, 0.47f), 0.07f, 0.045f, 0.022f, 0.045f, 1771);

            Texture2D enemySkinGrain = EditorBuildUtility.CreateOrUpdateGrainTexture(TexturesFolder, "GrainPeauEnnemi", 256,
                new Color(0.66f, 0.50f, 0.42f), 0.075f, 0.05f, 0.024f, 0.045f, 4242);

            Texture2D shirtWeave = EditorBuildUtility.CreateOrUpdateFabricTexture(TexturesFolder, "TissuChemise", 256,
                new Color(0.17f, 0.18f, 0.21f), 4, 0.10f, 909);

            Texture2D enemyShirtWeave = EditorBuildUtility.CreateOrUpdateFabricTexture(TexturesFolder, "TissuChemiseEnnemi", 256,
                new Color(0.36f, 0.12f, 0.13f), 4, 0.11f, 313);

            Texture2D denim = EditorBuildUtility.CreateOrUpdateFabricTexture(TexturesFolder, "TissuDenim", 256,
                new Color(0.20f, 0.23f, 0.31f), 3, 0.14f, 77);

            Texture2D leather = EditorBuildUtility.CreateOrUpdateGrainTexture(TexturesFolder, "GrainCuir", 256,
                new Color(0.10f, 0.10f, 0.11f), 0.05f, 0.06f, 0.018f, 0.085f, 515);

            // La peau accroche un peu la lumiere : une peau parfaitement mate ressemble a de la
            // craie. Un peu seulement — trop, et ca devient du plastique.
            m.Skin = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Skin",
                Color.white, 0.30f, 0f, skinGrain, new Vector2(10f, 10f));

            m.Shirt = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Shirt",
                Color.white, 0.10f, 0f, shirtWeave, new Vector2(12f, 12f));

            m.Pants = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Pants",
                Color.white, 0.08f, 0f, denim, new Vector2(14f, 14f));

            m.Shoe = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Shoe",
                Color.white, 0.34f, 0f, leather, new Vector2(8f, 8f));

            // L'ennemi est teinte differemment : en plein combat, il faut le distinguer
            // instantanement de ses propres mains.
            m.EnemySkin = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_EnemySkin",
                Color.white, 0.30f, 0f, enemySkinGrain, new Vector2(10f, 10f));

            m.EnemyShirt = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_EnemyShirt",
                Color.white, 0.12f, 0f, enemyShirtWeave, new Vector2(12f, 12f));

            // Hematome : violet sombre tirant sur le rouge, et mat. Un bleu brillant ferait
            // tache de peinture ; c'est l'absence de reflet qui le fait lire comme de la peau.
            m.Bruise = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Bruise",
                new Color(0.26f, 0.10f, 0.17f), 0.02f, 0f);

            return m;
        }
    }
}
