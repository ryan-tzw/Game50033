
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
    public class BaseBoss : MonoBehaviour
    {
        public BossMovement Movement { get; private set; }
        public BossHealth Health { get; private set; }
        public BossAttacks Attacks { get; private set; }
        private bool _initialized;

        private GameSession _session;
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

        internal void Initialize()
        {
            if (_initialized) return;

            _session = FindFirstObjectByType<GameSession>();
            players.Clear();
            players.AddRange(_session.Players);
            //players = new(_session.Players);

            Movement = GetComponent<BossMovement>();
            Health = GetComponent<BossHealth>();
            Attacks = GetComponent<BossAttacks>();
            Movement.Initialize();
            Health.Initialize();
            Attacks.Initialize();
            Health.HealthChanged += HandleHealthChange;
            Health.Dead += Die;
            Health.ChangePhase += HandlePhaseChange;

            _initialized = true;
        }

        private void OnEnable()
        {
            _session.PlayerRegistered += UpdatePlayers;
            _session.PlayerUnregistered += UpdatePlayers;
            Attacks.StartedAttack += () => { _attackAnimator.SetTrigger(AttackId); };
        }

        private void OnDisable()
        {
            _session.PlayerRegistered -= UpdatePlayers;
            _session.PlayerUnregistered -= UpdatePlayers;
        }

        private void UpdatePlayers(Player player)
        {
            if (players.Contains(player)) players.Remove(player);
            else players.Add(player);
        }

        protected virtual void Update()
        {
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
            CancelInvoke();
            Movement.StopMovement();
            gameObject.SetActive(false);
        }

        protected virtual void HandlePhaseChange(int phaseNumber)
        {
            Attacks.SetPhase(phaseNumber);
            Attacks.SetAttackIntervalTime(2);
            Attacks.SetProjectileSpeed(1.5f);
        }

        // Temporary implementation for finding players, will change later on
        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<Player>();
            if (player == null) return;
            if (players.Contains(player)) return;

            players.Add(player);
        }

        private void OnTriggerExit(Collider other)
        {
            var player = other.GetComponentInParent<Player>();
            if (player == null) return;

            players.Remove(player);
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