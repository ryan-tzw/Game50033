namespace Game50033
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    [RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
    public sealed class PlayerController : MonoBehaviour
    {
        private static readonly int PlayerTintId = Shader.PropertyToID("_PlayerTint");

        private static readonly Color[] PlayerColors =
        {
            Color.HSVToRGB(0.6f, 0.6f, 0.9f),
            Color.HSVToRGB(0.0f, 0.6f, 0.9f),
            Color.HSVToRGB(0.1f, 0.6f, 0.9f),
            Color.HSVToRGB(0.3f, 0.6f, 0.9f),
        };

        [SerializeField, Min(0f)] private float moveSpeed = 5f;

        private CharacterController _characterController;
        private InputAction _moveAction;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            var playerInput = GetComponent<PlayerInput>();
            _moveAction = playerInput.actions.FindAction("Player/Move", true);
        }

        private void Update()
        {
            var input = Vector2.ClampMagnitude(_moveAction.ReadValue<Vector2>(), 1f);
            var velocity = new Vector3(input.x, 0f, input.y) * moveSpeed;
            _characterController.SimpleMove(velocity);
        }

        public void ConfigurePlayer(int playerIndex)
        {
            gameObject.name = $"Player {playerIndex + 1}";
            var playerColor = PlayerColors[playerIndex % PlayerColors.Length];
            var materialProperties = new MaterialPropertyBlock();

            foreach (var childRenderer in GetComponentsInChildren<Renderer>(true))
            {
                childRenderer.GetPropertyBlock(materialProperties);
                materialProperties.SetColor(PlayerTintId, playerColor);
                childRenderer.SetPropertyBlock(materialProperties);
                materialProperties.Clear();
            }
        }
    }
}
