namespace Game50033
{
    using UnityEngine;

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class PlayerAnimation : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController controller;
        [SerializeField] private PlayerHitstop hitstop;
        [SerializeField] private PlayerAnimationTuning tuning;

        private static readonly int MovingId = Animator.StringToHash("Moving");
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int AttackPhaseId = Animator.StringToHash("AttackPhase");

        private PlayerMovement _movement;
        private PlayerAbilities _abilities;
        private SpriteRenderer _sprite;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _abilities = GetComponent<PlayerAbilities>();
            _sprite = animator.GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            animator.SetInteger(AttackPhaseId, (int)(_abilities != null && _abilities.isActiveAndEnabled
                ? _abilities.AnimationPhase : PlayerAttackPhase.None));

            // sample after movement and abilities; hold the current state and pose during hitstop.
            animator.speed = hitstop.IsStopped ? 0f : 1f;
            if (hitstop.IsStopped) return;

            var horizontalInput = _movement.MoveDirection.x;
            if (Mathf.Abs(horizontalInput) > 0.01f) _sprite.flipX = horizontalInput < 0f;

            var velocity = controller.enabled ? controller.velocity : Vector3.zero;
            velocity.y = 0f;
            var speed = velocity.magnitude;
            animator.SetBool(MovingId, speed > tuning.idleSpeed);
            animator.SetFloat(SpeedId, speed / Mathf.Max(0.01f, tuning.runSpeed));
        }

        private void OnDisable()
        {
            if (animator != null) animator.speed = 1f;
        }
    }
}
