using System;
using UberBagarre.Story;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// L'identification de la cible : regarder quelqu'un assez longtemps pour le reconnaître.
    ///
    /// C'est la scène que le dossier décrit en une phrase — « arriver devant le groupe et une
    /// fois que vous avez repéré l'individu, à vous d'engager » — et c'est une phrase qui cache
    /// une vraie question de conception : comment fait-on « repérer » ?
    ///
    /// Le mauvais réflexe serait de poser un marqueur au-dessus de la cible dès l'arrivée. Le
    /// joueur n'aurait alors rien repéré du tout : il aurait suivi une flèche. La fiche de
    /// signalement — veste rouge, crâne rasé — ne servirait à rien, et la scène perdrait sa
    /// seule idée.
    ///
    /// Ici, personne n'est marqué. Le joueur DÉVISAGE les gens un par un : rester sur quelqu'un
    /// une seconde affiche ce qu'on voit de lui, et c'est au joueur de comparer avec la fiche.
    /// Le marqueur n'apparaît qu'APRÈS, sur celui qu'il a reconnu — il ne désigne pas la cible,
    /// il confirme une décision. Et il reste, parce que reperdre dans la foule quelqu'un qu'on
    /// vient d'identifier ne serait pas une difficulté, juste une corvée.
    /// </summary>
    public class TargetFinder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _camera;
        [SerializeField] private MissionBriefing _briefing;

        [SerializeField]
        [Tooltip("Racine du groupe. Tous les CrowdMember qu'elle contient sont devisageables.")]
        private Transform _crowdRoot;

        [Header("Detection")]
        [SerializeField, Min(1f)] private float _maxDistance = 14f;

        [SerializeField, Range(1f, 30f)]
        [Tooltip("Tolerance de visee, en degres. Trop serre, devisager devient un exercice de " +
                 "precision ; trop large, on identifie tout le groupe d'un seul regard.")]
        private float _coneAngle = 7f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Duree de fixation avant de reconnaitre quelqu'un.")]
        private float _identifyTime = 0.9f;

        [Header("Etat")]
        [SerializeField]
        [Tooltip("Actif uniquement pendant la phase de recherche. Le scenario l'allume et l'eteint.")]
        private bool _searching;

        [Header("Apparence")]
        [SerializeField] private Color _neutral = new Color(0.85f, 0.88f, 0.92f);
        [SerializeField] private Color _targetColor = new Color(1f, 0.28f, 0.26f);
        [SerializeField] private int _fontSize = 14;

        private CrowdMember[] _members = new CrowdMember[0];
        private CrowdMember _looked;
        private CrowdMember _identified;
        private float _lookTime;

        /// <summary>Déclenché une seule fois, quand la cible est reconnue.</summary>
        public event Action<CrowdMember> Identified;

        public bool IsIdentified { get { return _identified != null; } }
        public CrowdMember Target { get { return _identified; } }

        public bool Searching
        {
            get { return _searching; }
            set { _searching = value; }
        }

        private void Awake()
        {
            Collect();
        }

        /// <summary>Recolle la liste du groupe. À appeler si le groupe change en cours de partie.</summary>
        public void Collect()
        {
            _members = _crowdRoot == null
                ? new CrowdMember[0]
                : _crowdRoot.GetComponentsInChildren<CrowdMember>(true);
        }

        private void Update()
        {
            if (!_searching)
            {
                _looked = null;
                _lookTime = 0f;
                return;
            }

            Camera camera = GuiKit.ActiveCamera(_camera);
            if (camera == null) return;

            CrowdMember aimed = FindAimed(camera);

            if (aimed != _looked)
            {
                _looked = aimed;
                _lookTime = 0f;
            }

            if (_looked == null) return;

            _lookTime += Time.deltaTime;

            if (_identified != null || !_looked.IsTarget || _lookTime < _identifyTime) return;

            _identified = _looked;
            _searching = false;

            Action<CrowdMember> handler = Identified;
            if (handler != null) handler(_identified);
        }

        private CrowdMember FindAimed(Camera camera)
        {
            Vector3 origin = camera.transform.position;
            Vector3 forward = camera.transform.forward;

            CrowdMember best = null;
            float bestAngle = _coneAngle;

            for (int i = 0; i < _members.Length; i++)
            {
                CrowdMember member = _members[i];
                if (member == null || !member.isActiveAndEnabled) continue;

                Vector3 toMember = member.LabelPoint - origin;
                float distance = toMember.magnitude;
                if (distance > _maxDistance || distance < 0.2f) continue;

                float angle = Vector3.Angle(forward, toMember);
                if (angle > bestAngle) continue;

                bestAngle = angle;
                best = member;
            }

            return best;
        }

        private void OnGUI()
        {
            Camera camera = GuiKit.ActiveCamera(_camera);
            if (camera == null) return;

            if (_identified != null) DrawMarker(camera, _identified);

            if (!_searching || _looked == null || _looked == _identified) return;

            DrawExamination(camera, _looked);
        }

        /// <summary>Ce qu'on voit de quelqu'un qu'on dévisage, et la jauge de reconnaissance.</summary>
        private void DrawExamination(Camera camera, CrowdMember member)
        {
            Vector2 point;
            if (!GuiKit.WorldToGui(camera, member.LabelPoint, out point)) return;

            float progress = Mathf.Clamp01(_lookTime / _identifyTime);

            const float width = 250f;
            float x = point.x - width * 0.5f;
            float y = point.y - 54f;

            GuiKit.Fill(new Rect(x, y, width, 44f), new Color(0f, 0f, 0f, 0.6f));

            GuiKit.OutlinedLabel(new Rect(x + 8f, y + 4f, width - 16f, 18f),
                member.Description, GuiKit.Style(_fontSize, FontStyle.Bold, TextAnchor.MiddleCenter),
                _neutral, new Color(0f, 0f, 0f, 0.9f), 1.2f);

            GuiKit.OutlinedLabel(new Rect(x + 8f, y + 22f, width - 16f, 16f),
                "comparer au signalement", GuiKit.Style(11, FontStyle.Italic, TextAnchor.MiddleCenter),
                new Color(1f, 1f, 1f, 0.45f), new Color(0f, 0f, 0f, 0.85f), 1f);

            // La jauge ne progresse QUE sur la cible. Sur les autres, elle reste vide : c'est
            // un retour honnête, il n'y a rien à reconnaître.
            if (!member.IsTarget) return;

            Rect bar = new Rect(x + 40f, y + 40f, width - 80f, 3f);
            GuiKit.Fill(bar, new Color(1f, 1f, 1f, 0.2f));
            GuiKit.Fill(new Rect(bar.x, bar.y, bar.width * progress, bar.height), _targetColor);
        }

        private void DrawMarker(Camera camera, CrowdMember member)
        {
            Vector2 point;
            if (!GuiKit.WorldToGui(camera, member.LabelPoint + Vector3.up * 0.28f, out point)) return;

            float bob = Mathf.Sin(Time.unscaledTime * 3f) * 4f;
            float y = point.y + bob;

            // Un chevron, pas un rectangle : trois barres de largeur décroissante suffisent à
            // lire une pointe vers le bas, et ça ne ressemble à aucun autre élément du HUD.
            for (int i = 0; i < 4; i++)
            {
                float w = 22f - i * 5f;

                GuiKit.Fill(new Rect(point.x - w * 0.5f, y - 18f + i * 4f, w, 3f), _targetColor);
            }

            string name = _briefing != null ? _briefing.TargetName : member.DisplayName;

            GuiKit.OutlinedLabel(new Rect(point.x - 140f, y - 44f, 280f, 20f),
                name + "  —  CIBLE", GuiKit.Style(_fontSize, FontStyle.Bold, TextAnchor.MiddleCenter),
                _targetColor, new Color(0f, 0f, 0f, 0.9f), 1.4f);
        }
    }
}
