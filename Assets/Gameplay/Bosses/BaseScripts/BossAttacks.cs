namespace Game50033
{
    using UnityEngine;
    using System.Collections.Generic;
    using System;
    using Unity.VisualScripting;

    public class BossAttacks : MonoBehaviour
    {
        private bool _initialized;
        private BaseBoss _boss;
        private BossAttackStack _stack;

        [Header("Phase 1")]
        [SerializeField] private AttackSet phase1;

        [Header("Phase 2")]
        [SerializeField] private AttackSet phase2;

        [Header("Phase 3")]
        [SerializeField] private AttackSet phase3;

        public bool IsAttacking { get; private set; }
        public event Action StartedAttack;

        // [SerializeField] private float attackDuration = 1f;
        [SerializeField] private float BossAttackInterval = 3f;
        protected float bossAttackIntervalTimer;
        public int CurrentPhase = 1;

        private float projectileSpeedMultiplier = 1f;

        private BaseBossAttack _controlledAttack;
        private float _controlDeadline;
        private float _controlRecoveryTime;
        private float _recoveryDeadline;
        private bool _recoveringFromControl;
        private readonly HashSet<GameObject> _triedAttacks = new();

        public int ConfiguredPhaseCount => phase3 != null ? 3 : phase2 != null ? 2 : phase1 != null ? 1 : 0;


        internal void Initialize()
        {
            if (_initialized) return;
            _boss = GetComponent<BaseBoss>();
            _stack = GetComponent<BossAttackStack>();
            StartedAttack += () => { IsAttacking = true; };
            _stack.RegisterAttacks(GetPhaseAttacks());
            _initialized = true;
        }

        protected void Update()
        {
            if (bossAttackIntervalTimer > 0f) bossAttackIntervalTimer -= Time.deltaTime;

            if (ReferenceEquals(_controlledAttack, null)) return;

            if (!_recoveringFromControl && (_controlledAttack == null || Time.time >= _controlDeadline))
            {
                // If Charge never finishes, stop it and let the boss recover.
                BeginControlRecovery();
                if (_controlledAttack != null) _controlledAttack.Cancel();
            }

            if (_recoveringFromControl && Time.time >= _recoveryDeadline)
            {
                ClearControlledAttack();
                EndAttack();
            }
        }

        private void OnDisable()
        {
            if (ReferenceEquals(_controlledAttack, null)) return;
            ClearControlledAttack();
            IsAttacking = false;
            if (_boss != null && _boss.Movement != null) _boss.Movement.StopMovement();
        }

        public void TryAttacking()
        {
            if (!isActiveAndEnabled || !_boss.isActiveAndEnabled || _boss.Health.IsDead
                || IsAttacking || bossAttackIntervalTimer > 0f) return;
            _triedAttacks.Clear();
            int attempts = _stack.SelectionAttemptLimit;
            while (attempts-- > 0)
            {
                GameObject nextAttack = _stack.GetNext();
                if (nextAttack == null) return;
                if (!_triedAttacks.Add(nextAttack)) continue;

                GameObject currentAttack = Instantiate(nextAttack, transform.position, Quaternion.identity);
                var attack = currentAttack.GetComponent<BaseBossAttack>();
                if (attack is TargetedBossAttack targeted)
                    targeted.SetProjectileSpeedMultiplier(projectileSpeedMultiplier);
                attack.Setup(_boss);
                if (!attack.CanStart)
                {
                    Destroy(currentAttack);
                    continue;
                }

                // Only announce an attack after it has found a usable target and path.
                StartedAttack?.Invoke();
                if (attack.ControlsBoss)
                {
                    _controlledAttack = attack;
                    _controlRecoveryTime = Mathf.Max(0f, attack.recoveryTime);
                    _recoveringFromControl = false;
                    _controlDeadline = Time.time + attack.BossControlTimeout;
                    attack.BossControlReleased += HandleBossControlReleased;
                    bossAttackIntervalTimer = BossAttackInterval;
                    attack.Attack();
                    return;
                }

                attack.Attack();
                bossAttackIntervalTimer = BossAttackInterval;
                float recovery = attack.windUpTime + attack.recoveryTime;
                Invoke(nameof(EndAttack), recovery);
                Destroy(currentAttack, recovery);
                return;
            }
        }

        private void HandleBossControlReleased(BaseBossAttack attack)
        {
            if (attack != _controlledAttack || _recoveringFromControl) return;
            BeginControlRecovery();
        }

        private void BeginControlRecovery()
        {
            _recoveringFromControl = true;
            _recoveryDeadline = Time.time + _controlRecoveryTime;
        }

        private void ClearControlledAttack()
        {
            var attack = _controlledAttack;
            _controlledAttack = null;
            _recoveringFromControl = false;
            if (attack == null) return;
            attack.BossControlReleased -= HandleBossControlReleased;
            attack.Cancel();
            Destroy(attack.gameObject);
        }

        protected void EndAttack()
        {
            IsAttacking = false;
            if (_boss.Movement.movingToCenter)
            {
                _boss.Movement.movingToCenter = false;
                _boss.Movement.StopMovement(false);
            }
        }

        public void SetPhase(int phase)
        {
            if (CurrentPhase == phase) return;
            CurrentPhase = phase;
            _stack.RegisterAttacks(GetPhaseAttacks());
        }

        public void SetAttackIntervalTime(float intervalTime)
        {
            BossAttackInterval = intervalTime;
        }

        public void SetProjectileSpeed(float value)
        {
            projectileSpeedMultiplier = value;
        }

        private List<AttackEntry> GetPhaseAttacks()
        {
            AttackSet set = CurrentPhase switch
            {
                2 => phase2,
                3 => phase3,
                _ => phase1
            };
            return set != null ? set.attacks : new List<AttackEntry>();
        }
    }
}
