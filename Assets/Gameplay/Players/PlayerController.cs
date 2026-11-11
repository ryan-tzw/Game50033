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

        /// <summary>Last non-zero movement direction on the XZ plane (starts facing the camera).</summary>
        public Vector3 Facing { get; private set; } = Vector3.back;

        /// <summary>The character this player picked in the lobby, if any.</summary>
        public CharacterDefinition Character { get; private set; }

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
            if (input.sqrMagnitude > 0.01f) Facing = new Vector3(input.x, 0f, input.y).normalized;
            _characterController.SimpleMove(velocity);
        }

        public void ConfigurePlayer(int playerIndex)
        {
            gameObject.name = $"Player {playerIndex + 1}";
            SetTint(PlayerColors[playerIndex % PlayerColors.Length]);
        }

        public void ApplyCharacter(CharacterDefinition character)
        {
            Character = character;
            if (character == null) return;

            if (character.Sprite != null)
            {
                foreach (var spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
                {
                    spriteRenderer.sprite = character.Sprite;
                }
            }

            SetTint(character.Tint);
        }

        private void SetTint(Color color)
        {
            var materialProperties = new MaterialPropertyBlock();

            foreach (var childRenderer in GetComponentsInChildren<Renderer>(true))
            {
                childRenderer.GetPropertyBlock(materialProperties);
                materialProperties.SetColor(PlayerTintId, color);
                childRenderer.SetPropertyBlock(materialProperties);
                materialProperties.Clear();
            }
        }
    }
}
