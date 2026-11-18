namespace Game50033
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] private DebugMenu debugMenuPrefab;
        private DebugState _debug;
        public DebugState Debug => _debug ??= new DebugState(Players);

        private readonly List<Player> _players = new List<Player>();
        private IReadOnlyList<Player> _playersView;
        private LivingPlayerView _livingPlayers;
        private bool _initialized;

        public IReadOnlyList<Player> Players => _playersView ??= _players.AsReadOnly();
        public IReadOnlyList<Player> LivingPlayers => _livingPlayers ??= new LivingPlayerView(_players);

        public event Action<Player> PlayerRegistered;
        public event Action<Player> PlayerUnregistered;

        private void Awake()
        {
            // scene reloads may bring in another session prefab; keep the already-running session.
            foreach (var session in FindObjectsByType<GameSession>(FindObjectsSortMode.None))
            {
                if (session == this || !session._initialized) continue;

                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            _initialized = true;
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            if (debugMenuPrefab != null) Instantiate(debugMenuPrefab, transform).Initialize(this);
        }

        public void RegisterPlayer(Player player)
        {
            if (player == null || _players.Contains(player)) return;

            if (player.Session != null) player.Session.UnregisterPlayer(player);
            _players.Add(player);
            player.Session = this;
            if (Debug.NoCooldowns && player.Abilities != null) player.Abilities.ClearCooldowns();
            PlayerRegistered?.Invoke(player);
        }

        public void UnregisterPlayer(Player player)
        {
            if (!_players.Remove(player)) return;

            if (player != null && player.Session == this) player.Session = null;
            PlayerUnregistered?.Invoke(player);
        }

        // evaluate eligibility at read time so a death or revive is visible even between update loops.
        private sealed class LivingPlayerView : IReadOnlyList<Player>
        {
            private readonly IReadOnlyList<Player> _players;

            public LivingPlayerView(IReadOnlyList<Player> players) => _players = players;

            public int Count
            {
                get
                {
                    var count = 0;
                    for (var i = 0; i < _players.Count; i++)
                        if (IsEligible(_players[i])) count++;
                    return count;
                }
            }

            public Player this[int index]
            {
                get
                {
                    for (var i = 0; i < _players.Count; i++)
                    {
                        var player = _players[i];
                        if (IsEligible(player) && index-- == 0) return player;
                    }
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
            }

            public IEnumerator<Player> GetEnumerator()
            {
                for (var i = 0; i < _players.Count; i++)
                {
                    var player = _players[i];
                    if (IsEligible(player)) yield return player;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            private static bool IsEligible(Player player) =>
                player != null && player.isActiveAndEnabled && player.Health != null && !player.Health.IsDowned;
        }

        private void OnDestroy()
        {
            foreach (var player in _players)
            {
                if (player != null && player.Session == this) player.Session = null;
            }

            _players.Clear();
        }
    }
}
