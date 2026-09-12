using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Player;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.Sandbox
{
    /// <summary>
    /// Relance le combat d'une touche.
    ///
    /// Sans ça, tester l'équilibrage demande de quitter le mode Play et de le relancer à chaque
    /// KO — soit une dizaine de secondes perdues à chaque essai, et la perte de tous les réglages
    /// tentés en direct. C'est un outil de développement, pas une mécanique de jeu.
    /// </summary>
    public class FightResetter : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private SpawnDirector _spawnDirector;
        [SerializeField] private List<Combatant> _combatants = new List<Combatant>();

        [SerializeField]
        [Tooltip("Affiche un rappel a l'ecran quand le joueur est KO.")]
        private bool _showDeathPrompt = true;

        private bool _playerDown;

        private void Update()
        {
            if (_input != null && _input.RestartFightPressed) Restart();

            _playerDown = false;

            for (int i = 0; i < _combatants.Count; i++)
            {
                Combatant combatant = _combatants[i];
                if (combatant == null || combatant.Faction != Faction.Player) continue;

                _playerDown = !combatant.IsAlive;
            }
        }

        [ContextMenu("Relancer le combat")]
        public void Restart()
        {
            for (int i = 0; i < _combatants.Count; i++)
            {
                Combatant combatant = _combatants[i];
                if (combatant == null) continue;

                combatant.Revive();

                // Une relance doit effacer TOUTES les traces du combat precedent, pas seulement
                // les barres : un combattant a plein de vie couvert de bleus, ou encore couche
                // au sol, raconte le contraire de ce que disent les chiffres.
                KnockdownSystem knockdown = combatant.GetComponentInChildren<KnockdownSystem>(true);
                if (knockdown != null) knockdown.ForceStand();

                BruiseSystem bruises = combatant.GetComponentInChildren<BruiseSystem>(true);
                if (bruises != null) bruises.Clear();
            }

            if (_spawnDirector != null) _spawnDirector.SpawnAll();

            Debug.Log("[UberBagarre] Combat relance.");
        }

        private void OnGUI()
        {
            if (!_showDeathPrompt || !_playerDown) return;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 22;
            style.normal.textColor = new Color(1f, 0.85f, 0.85f);

            GUI.Label(new Rect(0f, Screen.height * 0.42f, Screen.width, 60f), "K.O.  —  R pour relancer", style);
        }
    }
}
