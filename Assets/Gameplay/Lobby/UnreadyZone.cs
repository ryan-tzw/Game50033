namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// A ready (spawned) player who stands inside this ring for a short countdown goes back to
    /// character select, freeing their character. Leaving the ring cancels the countdown. Players re-spawn
    /// at their own lobby spawn point (LobbyManager), never inside the ring.
    /// </summary>
    public sealed class UnreadyZone : MonoBehaviour
    {
        private const int RingSegments = 64;

        [SerializeField] private LobbyManager lobby;
        [SerializeField] private LobbyTuning tuning;
        [Tooltip("Radius of the zone on the floor, centred on this transform.")]
        [SerializeField, Min(0.1f)] private float radius = 0.75f;
        [SerializeField] private LineRenderer ring;
        [Tooltip("Disabled screen-space Text cloned once per player for the over-head countdown.")]
        [SerializeField] private Text countdownTemplate;

        private readonly Dictionary<PlayerCharacterSelect, float> _elapsed = new();
        private readonly Dictionary<PlayerCharacterSelect, Text> _labels = new();
        private readonly List<PlayerCharacterSelect> _scratch = new();
        private Camera _camera;

        private void Awake()
        {
            BuildRing();
            if (countdownTemplate) countdownTemplate.gameObject.SetActive(false);
        }

        private void OnValidate() => BuildRing();

        private void Update()
        {
            if (lobby == null || tuning == null) return;
            var settings = tuning.unready;

            _scratch.Clear();
            _scratch.AddRange(lobby.Players);
            foreach (var player in _scratch)
            {
                if (player == null || !player.IsSpawned || !Contains(player.transform.position))
                {
                    StopCountdown(player);
                    continue;
                }

                _elapsed.TryGetValue(player, out var elapsed);
                elapsed += Time.deltaTime;
                _elapsed[player] = elapsed;
                var remaining = settings.countdownSeconds - elapsed;

                if (remaining <= 0f)
                {
                    StopCountdown(player);
                    player.ReturnToSelection();
                    continue;
                }

                ShowLabel(player, Mathf.CeilToInt(remaining).ToString(), settings);
            }

            // drop state for players who left the lobby or were replaced by a new character prefab.
            _scratch.Clear();
            foreach (var player in _labels.Keys)
                if (player == null || !IsInLobby(player)) _scratch.Add(player);
            foreach (var player in _scratch) StopCountdown(player);
        }

        private bool IsInLobby(PlayerCharacterSelect player)
        {
            foreach (var joined in lobby.Players)
                if (joined == player) return true;
            return false;
        }

        public bool Contains(Vector3 worldPosition)
        {
            var offset = worldPosition - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }

        private void ShowLabel(PlayerCharacterSelect player, string text, LobbyTuning.UnreadySettings settings)
        {
            if (!countdownTemplate) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            if (!_labels.TryGetValue(player, out var label) || label == null)
            {
                label = Instantiate(countdownTemplate, countdownTemplate.transform.parent);
                label.name = $"Unready countdown P{player.PlayerIndex + 1}";
                _labels[player] = label;
            }

            label.gameObject.SetActive(true);
            label.color = settings.labelColor;
            if (label.text != text) label.text = text;
            label.transform.position = _camera.WorldToScreenPoint(player.transform.position + Vector3.up * settings.labelHeight);
        }

        private void StopCountdown(PlayerCharacterSelect player)
        {
            if (player is null) return;
            _elapsed.Remove(player);
            if (_labels.TryGetValue(player, out var label))
            {
                if (label != null) Destroy(label.gameObject);
                _labels.Remove(player);
            }
        }

        private void BuildRing()
        {
            if (ring == null) return;
            // skip when already built at this radius; rewriting on every reload would dirty the scene.
            if (ring.positionCount == RingSegments && !ring.useWorldSpace && ring.loop
                && Mathf.Abs(ring.GetPosition(0).x - radius) < 0.0001f) return;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = RingSegments;
            for (var i = 0; i < RingSegments; i++)
            {
                var angle = i * Mathf.PI * 2f / RingSegments;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.02f, Mathf.Sin(angle) * radius));
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.8f);
            const int segments = 40;
            for (var i = 0; i < segments; i++)
            {
                var a = Quaternion.AngleAxis(360f * i / segments, Vector3.up) * Vector3.forward * radius;
                var b = Quaternion.AngleAxis(360f * (i + 1) / segments, Vector3.up) * Vector3.forward * radius;
                Gizmos.DrawLine(transform.position + a, transform.position + b);
            }
        }
    }
}
