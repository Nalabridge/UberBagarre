using System;
using System.Collections.Generic;
using UberBagarre.Core;
using UnityEngine;

namespace UberBagarre.Sandbox
{
    /// <summary>
    /// Place les combattants au lancement de la scène.
    ///
    /// Deux modes par entrée :
    /// - "prefab" renseigné    : l'objet est instancié (ce sera le cas de l'ennemi, phase 9) ;
    /// - "existingInstance"    : l'objet est déjà dans la scène, il est simplement téléporté
    ///                           (c'est le cas du joueur : pratique pour le garder visible et éditable).
    ///
    /// Le directeur ne connaît ni le joueur ni l'ennemi : il parle à l'interface ISpawnReceiver.
    /// </summary>
    public class SpawnDirector : MonoBehaviour
    {
        [Serializable]
        public class SpawnRequest
        {
            [Tooltip("Nom lisible, uniquement pour les logs et l'Inspector.")]
            public string label = "Combattant";

            [Tooltip("Ou apparaitre.")]
            public SpawnPoint spawnPoint;

            [Tooltip("Prefab a instancier. Laisse vide pour utiliser 'Existing Instance'.")]
            public GameObject prefab;

            [Tooltip("Objet deja present dans la scene, simplement teleporte au spawn point.")]
            public GameObject existingInstance;

            [Tooltip("Optionnel : oriente l'objet vers ce point au lieu d'utiliser la rotation du spawn point.")]
            public SpawnPoint faceTarget;

            [NonSerialized] public GameObject SpawnedInstance;
        }

        [Header("Configuration")]
        [SerializeField] private bool _spawnOnStart = true;
        [SerializeField] private List<SpawnRequest> _requests = new List<SpawnRequest>();

        /// <summary>Émis après chaque apparition. Les systèmes (HUD, IA, caméra) s'y branchent sans référence directe.</summary>
        public event Action<SpawnRequest, GameObject> Spawned;

        public IReadOnlyList<SpawnRequest> Requests { get { return _requests; } }

        private void Start()
        {
            if (_spawnOnStart) SpawnAll();
        }

        [ContextMenu("Spawner maintenant")]
        public void SpawnAll()
        {
            for (int i = 0; i < _requests.Count; i++)
            {
                Spawn(_requests[i]);
            }
        }

        public GameObject Spawn(SpawnRequest request)
        {
            if (request == null) return null;

            if (request.spawnPoint == null)
            {
                Debug.LogWarning("[UberBagarre] SpawnDirector : aucun spawn point pour '" + request.label + "'.", this);
                return null;
            }

            Quaternion rotation = ResolveRotation(request);
            Vector3 position = request.spawnPoint.Position;

            GameObject instance = request.existingInstance;
            if (request.prefab != null)
            {
                instance = Instantiate(request.prefab, position, rotation);
                instance.name = request.prefab.name + " (" + request.label + ")";
            }

            if (instance == null)
            {
                Debug.LogWarning("[UberBagarre] SpawnDirector : '" + request.label + "' n'a ni prefab ni instance existante.", this);
                return null;
            }

            instance.transform.SetPositionAndRotation(position, rotation);

            // Les composants concernés (CharacterController, visée...) corrigent ce dont ils ont besoin.
            ISpawnReceiver[] receivers = instance.GetComponentsInChildren<ISpawnReceiver>(true);
            for (int i = 0; i < receivers.Length; i++)
            {
                receivers[i].OnSpawned(position, rotation);
            }

            request.SpawnedInstance = instance;

            Action<SpawnRequest, GameObject> handler = Spawned;
            if (handler != null) handler(request, instance);

            return instance;
        }

        private static Quaternion ResolveRotation(SpawnRequest request)
        {
            if (request.faceTarget == null) return request.spawnPoint.Rotation;

            Vector3 flatDirection = request.faceTarget.Position - request.spawnPoint.Position;
            flatDirection.y = 0f;

            if (flatDirection.sqrMagnitude < 0.0001f) return request.spawnPoint.Rotation;

            return Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        }
    }
}
