namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LockOnIndicator : MonoBehaviour
    {
        [SerializeField] private Behaviour target;
        [SerializeField] private Transform groundAnchor;
        [SerializeField] private GameSession session;
        [SerializeField] private LockOnIndicatorTuning tuning;

        private readonly List<Player> _observed = new(4);
        private readonly Player[] _players = new Player[4];
        private readonly float[] _acquiredAt = new float[4];
        private readonly Vector4[] _colors = new Vector4[4];
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _properties;
        private GameSession _boundSession;

        private static readonly int ColorsId = Shader.PropertyToID("_PlayerColors");
        private static readonly int ScalesId = Shader.PropertyToID("_PlayerScales");
        private static readonly int FadesId = Shader.PropertyToID("_PlayerFades");
        private static readonly int CountId = Shader.PropertyToID("_PlayerCount");
        private static readonly int ArcsId = Shader.PropertyToID("_ArcsPerPlayer");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        private static readonly int RotationId = Shader.PropertyToID("_Rotation");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        private void OnEnable()
        {
            _renderer = GetComponent<MeshRenderer>();
            _properties ??= new MaterialPropertyBlock();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
            if (Application.isPlaying && session != null && session.isActiveAndEnabled) BindSession();
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            // encounter visuals can resolve the already-persistent roster once; no per-frame lookup or singleton.
            if (session == null || !session.isActiveAndEnabled) session = FindFirstObjectByType<GameSession>();
            if (session != null && _boundSession != session) BindSession();
            if (session == null) Debug.LogError("LockOnIndicator requires a GameSession reference in the encounter.", this);
        }

        private void BindSession()
        {
            if (_boundSession == session) return;
            _boundSession = session;
            session.PlayerRegistered += Observe;
            session.PlayerUnregistered += Unobserve;
            foreach (var player in session.Players) Observe(player);
        }

        private void Observe(Player player)
        {
            if (player == null || _observed.Contains(player)) return;
            _observed.Add(player);
            player.AutoTargetChanged += OnTargetChanged;
            SetPlayer(player, ReferenceEquals(player.AutoTarget, target));
        }

        private void Unobserve(Player player)
        {
            if (player != null) player.AutoTargetChanged -= OnTargetChanged;
            _observed.Remove(player);
            SetPlayer(player, false);
        }

        private void OnTargetChanged(Player player, Behaviour selectedTarget)
        {
            SetPlayer(player, ReferenceEquals(selectedTarget, target));
        }

        private void SetPlayer(Player player, bool selected)
        {
            if (ReferenceEquals(player, null)) return;
            var slot = player.PlayerIndex;
            if (slot < 0 || slot >= _players.Length) return;
            if (selected)
            {
                if (ReferenceEquals(_players[slot], player)) return;
                _players[slot] = player;
                _acquiredAt[slot] = Time.time;
            }
            else if (ReferenceEquals(_players[slot], player)) _players[slot] = null;
        }

        private void LateUpdate()
        {
            if (_renderer == null || tuning == null || Application.isPlaying && !AttackTargeting.IsValidTarget(target))
            {
                if (_renderer != null) _renderer.enabled = false;
                return;
            }

            var count = 0;
            var scales = Vector4.one;
            var fades = Vector4.zero;
            for (var slot = 0; slot < 4; slot++)
            {
                var player = _players[slot];
                var preview = !Application.isPlaying && (tuning.editorPreviewPlayers & (1 << slot)) != 0;
                if (!preview && (player == null || !ReferenceEquals(player.AutoTarget, target))) continue;

                var progress = preview || tuning.acquireDuration <= 0f ? 1f
                    : Mathf.Clamp01((Time.time - _acquiredAt[slot]) / tuning.acquireDuration);
                var eased = progress * progress * (3f - 2f * progress);
                scales[count] = Mathf.Lerp(Mathf.Max(1f, tuning.acquireScale), 1f, eased);
                fades[count] = eased;
                var overrideColor = tuning.playerColors != null && slot < tuning.playerColors.Length
                    ? tuning.playerColors[slot] : Color.white;
                _colors[count] = preview || tuning.overridePlayerColors ? overrideColor : player.PlayerColor;
                count++;
            }
            _renderer.enabled = count > 0;
            if (count == 0) return;

            // compact active slots in identity order: all arcs are evenly spaced for every player count.
            // the acquisition clock belongs to the identity slot, so redistribution never restarts existing fades.
            var radius = Mathf.Max(0.01f, tuning.radius);
            var thickness = Mathf.Max(0.001f, tuning.thickness);
            var extent = (radius + thickness) * Mathf.Max(1f, tuning.acquireScale);
            var position = groundAnchor != null ? groundAnchor.position : target != null ? target.transform.position
                : new Vector3(transform.position.x, 0f, transform.position.z);
            transform.SetPositionAndRotation(position + Vector3.up * tuning.groundOffset, Quaternion.Euler(90f, 0f, 0f));
            transform.localScale = Vector3.one * (extent * 2f);
            _properties.SetVectorArray(ColorsId, _colors);
            _properties.SetVector(ScalesId, scales);
            _properties.SetVector(FadesId, fades);
            _properties.SetFloat(CountId, count);
            _properties.SetFloat(ArcsId, Mathf.Clamp(tuning.arcsPerPlayer, 1, 8));
            _properties.SetFloat(RadiusId, radius / extent);
            _properties.SetFloat(ThicknessId, thickness / extent);
            _properties.SetFloat(CoverageId, Mathf.Clamp(tuning.coverage, 0.05f, 0.95f));
            _properties.SetFloat(RotationId, tuning.rotationDegrees * Mathf.Deg2Rad);
            _properties.SetFloat(OpacityId, Mathf.Clamp01(tuning.opacity));
            _renderer.SetPropertyBlock(_properties);
        }

        private void OnDisable()
        {
            if (_boundSession != null)
            {
                _boundSession.PlayerRegistered -= Observe;
                _boundSession.PlayerUnregistered -= Unobserve;
            }
            foreach (var player in _observed)
                if (player != null) player.AutoTargetChanged -= OnTargetChanged;
            _observed.Clear();
            for (var i = 0; i < 4; i++) _players[i] = null;
            _boundSession = null;
            if (_renderer != null) _renderer.enabled = false;
        }
    }
}
