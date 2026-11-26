
namespace Game50033
{
    using UnityEngine;
    using UnityEngine.UI;
    using System.Collections.Generic;
    using NUnit.Framework;

    /// <summary>
    /// Handles Animations & Consolidates all Boss Scripts
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossHealth), typeof(BossMovement), typeof(BossAttacks))]
    public class BaseBoss : MonoBehaviour, IEncounterBoss
    {
        // IEncounterBoss: what the encounter may know about this boss (victory, fight stats, Discordance fill rate).
        public bool IsDefeated => Health != null && Health.IsDead;
        public int Phase => Attacks != null ? Attacks.CurrentPhase : 1;
        public event System.Action<IEncounterBoss> Defeated;
        public event System.Action<HitInfo, float> Damaged;
        public event System.Action<int> PhaseChanged;

        public BossMovement Movement { get; private set; }
        public BossHealth Health { get; private set; }
        public BossAttacks Attacks { get; private set; }
        private bool _initialized;

        private GameSession _session;
        private IReadOnlyList<Player> _suppliedTargets;
        public IReadOnlyList<Player> Targets => _suppliedTargets
            ?? (_session != null ? _session.LivingPlayers : System.Array.Empty<Player>());

        public int BossTotalPhases;
        protected float BossTargetClearTime = 120f;
        public int BossNextPhase = 2;

        protected SpriteRenderer _sr;
        protected Rigidbody _rb;

        protected bool InRange = false;
        protected Player TargetedPlayer;

        [SerializeField] private string _bossName;
        [SerializeField] private Slider _healthBar;
        [SerializeField] private Text _bossText;

        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int IsWalkingId = Animator.StringToHash("IsWalking");
        protected static readonly int PhaseChangeId = Animator.StringToHash("ChangingPhase");

        protected Animator _attackAnimator;

        public bool CanMove => isActiveAndEnabled && Movement.isActiveAndEnabled && !Health.IsDead && !Attacks.IsAttacking && fightStarted;
        public bool CanTryAttacking => isActiveAndEnabled && Movement.InRange && !Health.IsDead && !Attacks.IsAttacking && fightStarted;
        [SerializeField] private bool fightStarted = false;

        protected void Awake()
        {
            Initialize();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _rb = GetComponent<Rigidbody>();
            _rb.constraints |= RigidbodyConstraints.FreezePositionY;

            if (_sr != null)
            {
                _attackAnimator = _sr.GetComponent<Animator>();
            }

            _bossText = GameObject.Find("BossText").GetComponent<Text>();
            _healthBar = GameObject.Find("BossHealthBar").GetComponent<Slider>();
            HandleHealthChange(Health.CurrentHealth, Health.MaxHealth);

            if (BossNextPhase != 2) BossNextPhase = 2;
        }

        public void PlayAnimationTrigger(int triggerId)
        {
            if (_attackAnimator != null) _attackAnimator.SetTrigger(triggerId);
        }

        internal void Initialize()
        {
            if (_initialized) return;

            _session = FindFirstObjectByType<GameSession>();

            Movement = GetComponent<BossMovement>();
            Health = GetComponent<BossHealth>();
            Attacks = GetComponent<BossAttacks>();
            Movement.Initialize();
            Attacks.Initialize();
            // If no phase count was set, use the phases assigned to BossAttacks.
            if (BossTotalPhases <= 0) BossTotalPhases = Attacks.ConfiguredPhaseCount;
            if (BossTotalPhases <= 0) Debug.LogError("Boss has no configured attack phases.", this);
            Health.Initialize(BossTotalPhases);
            Health.HealthChanged += HandleHealthChange;
            Health.Dead += Die;
            Health.ChangePhase += HandleHealthPhase;
            Health.Damaged += (hit, applied) => Damaged?.Invoke(hit, applied);

            _initialized = true;
        }

        private void OnEnable()
        {
            // disabling the boss (e.g. the encounter freezing it after the fight) pauses its parts with it.
            if (Movement) Movement.enabled = true;
            if (Attacks) Attacks.enabled = true;
            if (Attacks) Attacks.StartedAttack += HandleAttackStarted;
        }

        private void HandleAttackStarted()
        {
            if (_attackAnimator != null) _attackAnimator.SetTrigger(AttackId);
        }

        private void OnDisable()
        {
            // parts may already be destroyed when the scene unloads.
            if (Movement) { Movement.StopMovement(); Movement.enabled = false; }
            if (Attacks)
            {
                Attacks.StartedAttack -= HandleAttackStarted;
                Attacks.enabled = false;
            }
        }

        public void SetTargets(IReadOnlyList<Player> livingPlayers)
        {
            _suppliedTargets = livingPlayers;
        }

        protected virtual void Update()
        {
            if (_attackAnimator != null)
            {
                _attackAnimator.SetBool(IsWalkingId, Movement.IsMoving);
            }

            Movement.StopMovement(!CanMove);
            if (CanTryAttacking && Targets.Count != 0) { Attacks.TryAttacking(); }
            
        }

        private void HandleHealthChange(float currentHealth, float maxHealth)
        {
            if (_healthBar) { _healthBar.value = currentHealth / maxHealth; }
            if (_bossText) { _bossText.text = $"{_bossName}: {currentHealth}/{maxHealth}"; }
        }

        private void Die()
        {
            Defeated?.Invoke(this);
            CancelInvoke();
            Movement.StopMovement();
            gameObject.SetActive(false);
        }

        // BossHealth asks for the phase on every hit past its threshold; the boss's own handling is unchanged,
        // and the encounter hears PhaseChanged only when the phase actually changes.
        // Should only be called once per phase
        private void HandleHealthPhase(int phaseNumber)
        {
            if (Phase != BossNextPhase)
            {
                Debug.Log($"{_bossName}: Transitioning from Phase {Phase} to Phase {BossNextPhase}.");
                HandlePhaseChange(phaseNumber);
                BossNextPhase = Mathf.Clamp(Phase + 1, 2, BossTotalPhases);
                PhaseChanged?.Invoke(Phase);
            }
        }

        protected virtual void HandlePhaseChange(int phaseNumber)
        {
            Attacks.SetPhase(phaseNumber);
            Attacks.SetAttackIntervalTime(2);
            Attacks.SetProjectileSpeed(1.5f);

            try { StartCoroutine(PlayAnimationInPlace(PhaseChangeId)); }
            catch { Debug.LogWarning("Animator does not include Phase Change Animation"); }
        }

        public void SetVisibility(bool visibility)
        {
            _sr.gameObject.SetActive(visibility);
        }

        public void SetPosition(Vector3 position)
        {
            if (_rb != null) _rb.position = position;
            transform.position = position;
        }

        public virtual System.Collections.IEnumerator PlayAnimationInPlace(int animationId)
        {
            Debug.Log("Playing Phase Changing Animation");
            Attacks.enabled = false;
            yield return null;
            _attackAnimator.SetTrigger(animationId);
            yield return null;
            yield return new WaitUntil(() => _attackAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f);
            Attacks.enabled = true;
        }
    }
}
