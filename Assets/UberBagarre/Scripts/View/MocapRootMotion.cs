using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Relais du déplacement contenu dans les animations capturées (le pas d'un crochet, le
    /// recul d'une réaction, la glissade d'une chute). Unity ne le donne qu'au GameObject qui
    /// porte l'Animator — le corps — alors que c'est la racine du personnage, avec sa capsule,
    /// qui doit bouger. Ce composant le transmet au <see cref="MocapDriver"/>, qui décide
    /// combien en garder et le fait passer par la capsule (collisions comprises).
    ///
    /// Sa seule présence change aussi le comportement de l'Animator : le déplacement est « géré
    /// par script », donc le corps ne s'éloigne jamais tout seul de sa racine.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class MocapRootMotion : MonoBehaviour
    {
        [SerializeField] private MocapDriver _driver;

        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void OnAnimatorMove()
        {
            if (_driver == null || _animator == null) return;
            _driver.ApplyRootMotion(_animator.deltaPosition);
        }
    }
}
