using System.Collections.Generic;
using UnityEngine;

namespace Game50033
{
    public class EntryPoint : MonoBehaviour
    {
        [SerializeField] private BaseBoss bossPrefab;
        [SerializeField] private CameraMovement _camera;
        [SerializeField] private float panTime;
        [SerializeField] private float focusTime;
        [SerializeField] private BoxCollider arenaCollider;
        private GameSession _session;
        private IReadOnlyList<Player> _suppliedPlayers;
        public IReadOnlyList<Player> Players => _suppliedPlayers
      ?? (_session != null ? _session.LivingPlayers : System.Array.Empty<Player>());
        [SerializeField] private Transform arenaSpawnPosition;
        [SerializeField] private float spacing = 1.5f;

        private bool isTriggered = false;
        private void Awake()
        {
            _session = FindFirstObjectByType<GameSession>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isTriggered)
            {
                isTriggered = true;
                _camera.FocusOn(bossPrefab.transform, panTime, focusTime);
                foreach (var _player in Players)
                {
                    _player.Hitstop.Request(panTime + focusTime + 1);
                }
            }
        }

        private void OnEnable()
        {
            _camera.FocusFinished += HandleStartOfFight;
        }

        private void OnDisable()
        {
            _camera.FocusFinished -= HandleStartOfFight;
        }

        private void HandleStartOfFight()
        {
            gameObject.SetActive(false);
            TeleportAllPlayers();
            arenaCollider.gameObject.SetActive(true);
            bossPrefab.fightStarted = true;
        }

        private void TeleportAllPlayers()
        {
            for (int i = 0; i < Players.Count; i ++)
            {
                Player _player = Players[i];
                float sideOffset = (i - (Players.Count - 1) / 2f) * spacing;
                Vector3 position = arenaSpawnPosition.position + arenaSpawnPosition.right * sideOffset;
                _player.transform.position = position;
            }
        }
    }
}
