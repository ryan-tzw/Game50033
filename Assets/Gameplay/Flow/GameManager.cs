namespace Game50033
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.SceneManagement;

    public enum GameState
    {
        Lobby,
        Cutscene,
        Encounter,
        Victory,
        Defeat,
    }

    /// <summary>
    /// Scene-local owner of the game's state (lobby, cutscene, encounter, victory, defeat) and of moving
    /// between scenes in the order set by <see cref="GameFlow"/>. Put one in every scene of the flow.
    /// It resets players at encounter boundaries. After a defeat it freezes players and waits for every
    /// player to press Special (E / bottom face button); after a victory it stands everyone up and lets
    /// them move, and the scene's VictoryReadyZone calls <see cref="Continue"/> once everyone is in it.
    /// What happens inside a fight belongs to the scene's EncounterManager, which reports the outcome here.
    ///
    /// Holds nothing that must survive a scene change: where the run is comes from the active scene.
    /// Players and device pairing live on the persistent GameSession root, not here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private GameFlow flow;

        private readonly List<Player> _players = new();
        private readonly HashSet<Player> _ready = new();
        private float _readyOpensAt;
        private bool _loading;

        public GameState State { get; private set; }
        public GameFlow Flow => flow;

        /// <summary>Old state, new state.</summary>
        public event Action<GameState, GameState> StateChanged;

        /// <summary>Raised after every player was reset for a fresh fight, just before the state becomes Encounter.</summary>
        public event Action EncounterReset;

        /// <summary>Raised when a player readies up on the defeat screen.</summary>
        public event Action ReadyChanged;

        public bool IsShowingResults => State is GameState.Victory or GameState.Defeat;
        public int ReadyCount => _ready.Count;
        public int PlayerCount
        {
            get
            {
                // counted without the shared buffer, so it is safe to call from ReadyChanged.
                var count = 0;
                foreach (var playerInput in PlayerInput.all)
                    if (playerInput.TryGetComponent(out Player _)) count++;
                return count;
            }
        }
        public bool IsReady(Player player) => _ready.Contains(player);

        private void Awake()
        {
            if (flow == null)
            {
                Debug.LogError($"{nameof(GameManager)} requires a {nameof(GameFlow)} asset.", this);
                enabled = false;
                return;
            }

            State = flow.IsLobby(SceneManager.GetActiveScene().name) ? GameState.Lobby : GameState.Cutscene;
        }

        private void Start()
        {
            if (State != GameState.Lobby) return;

            // back in the lobby: everyone stands up fresh and new players may join again.
            ResetPlayers();
            if (PlayerInputManager.instance != null) PlayerInputManager.instance.EnableJoining();
        }

        private void Update()
        {
            if (State != GameState.Defeat || _loading || Time.time < _readyOpensAt) return;

            var players = CollectPlayers();
            foreach (var player in players)
            {
                if (!player.SpecialPressedThisFrame || !_ready.Add(player)) continue;
                ReadyChanged?.Invoke();
            }

            if (players.Count > 0 && _ready.Count >= players.Count) Continue();
        }

        /// <summary>Lobby: start a run from the first run scene.</summary>
        public void StartRun()
        {
            if (State != GameState.Lobby) return;
            LoadScene(flow.FirstRunScene);
        }

        /// <summary>Cutscene: go on to the next scene of the run.</summary>
        public void LoadNext() => LoadScene(flow.NextAfter(SceneManager.GetActiveScene().name));

        public void ReturnToLobby() => LoadScene(flow.lobbyScene);

        /// <summary>Encounter scene start: reset every player and begin the fight.</summary>
        public void BeginEncounter()
        {
            if (State is GameState.Encounter || _loading) return;

            ResetPlayers();
            EncounterReset?.Invoke();
            SetState(GameState.Encounter);
        }

        /// <summary>Called by the EncounterManager when the fight is decided.</summary>
        public void EndEncounter(bool victory)
        {
            if (State != GameState.Encounter) return;

            if (victory)
            {
                // everyone stands up and keeps moving, so they can walk to the ready circle.
                ResetPlayers();
            }
            else
            {
                // freeze players in place; input stays active so they can ready up.
                foreach (var player in CollectPlayers()) player.enabled = false;
            }

            _ready.Clear();
            _readyOpensAt = Time.time + flow.results.defeatReadyDelay;
            SetState(victory ? GameState.Victory : GameState.Defeat);
        }

        /// <summary>Results: victory goes on with the run (the lobby after the last scene); defeat goes to the lobby.</summary>
        public void Continue()
        {
            if (State == GameState.Victory) LoadNext();
            else if (State == GameState.Defeat) ReturnToLobby();
        }

        private void LoadScene(string sceneName)
        {
            if (_loading || string.IsNullOrEmpty(sceneName)) return;
            _loading = true;

            if (PlayerInputManager.instance != null) PlayerInputManager.instance.DisableJoining();

            // copy: PlayerInput.all can change while we iterate. Players stay frozen until the next
            // scene's EncounterManager (or LobbyManager) gives them control again.
            foreach (var playerInput in new List<PlayerInput>(PlayerInput.all))
            {
                playerInput.DeactivateInput();
                if (playerInput.TryGetComponent(out Player player)) player.enabled = false;
                DontDestroyOnLoad(playerInput.transform.root.gameObject);
            }

            SceneManager.LoadScene(sceneName);
        }

        private void ResetPlayers()
        {
            foreach (var player in CollectPlayers()) player.ResetForEncounter();
        }

        private List<Player> CollectPlayers()
        {
            _players.Clear();
            foreach (var playerInput in PlayerInput.all)
            {
                if (playerInput.TryGetComponent(out Player player)) _players.Add(player);
            }
            return _players;
        }

        private void SetState(GameState next)
        {
            if (State == next) return;
            var previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
        }
    }
}
