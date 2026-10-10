using UnityEngine;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Events.Domain;

namespace IdleDefenseSurvival.Events.Infrastructure
{
    /// <summary>
    /// Spawns Rift when incident triggers.
    /// Listens to EventService.OnIncidentTriggered.
    /// </summary>
    public class RiftSpawner : MonoBehaviour
    {
        [Header("Rift Prefab")]
        [SerializeField] private GameObject _riftPrefab;

        [Header("Spawn Settings")]
        [SerializeField] private float _spawnRadius = 5f;

        private Transform _player;

        private void Start()
        {
            _player = Player.Player.Instance?.transform;
        }

        private void OnEnable()
        {
            var eventService = ServiceLocator.EventService as EventService;
            if (eventService != null)
            {
                eventService.OnIncidentTriggered += OnIncident;
            }
        }

        private void OnDisable()
        {
            var eventService = ServiceLocator.EventService as EventService;
            if (eventService != null)
            {
                eventService.OnIncidentTriggered -= OnIncident;
            }
        }

        private void OnIncident(string description)
        {
            if (description != null && description.ToLower().Contains("rift"))
            {
                SpawnRift();
            }
        }

        private void SpawnRift()
        {
            if (_riftPrefab == null || _player == null) return;

            Vector2 offset = Random.insideUnitCircle * _spawnRadius;
            Vector3 spawnPos = _player.position + (Vector3)offset;

            GameObject rift = Instantiate(_riftPrefab, spawnPos, Quaternion.identity);
            if (rift.TryGetComponent<RiftBehaviour>(out var behaviour))
            {
                behaviour.Initialize();
            }
        }
    }
}
