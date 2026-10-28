namespace Game50033
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    [RequireComponent(typeof(PlayerInputManager))]
    public sealed class PlayerJoinManager : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float spawnSpacing = 2f;

        private PlayerInputManager _playerInputManager;

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

            var characterController = playerInput.GetComponent<CharacterController>();
            characterController.enabled = false;
            playerInput.transform.position = transform.position + gridOffset;
            characterController.enabled = true;

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
