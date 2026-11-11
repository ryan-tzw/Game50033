namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    /// PLACEHOLDER so the lobby dummy can be hit before the real character abilities exist.
    /// A single melee swing on the Primary action that hits every <see cref="IHittable"/>
    /// in a small sphere in front of the player.
    ///
    /// Uses the shared PlayerAbilities slot so downed, dodge and input restrictions apply.
    /// Replace this component with per-character abilities when those are implemented.
    /// </summary>
    [RequireComponent(typeof(PlayerInput), typeof(Player))]
    public sealed class PlayerCombat : PlayerAbilities
    {
        [SerializeField, Min(0f)] private float attackReach = 0.3f;
        [SerializeField, Min(0.01f)] private float attackRadius = 0.35f;
        [SerializeField, Min(0f)] private float attackHeight = 0.2f;
        [SerializeField, Min(0f)] private float cooldown = 0.3f;
        [SerializeField, Min(0f)] private float damage = 1f;
        [SerializeField] private LayerMask hitMask = ~0;

        private static readonly Collider[] HitBuffer = new Collider[16];
        private readonly HashSet<IHittable> _hitThisSwing = new();

        private InputAction _attackAction;
        private float _nextAttackTime;
        private int _enabledFrame;

        private void Awake()
        {
            _attackAction = GetComponent<PlayerInput>().actions.FindAction("Player/Primary", true);
        }

        private void OnEnable()
        {
            // the button press that confirmed the character shouldn't also swing
            _enabledFrame = Time.frameCount;
        }

        public override bool TryUsePrimary()
        {
            var actions = GetComponent<PlayerInput>().actions;
            if (_attackAction == null || _attackAction.actionMap.asset != actions)
                _attackAction = actions.FindAction("Player/Primary", true);
            if (Time.frameCount == _enabledFrame) return false;
            if (!_attackAction.WasPressedThisFrame() || Time.time < _nextAttackTime) return false;

            _nextAttackTime = Time.time + cooldown;
            Attack();
            return true;
        }

        public override bool TryUseSecondary() => false;
        public override bool TryUseDefensive() => false;
        public override bool TryUseSpecial() => false;
        public override void Cancel() { }

        private Vector3 AttackCenter =>
            transform.position + Vector3.up * attackHeight
            + Player.AimDirection * attackReach;

        private void Attack()
        {
            var facing = Player.AimDirection;
            var count = Physics.OverlapSphereNonAlloc(AttackCenter, attackRadius, HitBuffer, hitMask, QueryTriggerInteraction.Ignore);

            _hitThisSwing.Clear();
            for (var i = 0; i < count; i++)
            {
                var hitCollider = HitBuffer[i];
                if (hitCollider.transform.IsChildOf(transform)) continue;

                var hittable = hitCollider.GetComponentInParent<IHittable>();
                if (hittable == null || !_hitThisSwing.Add(hittable)) continue;

                hittable.TakeHit(new HitInfo(gameObject, damage, facing));
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(AttackCenter, attackRadius);
        }
    }
}
