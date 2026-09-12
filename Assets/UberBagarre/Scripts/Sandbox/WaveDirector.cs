using System;
using System.Collections.Generic;
using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.Sandbox
{
    /// <summary>
    /// Mode vagues : des adversaires de plus en plus nombreux et de plus en plus solides.
    ///
    /// Pourquoi ça change la nature du test : un duel contre un seul adversaire réglé une fois pour
    /// toutes finit par se jouer toujours pareil, et on ne découvre plus rien. À plusieurs, le
    /// combat pose des questions que le duel ne pose pas — se replacer, ne pas se faire encercler,
    /// choisir qui mettre au sol d'abord, garder de l'endurance pour sortir d'une mauvaise
    /// position. Ce sont ces questions qui révèlent ce qui manque aux mécaniques.
    ///
    /// La difficulté monte par les STATISTIQUES et non par le nombre seul : plus de points de vie,
    /// plus de dégâts, des coups plus rapides. Multiplier les adversaires sans les renforcer rend
    /// les vagues plus longues mais pas plus dures, et allonger un test n'apprend rien.
    ///
    /// Les adversaires sont des copies de celui de la scène : aucun prefab à maintenir, donc aucun
    /// risque de divergence entre ce qu'on teste et ce qu'on a réglé.
    /// </summary>
    public class WaveDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _enemyTemplate;
        [SerializeField] private Combatant _player;

        [Header("Composition")]
        [SerializeField, Min(1)] private int _firstWaveCount = 2;

        [SerializeField, Min(0f)]
        [Tooltip("Adversaires ajoutes par vague.")]
        private float _countPerWave = 0.8f;

        [SerializeField, Min(1)] private int _maxAlive = 8;

        [Header("Montee en difficulte (par vague)")]
        [SerializeField, Min(0f)] private float _healthGrowth = 0.14f;
        [SerializeField, Min(0f)] private float _damageGrowth = 0.10f;
        [SerializeField, Min(0f)] private float _speedGrowth = 0.05f;

        [Header("Placement")]
        [SerializeField, Min(1f)] private float _spawnRadius = 7f;

        [SerializeField, Min(0f)]
        [Tooltip("Delai entre la fin d'une vague et la suivante. C'est la respiration : sans elle, " +
                 "on enchaine sans jamais voir ou on en est.")]
        private float _betweenWaves = 3f;

        [Header("Debug")]
        [SerializeField] private bool _logWaves = true;

        private readonly List<GameObject> _alive = new List<GameObject>();

        private int _wave;
        private float _nextWaveAt;
        private bool _running;

        public int Wave { get { return _wave; } }
        public bool Running { get { return _running; } }
        public int AliveCount { get { return CountAlive(); } }

        /// <summary>Temps restant avant la prochaine vague. 0 si une vague est en cours.</summary>
        public float NextWaveIn
        {
            get { return !_running || CountAlive() > 0 ? 0f : Mathf.Max(0f, _nextWaveAt - Time.time); }
        }

        public event Action<int> WaveStarted;

        public void StartWaves()
        {
            Clear();

            _wave = 0;
            _running = true;
            _nextWaveAt = Time.time;
        }

        public void Stop()
        {
            _running = false;
            Clear();
        }

        /// <summary>Détruit les adversaires de vague encore présents.</summary>
        public void Clear()
        {
            for (int i = 0; i < _alive.Count; i++)
            {
                if (_alive[i] != null) Destroy(_alive[i]);
            }

            _alive.Clear();
        }

        private void Update()
        {
            if (!_running) return;

            // On ne compte que les VIVANTS : un cadavre en ragdoll reste dans la scene, et attendre
            // sa disparition bloquerait les vagues indefiniment.
            if (CountAlive() > 0) return;

            if (Time.time < _nextWaveAt) return;

            SpawnWave();
        }

        private int CountAlive()
        {
            int count = 0;

            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] == null)
                {
                    _alive.RemoveAt(i);
                    continue;
                }

                Combatant combatant = _alive[i].GetComponent<Combatant>();
                if (combatant != null && combatant.IsAlive) count++;
            }

            return count;
        }

        private void SpawnWave()
        {
            if (_enemyTemplate == null)
            {
                Debug.LogWarning("[UberBagarre] WaveDirector : aucun modele d'adversaire, vagues annulees.", this);
                _running = false;
                return;
            }

            _wave++;

            int count = Mathf.Min(_maxAlive,
                _firstWaveCount + Mathf.FloorToInt((_wave - 1) * _countPerWave));

            Vector3 centre = _player != null ? _player.transform.position : transform.position;

            for (int i = 0; i < count; i++)
            {
                // Repartition reguliere en cercle, decalee a chaque vague : deux adversaires qui
                // apparaissent dans la meme capsule se repoussent violemment des la premiere image.
                float angle = (i / (float)count * Mathf.PI * 2f) + _wave * 0.7f;
                Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * _spawnRadius;

                GameObject clone = Instantiate(_enemyTemplate, centre + offset, Quaternion.identity);
                clone.name = "Ennemi (vague " + _wave + " - " + (i + 1) + ")";
                clone.transform.rotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);

                ApplyDifficulty(clone);
                _alive.Add(clone);
            }

            _nextWaveAt = Time.time + _betweenWaves;

            if (_logWaves) Debug.Log("[UberBagarre] Vague " + _wave + " : " + count + " adversaire(s).", this);

            Action<int> started = WaveStarted;
            if (started != null) started(_wave);
        }

        /// <summary>
        /// Renforce un adversaire selon la vague.
        ///
        /// Passe par les overrides de statistiques, donc par le même chemin qu'une amélioration de
        /// personnage : la montée en difficulté n'est pas un cas particulier câblé à part, c'est le
        /// système de stats utilisé normalement.
        /// </summary>
        private void ApplyDifficulty(GameObject enemy)
        {
            Combatant combatant = enemy.GetComponent<Combatant>();
            if (combatant == null || combatant.Stats == null) return;

            int steps = Mathf.Max(0, _wave - 1);

            float health = combatant.Stats.Get(StatType.MaxHealth) * (1f + _healthGrowth * steps);
            float strength = combatant.Stats.Get(StatType.Strength) + 12f * _damageGrowth * steps;
            float speed = combatant.Stats.Get(StatType.AttackSpeed) * (1f + _speedGrowth * steps);

            combatant.Stats.SetOverride(StatType.MaxHealth, health);
            combatant.Stats.SetOverride(StatType.Strength, strength);
            combatant.Stats.SetOverride(StatType.AttackSpeed, speed);

            if (combatant.Health != null) combatant.Health.SetMaxHealth(health, true);
            combatant.ApplyStats();
        }
    }
}
