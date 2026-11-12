namespace Game50033
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class GameSession : MonoBehaviour
    {
        private readonly List<Player> _players = new List<Player>();
        private IReadOnlyList<Player> _playersView;
        private bool _initialized;

        public IReadOnlyList<Player> Players => _playersView ??= _players.AsReadOnly();
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
        }

        public void RegisterPlayer(Player player)
        {
            if (player == null || _players.Contains(player)) return;

            if (player.Session != null) player.Session.UnregisterPlayer(player);
            _players.Add(player);
            player.Session = this;
            PlayerRegistered?.Invoke(player);
        }

        public void UnregisterPlayer(Player player)
        {
            if (!_players.Remove(player)) return;

            if (player != null && player.Session == this) player.Session = null;
            PlayerUnregistered?.Invoke(player);
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
