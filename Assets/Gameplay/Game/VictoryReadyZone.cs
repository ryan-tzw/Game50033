namespace Game50033
{
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// After a victory, a lit circle on the arena floor. When every player stands inside it, a countdown
    /// runs and then the GameManager continues the run; anyone leaving cancels it. Hidden until victory.
    /// It only reads player positions and tells the GameManager when to go on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VictoryReadyZone : MonoBehaviour
    {
        private const int RingSegments = 64;

        [SerializeField] private GameManager gameManager;
        [Tooltip("Radius of the circle on the floor, centred on this transform.")]
        [SerializeField, Min(0.1f)] private float radius = 1.25f;
        [Tooltip("Floor ring drawn around the circle; hidden until victory.")]
        [SerializeField] private LineRenderer ring;
        [Tooltip("Screen-space label kept above the circle: how many are in, then the countdown.")]
        [SerializeField] private Text label;
        [Tooltip("World height above the circle's centre where the label sits.")]
        [SerializeField] private float labelHeight = 1.6f;
        [Tooltip("Ring width multiplier while the countdown runs, so the circle reads as lit. The colour comes from the ring's material.")]
        [SerializeField, Min(1f)] private float countingWidthScale = 1.8f;

        private Camera _camera;
        private float _ringWidth = -1f;
        private float _remaining;
        private bool _counting;
        private bool _active;
        private bool _done;

        private void Awake()
        {
            BuildRing();
            Show(false);
        }

        private void OnValidate() => BuildRing();

        private void OnEnable()
        {
            if (gameManager != null) gameManager.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState previous, GameState next)
        {
            _done = false;
            _counting = false;
            Show(next == GameState.Victory);
        }

        private void Update()
        {
            if (!_active || _done) return;

            var players = gameManager.Players;
            var total = 0;
            var inside = 0;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null) continue;
                total++;
                if (Contains(player.transform.position)) inside++;
            }

            if (total == 0 || inside < total)
            {
                _counting = false;
                SetRingLit(false);
                SetLabel($"Stand here to continue  ({inside}/{total})");
                return;
            }

            if (!_counting)
            {
                _counting = true;
                _remaining = gameManager.Flow.results.victoryCountdownSeconds;
            }

            _remaining -= Time.deltaTime;
            SetRingLit(true);
            var lobbyNext = gameManager.Flow.IsLobby(gameManager.Flow.NextAfter(gameObject.scene.name));
            SetLabel($"{(lobbyNext ? "Back to the lobby" : "Next stage")} in {Mathf.Max(1, Mathf.CeilToInt(_remaining))}");
            if (_remaining > 0f) return;

            _done = true;
            gameManager.Continue();
        }

        private void LateUpdate()
        {
            if (!_active || label == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            label.transform.position = _camera.WorldToScreenPoint(transform.position + Vector3.up * labelHeight);
        }

        public bool Contains(Vector3 worldPosition)
        {
            var offset = worldPosition - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }

        private void Show(bool visible)
        {
            _active = visible;
            if (ring) ring.enabled = visible;
            if (label) label.gameObject.SetActive(visible);
            if (visible) SetRingLit(false);
        }

        private void SetRingLit(bool lit)
        {
            if (!ring) return;
            if (_ringWidth < 0f) _ringWidth = ring.widthMultiplier;
            ring.widthMultiplier = lit ? _ringWidth * countingWidthScale : _ringWidth;
        }

        private void SetLabel(string text)
        {
            if (label && label.text != text) label.text = text;
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
            Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.8f);
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
