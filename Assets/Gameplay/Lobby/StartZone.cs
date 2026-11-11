namespace Game50033
{
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// When every joined player has spawned and is standing inside this box, counts down
    /// and then starts the game. Leaving the zone (or a new player joining) cancels the countdown.
    /// </summary>
    public sealed class StartZone : MonoBehaviour
    {
        [SerializeField] private LobbyManager lobby;
        [SerializeField] private StartGameSequence startSequence;
        [Tooltip("Size of the zone in local space, centred on this transform.")]
        [SerializeField] private Vector3 size = new(4f, 2f, 2f);
        [SerializeField, Min(0f)] private float countdownSeconds = 3f;
        [SerializeField] private Text statusLabel;

        private float _remaining;
        private bool _counting;
        private bool _started;

        public bool IsCountingDown => _counting;

        private void Update()
        {
            if (_started || lobby == null) return;

            var inside = CountSpawnedPlayersInside();
            var total = lobby.Players.Count;
            var everyoneReady = lobby.AllPlayersSpawned && inside == total;

            if (!everyoneReady)
            {
                _counting = false;
                SetStatus(inside > 0 ? $"{inside}/{total} players ready" : string.Empty);
                return;
            }

            if (!_counting)
            {
                _counting = true;
                _remaining = countdownSeconds;
            }

            _remaining -= Time.deltaTime;
            SetStatus($"Starting in {Mathf.Max(1, Mathf.CeilToInt(_remaining))}");

            if (_remaining > 0f) return;

            _started = true;
            _counting = false;
            SetStatus("Starting!");

            if (startSequence) startSequence.Begin();
            else Debug.LogError($"{nameof(StartZone)} has no {nameof(StartGameSequence)} assigned.", this);
        }

        private int CountSpawnedPlayersInside()
        {
            var count = 0;
            foreach (var player in lobby.Players)
            {
                if (player.IsSpawned && Contains(player.transform.position)) count++;
            }
            return count;
        }

        public bool Contains(Vector3 worldPosition)
        {
            var local = transform.InverseTransformPoint(worldPosition);
            var half = size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        private void SetStatus(string text)
        {
            if (statusLabel && statusLabel.text != text) statusLabel.text = text;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
