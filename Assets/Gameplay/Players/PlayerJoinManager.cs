namespace Game50033
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    [DefaultExecutionOrder(-900)]
    [RequireComponent(typeof(PlayerInputManager), typeof(GameSession))]
    public sealed class PlayerJoinManager : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float spawnSpacing = 2f;

        [Tooltip("Optional prefab overrides by player index: element 0 is Player 1. Empty entries use PlayerInputManager's original player prefab.")]
        [SerializeField] private Player[] playerPrefabsByIndex = System.Array.Empty<Player>();

        [Tooltip("Only these buttons let a new device join. Empty = any button on an unpaired device joins.")]
        [SerializeField] private string[] joinBindings =
        {
            "<Keyboard>/e",
            "<Gamepad>/buttonSouth", // A (Xbox) / Cross (PS) / B (Switch Pro) - the bottom face button
        };

        private PlayerInputManager _playerInputManager;
        private GameSession _session;
        private GameObject _defaultPlayerPrefab;

        private void Awake()
        {
            _playerInputManager = GetComponent<PlayerInputManager>();
            _defaultPlayerPrefab = _playerInputManager.playerPrefab;
            SelectNextPlayerPrefab();

            if (joinBindings == null || joinBindings.Length == 0) return;

            var joinAction = new InputAction("Join", InputActionType.Button);
            foreach (var binding in joinBindings) joinAction.AddBinding(binding);

            // the setters re-arm joining themselves if PlayerInputManager is already enabled
            var manager = GetComponent<PlayerInputManager>();
            manager.joinAction = new InputActionProperty(joinAction);
            manager.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenJoinActionIsTriggered;
        }

        private void OnEnable()
        {
            _playerInputManager = GetComponent<PlayerInputManager>();
            _session = GetComponent<GameSession>();
            _playerInputManager.onPlayerJoined += HandlePlayerJoined;
            _playerInputManager.onPlayerLeft   += HandlePlayerLeft;

            // leaving while this manager is disabled does not send it a callback.
            for (var i = _session.Players.Count - 1; i >= 0; i--)
            {
                var player = _session.Players[i];
                if (player == null || !player.TryGetComponent(out PlayerInput input) || !input.isActiveAndEnabled)
                    _session.UnregisterPlayer(player);
            }

            foreach (var playerInput in PlayerInput.all) HandlePlayerJoined(playerInput);
            SelectNextPlayerPrefab();
        }

        private void Start()
        {
            // reconcile players enabled before the input manager sent its joining notifications.
            foreach (var playerInput in PlayerInput.all) HandlePlayerJoined(playerInput);
            SelectNextPlayerPrefab();
        }

        private void OnDisable()
        {
            if (_playerInputManager == null) return;

            _playerInputManager.onPlayerJoined -= HandlePlayerJoined;
            _playerInputManager.onPlayerLeft   -= HandlePlayerLeft;
        }

        private void HandlePlayerJoined(PlayerInput playerInput)
        {
            if (!playerInput.TryGetComponent(out Player player))
            {
                Debug.LogError("Joined player is missing its Player component.", playerInput);
                return;
            }

            player.Initialize();
            if (player.Session == _session) return;

            // arrange players in a 2x2 grid
            var column = playerInput.playerIndex % 2;
            var row = playerInput.playerIndex / 2;
            var gridOffset = new Vector3( (column - 0.5f) * spawnSpacing, 0f, (row - 0.5f) * spawnSpacing);

            // the lobby may have disabled the controller already (player still picking a character),
            // so restore whatever state it was in rather than forcing it on
            var characterController = playerInput.GetComponent<CharacterController>();
            var controllerWasEnabled = characterController.enabled;
            characterController.enabled = false;
            playerInput.transform.position = transform.position + gridOffset;
            playerInput.transform.SetParent(transform, true);
            characterController.enabled = controllerWasEnabled;

            player.ConfigurePlayer(playerInput.playerIndex);
            _session.RegisterPlayer(player);

            Debug.Log(
                $"Player {playerInput.playerIndex + 1} joined with " +
                $"{playerInput.currentControlScheme ?? "an unknown control scheme"}.",
                playerInput);
            SelectNextPlayerPrefab();
        }

        private void HandlePlayerLeft(PlayerInput playerInput)
        {
            if (playerInput.TryGetComponent(out Player player)) _session.UnregisterPlayer(player);
            SelectNextPlayerPrefab();
            Debug.Log($"Player {playerInput.playerIndex + 1} left.", playerInput);
        }

        internal PlayerInput JoinPlayerWithPrefab(
            Player prefab,
            int playerIndex,
            int splitScreenIndex,
            string controlScheme,
            InputDevice[] devices)
        {
            var previousPrefab = _playerInputManager.playerPrefab;

            try
            {
                _playerInputManager.playerPrefab = prefab.gameObject;
                return _playerInputManager.JoinPlayer(playerIndex, splitScreenIndex, controlScheme, devices);
            }
            finally
            {
                // Restore the usual prefab after spawning the selected character.
                _playerInputManager.playerPrefab = previousPrefab;
                SelectNextPlayerPrefab();
            }
        }

        private void SelectNextPlayerPrefab()
        {
            if (playerPrefabsByIndex == null || playerPrefabsByIndex.Length == 0)
            {
                return;
            }

            var nextIndex = 0;
            while (PlayerInput.GetPlayerByIndex(nextIndex) != null)
            {
                nextIndex++;
            }

            var prefab = nextIndex < playerPrefabsByIndex.Length
                ? playerPrefabsByIndex[nextIndex]
                : null;
            _playerInputManager.playerPrefab = prefab != null ? prefab.gameObject : _defaultPlayerPrefab;
        }
    }
}
