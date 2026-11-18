
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
        /// <summary>Players the boss may chase and attack, refreshed every frame (see RefreshTargets).</summary>
        public List<Player> players = new();

        protected int BossTotalPhases = 3;
        protected int BossCurrentPhase = 1;
        protected float BossTargetClearTime = 120f;

        protected SpriteRenderer _sr;
        protected Rigidbody _rb;

        protected bool InRange = false;
        protected Player TargetedPlayer;

        [SerializeField] private string _bossName;
        [SerializeField] private Slider _healthBar;
        [SerializeField] private Text _bossText;

        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int IsWalkingId = Animator.StringToHash("IsWalking");

        private Animator _attackAnimator;

        public bool CanMove => isActiveAndEnabled && Movement.isActiveAndEnabled && !Health.IsDead && !Attacks.IsAttacking;
        public bool CanTryAttacking => isActiveAndEnabled && Movement.InRange && !Health.IsDead && !Attacks.IsAttacking;

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
        }

        public void PlayAnimationTrigger(int triggerId)
        {
            if (_attackAnimator != null) _attackAnimator.SetTrigger(triggerId);
        }

        internal void Initialize()
        {
            if (_initialized) return;

            _session = FindFirstObjectByType<GameSession>();
            RefreshTargets();

            Movement = GetComponent<BossMovement>();
            Health = GetComponent<BossHealth>();
            Attacks = GetComponent<BossAttacks>();
            Movement.Initialize();
            Health.Initialize();
            Attacks.Initialize();
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
            Attacks.StartedAttack += () => { _attackAnimator.SetTrigger(AttackId); };
        }

        private void OnDisable()
        {
            // parts may already be destroyed when the scene unloads.
            if (Movement) { Movement.StopMovement(); Movement.enabled = false; }
            if (Attacks) Attacks.enabled = false;
        }

        public void SetTargets(IReadOnlyList<Player> livingPlayers)
        {
            _suppliedTargets = livingPlayers;
            RefreshTargets();
        }

        // refill the same list in place: BossMovement holds a reference to it and attacks copy it when they start.
        // In an encounter the targets are the EncounterManager's living players; in a test scene without one,
        // they are the session's players who are not downed.
        private void RefreshTargets()
        {
            players.Clear();
            if (_suppliedTargets != null)
            {
                foreach (var player in _suppliedTargets)
                    if (player != null) players.Add(player);
                return;
            }

            if (_session == null) return;
            foreach (var player in _session.Players)
                if (player != null && player.isActiveAndEnabled && !player.Health.IsDowned) players.Add(player);
        }

        protected virtual void Update()
        {
            RefreshTargets();

            if (_attackAnimator != null)
            {
                _attackAnimator.SetBool(IsWalkingId, Movement.IsMoving);
            }

            Movement.StopMovement(!CanMove);
            if (CanTryAttacking && (players.Count != 0)) { Attacks.TryAttacking(); }
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
        private void HandleHealthPhase(int phaseNumber)
        {
            var previous = Phase;
            HandlePhaseChange(phaseNumber);
            if (Phase != previous) PhaseChanged?.Invoke(Phase);
        }

        protected virtual void HandlePhaseChange(int phaseNumber)
        {
            Attacks.SetPhase(phaseNumber);
            Attacks.SetAttackIntervalTime(2);
            Attacks.SetProjectileSpeed(1.5f);
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
    }
}