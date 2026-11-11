namespace Game50033
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    [DefaultExecutionOrder(-900)]
    [RequireComponent(typeof(PlayerInputManager), typeof(GameSession))]
    public sealed class PlayerJoinManager : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float spawnSpacing = 2f;

        private PlayerInputManager _playerInputManager;
        private GameSession _session;

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
        }

        private void Start()
        {
            // reconcile players enabled before the input manager sent its joining notifications.
            foreach (var playerInput in PlayerInput.all) HandlePlayerJoined(playerInput);
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
        }

        private void HandlePlayerLeft(PlayerInput playerInput)
        {
            if (playerInput.TryGetComponent(out Player player)) _session.UnregisterPlayer(player);
            Debug.Log($"Player {playerInput.playerIndex + 1} left.", playerInput);
        }
    }
}
