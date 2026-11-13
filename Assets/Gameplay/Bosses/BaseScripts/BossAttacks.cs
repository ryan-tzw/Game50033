namespace Game50033
{
    using UnityEngine;
    using System.Collections.Generic;
    using System;
    using Unity.VisualScripting;

    public class BossAttacks : MonoBehaviour
    {
        private bool _initialized;

        [Header("Phase 1")]
        [SerializeField] private List<GameObject> p1Attacks;

        [Header("Phase 2")]
        [SerializeField] private List<GameObject> p2Attacks;

        [Header("Phase 3")]
        [SerializeField] private List<GameObject> p3Attacks;

        public bool IsAttacking { get; private set; }
        [SerializeField] private int _currentAttackIdx;
        public event Action StartedAttack;

        // [SerializeField] private float attackDuration = 1f;
        [SerializeField] private float BossAttackInterval = 3f;
        protected float bossAttackIntervalTimer;

        void Awake()
        {
            Initialize();
        }

        internal void Initialize()
        {
            if (_initialized) return;
            _currentAttackIdx = 0;
            StartedAttack += () => { IsAttacking = true; };
            _initialized = true;
        }

        protected void Update()
        {
            if (bossAttackIntervalTimer > 0f) bossAttackIntervalTimer -= Time.deltaTime;
        }

        public void TryAttacking()
        {
            if (bossAttackIntervalTimer <= 0f)
            {
                StartedAttack?.Invoke();
                GameObject currentAttack = Instantiate(p1Attacks[_currentAttackIdx], transform.position, Quaternion.identity);
                currentAttack.GetComponent<BaseBossAttack>().Attack();
                _currentAttackIdx++;
                if (_currentAttackIdx >= p1Attacks.Count) _currentAttackIdx = 0;
                bossAttackIntervalTimer = BossAttackInterval;
                Invoke(nameof(EndAttack), currentAttack.GetComponent<BaseBossAttack>().recoveryTime);
                Destroy(currentAttack);
            }
        }

        protected void EndAttack()
        {
            IsAttacking = false;
        }
    }
}
