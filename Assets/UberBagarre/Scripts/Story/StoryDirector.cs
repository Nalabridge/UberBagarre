using System.Collections.Generic;
using UberBagarre.Player;
using UnityEngine;

namespace UberBagarre.Story
{
    /// <summary>
    /// Le moteur d'histoire : il déroule une liste d'étapes, rien de plus.
    ///
    /// Il ne connaît PAS le prologue. C'est la séparation qui compte ici : le moteur sait
    /// enchaîner des étapes, afficher un objectif, jouer des répliques et geler le joueur ; le
    /// scénario, lui, est une liste écrite ailleurs. Mélanger les deux donnerait un composant où
    /// changer une réplique demande de relire la boucle de mise à jour.
    ///
    /// Une étape se termine quand TROIS conditions sont réunies : ses répliques sont finies, sa
    /// durée plancher est écoulée, et sa condition de sortie est vraie. Les trois, et pas une
    /// seule — sinon un objectif rempli par hasard pendant un dialogue coupe la réplique au
    /// milieu, et une réplique longue retient une étape que le joueur a déjà accomplie.
    /// </summary>
    public class StoryDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private SubtitleDisplay _subtitles;
        [SerializeField] private ObjectiveDisplay _objectives;

        [Header("Diagnostic")]
        [SerializeField]
        [Tooltip("Journalise chaque changement d'etape. Indispensable quand une etape ne passe pas : " +
                 "sans ca, un scenario bloque est indiscernable d'un scenario termine.")]
        private bool _logBeats = true;

        private readonly List<StoryBeat> _beats = new List<StoryBeat>();
        private int _index = -1;
        private float _beatTime;
        private bool _running;
        private bool _finished;

        public bool IsRunning { get { return _running; } }
        public bool IsFinished { get { return _finished; } }
        public int BeatIndex { get { return _index; } }
        public int BeatCount { get { return _beats.Count; } }

        public string CurrentBeatId
        {
            get
            {
                if (_index < 0 || _index >= _beats.Count) return _finished ? "(termine)" : "(pas demarre)";
                return _beats[_index].Id;
            }
        }

        public float BeatTime { get { return _beatTime; } }

        /// <summary>Démarre (ou redémarre) une séquence.</summary>
        public void Play(List<StoryBeat> beats)
        {
            _beats.Clear();
            if (beats != null) _beats.AddRange(beats);

            _index = -1;
            _finished = false;
            _running = _beats.Count > 0;

            if (_running) EnterPlayable(0);
        }

        /// <summary>Interrompt la séquence et rend la main au joueur.</summary>
        public void Stop()
        {
            if (!_running) return;

            ExitBeat();

            _running = false;
            _index = -1;

            if (_objectives != null) _objectives.Clear();
            if (_subtitles != null) _subtitles.Clear();

            SetGameplay(true);
        }

        /// <summary>Saute directement à une étape, par identifiant. Sert au diagnostic.</summary>
        public bool JumpTo(string beatId)
        {
            for (int i = 0; i < _beats.Count; i++)
            {
                if (_beats[i].Id != beatId) continue;

                ExitBeat();
                EnterBeat(i);
                return true;
            }

            return false;
        }

        private void OnDisable()
        {
            // Une histoire eteinte ne doit pas laisser le joueur fige derriere elle.
            if (_input != null) _input.SetGameplayLock(this, false);
        }

        private void Update()
        {
            if (!_running || _index < 0 || _index >= _beats.Count) return;

            _beatTime += Time.unscaledDeltaTime;

            StoryBeat beat = _beats[_index];

            if (beat.OnUpdate != null) beat.OnUpdate();

            // Passer une réplique : autorisé UNIQUEMENT sur les étapes qui n'attendent rien
            // d'autre que la fin du dialogue. Ailleurs, la même touche sert à agir — répondre au
            // téléphone, monter en voiture — et l'autoriser ici la ferait compter deux fois.
            if (beat.IsComplete == null && _input != null && _input.InteractPressed
                && _subtitles != null && _subtitles.IsSpeaking)
            {
                _subtitles.Skip();
            }

            bool linesDone = _subtitles == null || !_subtitles.IsSpeaking;
            bool waitDone = _beatTime >= beat.MinDuration;
            bool conditionDone = beat.IsComplete == null || beat.IsComplete();

            if (!linesDone || !waitDone || !conditionDone) return;

            Advance();
        }

        /// <summary>
        /// Passe l'étape en cours sans attendre sa condition. Réservé au menu de triche : une
        /// étape sautée peut laisser le décor dans un état que la suite n'attend pas (une cible
        /// debout alors que l'étape suivante la croit au sol).
        /// </summary>
        public void SkipBeat()
        {
            if (!_running || _index < 0 || _index >= _beats.Count) return;

            if (_subtitles != null) _subtitles.Clear();
            Advance();
        }

        private void Advance()
        {
            ExitBeat();
            EnterPlayable(_index + 1);
        }

        /// <summary>Entre dans la première étape à partir de <paramref name="next"/> qui ne demande pas à être sautée.</summary>
        private void EnterPlayable(int next)
        {
            while (next < _beats.Count && _beats[next].SkipIf != null && _beats[next].SkipIf())
            {
                if (_logBeats) Debug.Log("[UberBagarre] Etape sautee : " + _beats[next].Id, this);
                next++;
            }

            if (next >= _beats.Count)
            {
                _running = false;
                _finished = true;
                _index = _beats.Count;

                if (_objectives != null) _objectives.Clear();
                SetGameplay(true);

                if (_logBeats) Debug.Log("[UberBagarre] Sequence terminee.", this);
                return;
            }

            EnterBeat(next);
        }

        private void EnterBeat(int index)
        {
            _index = index;
            _beatTime = 0f;

            StoryBeat beat = _beats[index];

            if (_objectives != null) _objectives.Set(beat.Objective);

            if (_subtitles != null && beat.Lines.Count > 0) _subtitles.Play(beat.Lines);

            SetGameplay(beat.GameplayEnabled);

            if (_logBeats)
            {
                Debug.Log("[UberBagarre] Etape " + (index + 1) + "/" + _beats.Count + " : " + beat.Id, this);
            }

            if (beat.OnEnter != null) beat.OnEnter();
        }

        private void ExitBeat()
        {
            if (_index < 0 || _index >= _beats.Count) return;

            StoryBeat beat = _beats[_index];
            if (beat.OnExit != null) beat.OnExit();
        }

        private void SetGameplay(bool enabled)
        {
            if (_input == null) return;

            _input.SetGameplayLock(this, !enabled);
        }
    }
}
