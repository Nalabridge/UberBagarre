using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Les matières des accessoires que tiennent les cibles (cigarette, téléphone, bouteille,
    /// bombe, canne, liasse), la fumée et le tag. Construites par l'éditeur : à l'exécution,
    /// on ne fabrique pas de matériau dont le shader pourrait manquer dans une build.
    /// </summary>
    public class TargetActivityKit : MonoBehaviour
    {
        public Material Paper;
        public Material Ember;
        public Material Phone;
        public Material Bottle;
        public Material Can;
        public Material Rod;
        public Material Cash;
        public Material Graffiti;
        public Material Particle;
        public Color SprayColor = new Color(1f, 0.2f, 0.55f, 0.7f);
    }
}
