namespace Game50033
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    [RequireComponent(typeof(PlayerInputManager))]
    public sealed class PlayerJoinManager : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float spawnSpacing = 2f;

        [Tooltip("Only these buttons let a new device join. Empty = any button on an unpaired device joins.")]
        [SerializeField] private string[] joinBindings =
        {
            "<Keyboard>/e",
            "<Gamepad>/buttonSouth", // A (Xbox) / Cross (PS) / B (Switch Pro) - the bottom face button
        };

        private PlayerInputManager _playerInputManager;

        private void Awake()
        {
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
            _playerInputManager.onPlayerJoined += HandlePlayerJoined;
            _playerInputManager.onPlayerLeft   += HandlePlayerLeft;
        }

        private void OnDisable()
        {
            if (_playerInputManager == null) return;

            _playerInputManager.onPlayerJoined -= HandlePlayerJoined;
            _playerInputManager.onPlayerLeft   -= HandlePlayerLeft;
        }

        private void HandlePlayerJoined(PlayerInput playerInput)
        {
            // arrange players in a 2x2 grid
            var column = playerInput.playerIndex % 2;
            var row = playerInput.playerIndex / 2;
            var gridOffset = new Vector3( (column - 0.5f) * spawnSpacing, 0f, (row - 0.5f) * spawnSpacing);

            // the lobby may have disabled the controller already (player still picking a character),
            // so restore whatever state it was in rather than forcing it on
            var characterController = playerInput.GetComponent<CharacterController>();
            var wasEnabled = characterController.enabled;
            characterController.enabled = false;
            playerInput.transform.position = transform.position + gridOffset;
            characterController.enabled = wasEnabled;

            if (playerInput.TryGetComponent(out PlayerController playerController))
            {
                playerController.ConfigurePlayer(playerInput.playerIndex);
            }

            Debug.Log(
                $"Player {playerInput.playerIndex + 1} joined with " +
                $"{playerInput.currentControlScheme ?? "an unknown control scheme"}.",
                playerInput);
        }

        private static void HandlePlayerLeft(PlayerInput playerInput)
        {
            Debug.Log($"Player {playerInput.playerIndex + 1} left.", playerInput);
        }
    }
}
