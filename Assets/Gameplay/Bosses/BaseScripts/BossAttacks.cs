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
        [SerializeField] private int _currentAttackIdx;
        public event Action StartedAttack;

        // [SerializeField] private float attackDuration = 1f;
        [SerializeField] private float BossAttackInterval = 3f;
        protected float bossAttackIntervalTimer;
        public int CurrentPhase { get; private set; } = 1;

        internal void Initialize()
        {
            if (_initialized) return;
            _currentAttackIdx = 0;
            _boss = GetComponent<BaseBoss>();
            _stack = GetComponent<BossAttackStack>();
            StartedAttack += () => { IsAttacking = true; };
            _stack.RegisterAttacks(GetPhaseAttacks());
            _initialized = true;
        }

        protected void Update()
        {
            if (bossAttackIntervalTimer > 0f) bossAttackIntervalTimer -= Time.deltaTime;
        }

        public void TryAttacking()
        {
            //if (bossAttackIntervalTimer <= 0f)
            //{
            //    StartedAttack?.Invoke();
            //    GameObject currentAttack = Instantiate(p1Attacks[_currentAttackIdx], transform.position, Quaternion.identity);
            //    var attack = currentAttack.GetComponent<BaseBossAttack>();
            //    attack.Setup(_boss);
            //    attack.Attack();
            //    _currentAttackIdx++;
            //    if (_currentAttackIdx >= p1Attacks.Count) _currentAttackIdx = 0;
            //    bossAttackIntervalTimer = BossAttackInterval;
            //    float recovery = attack.windUpTime + attack.recoveryTime;
            //    Invoke(nameof(EndAttack), recovery);
            //    Destroy(currentAttack, recovery);
            //}
            if (IsAttacking || bossAttackIntervalTimer > 0f) return;
            GameObject nextAttack = _stack.GetNext();

            if (nextAttack == null) return;

            StartedAttack?.Invoke();
            GameObject currentAttack = Instantiate(nextAttack, transform.position, Quaternion.identity);
            var attack = currentAttack.GetComponent<BaseBossAttack>();
            attack.Setup(_boss);
            attack.Attack();
            bossAttackIntervalTimer = BossAttackInterval;
            float recovery = attack.windUpTime + attack.recoveryTime;
            Invoke(nameof(EndAttack), recovery);
            Destroy(currentAttack, recovery);
        }

        protected void EndAttack()
        {
            IsAttacking = false;
        }

        public void SetPhase(int phase)
        {
            if (CurrentPhase == phase) return;
            CurrentPhase = phase;
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
