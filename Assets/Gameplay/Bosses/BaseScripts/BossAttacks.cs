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
        private BossDiscordance _discordance;

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
        internal IReadOnlyList<AttackEntry> DebugAttackOptions => GetPhaseAttacks();
        internal GameObject DebugQueuedAttack { get; private set; }
        internal GameObject DebugRepeatedAttack { get; private set; }


        internal void Initialize()
        {
            if (_initialized) return;
            _boss = GetComponent<BaseBoss>();
            _stack = GetComponent<BossAttackStack>();
            _discordance = GetComponent<BossDiscordance>();
            StartedAttack += () => { IsAttacking = true; };
            _stack.RegisterAttacks(GetPhaseAttacks());
            _initialized = true;
        }

        protected void Update()
        {
            if (bossAttackIntervalTimer > 0f)
            {
                // only accelerate the remaining gap once the cast and its authored recovery have ended.
                var delayScale = !IsAttacking && _discordance != null ? _discordance.AttackDelayMultiplier : 1f;
                bossAttackIntervalTimer -= Time.deltaTime / Mathf.Max(0.05f, delayScale);
            }

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
            DebugQueuedAttack = null;
            DebugRepeatedAttack = null;
            if (ReferenceEquals(_controlledAttack, null)) return;
            ClearControlledAttack();
            IsAttacking = false;
            if (_boss != null && _boss.Movement != null) _boss.Movement.StopMovement();
        }

        internal void QueueDebugAttack(GameObject prefab)
        {
            if (IsInCurrentDeck(prefab)) DebugQueuedAttack = prefab;
        }

        internal void SetDebugRepeat(GameObject prefab)
        {
            DebugRepeatedAttack = IsInCurrentDeck(prefab) ? prefab : null;
        }

        private bool IsInCurrentDeck(GameObject prefab)
        {
            if (prefab == null) return false;
            foreach (var entry in GetPhaseAttacks())
                if (entry.attack == prefab && entry.count > 0) return true;
            return false;
        }

        public void TryAttacking()
        {
            if (!isActiveAndEnabled || !_boss.isActiveAndEnabled || _boss.Health.IsDead
                || IsAttacking || bossAttackIntervalTimer > 0f) return;
            _triedAttacks.Clear();
            // requests go through normal setup and never consume a card from the authored stack.
            var queued = DebugQueuedAttack;
            var repeat = DebugRepeatedAttack;
            DebugQueuedAttack = null;
            int attempts = _stack.SelectionAttemptLimit + (queued != null ? 1 : 0) + (repeat != null ? 1 : 0);
            while (attempts-- > 0)
            {
                var nextAttack = queued != null ? queued : repeat != null ? repeat : _stack.GetNext();
                queued = null;
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
                    if (nextAttack == repeat)
                    {
                        // wait for the selected attack's prerequisites without creating a prefab every frame.
                        bossAttackIntervalTimer = 0.25f;
                        return;
                    }
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
            if (!IsInCurrentDeck(DebugQueuedAttack)) DebugQueuedAttack = null;
            if (!IsInCurrentDeck(DebugRepeatedAttack)) DebugRepeatedAttack = null;
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
