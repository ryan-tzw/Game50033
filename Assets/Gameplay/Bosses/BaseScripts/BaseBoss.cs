
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
        public List<Player> players;

        // [SerializeField] protected float BossMovementSpeed = 5f;
        // [SerializeField, Min(0.01f)] protected float BossHealthPoints = 20000f;
        // [SerializeField] protected float BossStopDistance = 2f;
        // [SerializeField] protected float attackDuration = 1f;
        // [SerializeField] protected float BossAttackInterval = 3f;
        // protected float bossAttackIntervalTimer;

        // [SerializeField, Min(-1f)]
        // [Tooltip("Use -1 to start at full HP. Shows current HP during Play.")]
        // protected float BossCurrentHealth = -1f;
        protected int BossTotalPhases = 3;
        protected int BossCurrentPhase = 1;
        protected float BossTargetClearTime = 120f;

        protected SpriteRenderer _sr;
        protected Rigidbody _rb;

        protected bool InRange = false;

        protected Player TargetedPlayer;

        // protected bool IsAttacking; //Will probably change to states later on

        // [SerializeField] private GameObject BoneProjectile;

        [SerializeField] private string _bossName;
        [SerializeField] private Slider _healthBar;
        [SerializeField] private Text _bossText;
        // [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.15f;

        // private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int IsWalkingId = Animator.StringToHash("IsWalking");
        // private MaterialPropertyBlock _hitProperties;
        // private float _lastHitTime = float.NegativeInfinity;
        private Animator _attackAnimator;

        // public float MaxHealth => BossHealthPoints;
        // public float CurrentHealth => BossCurrentHealth;
        // public bool IsDead => BossCurrentHealth <= 0f;

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

            // _session = FindFirstObjectByType<GameSession>();
            // _players = new(_session.Players);

            // Keep the boss at its arena height when players collide with it.
            // _hitProperties = new MaterialPropertyBlock();
            // BossHealthPoints = Mathf.Max(0.01f, BossHealthPoints);
            // BossCurrentHealth = BossCurrentHealth < 0f
            //     ? BossHealthPoints
            //     : Mathf.Clamp(BossCurrentHealth, 0f, BossHealthPoints);
            // UpdateHealthBar();
            // UpdateBossText();

            // if (IsDead)
            // {
            //     Die();
            // }
        }

        internal void Initialize()
        {
            if (_initialized) return;

            _session = FindFirstObjectByType<GameSession>();
            players = new(_session.Players);

            Movement = GetComponent<BossMovement>();
            Health = GetComponent<BossHealth>();
            Attacks = GetComponent<BossAttacks>();
            Movement.Initialize();
            Health.Initialize();
            Attacks.Initialize();
            Health.HealthChanged += HandleHealthChange;
            Health.Dead += Die;

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
            // CancelInvoke(nameof(EndAttack));
            // IsAttacking = false;
            // if (_attackAnimator != null && _attackAnimator.isActiveAndEnabled)
            // {
            //     _attackAnimator.SetBool(IsWalkingId, false);
            // }
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
            // if (bossAttackIntervalTimer > 0f)
            // {
            //     bossAttackIntervalTimer -= Time.deltaTime;
            // }

            // var elapsed = Time.time - _lastHitTime;
            // SetHitFlash(Mathf.Clamp01(1f - elapsed / Mathf.Max(0.01f, hitFlashDuration)));

            if (_attackAnimator != null)
            {
                // var velocity = _rb.linearVelocity;
                // velocity.y = 0f;
                // _attackAnimator.SetBool(IsWalkingId, Movement.IsMoving && !IsAttacking && velocity.sqrMagnitude > 0.001f);
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

        //Will clean up and use a new script for boss1 later on, this is temporary for checkoff
        // private void BasicAttack()
        // {
        //     IsAttacking = true;
        //     if (_attackAnimator != null)
        //     {
        //         _attackAnimator.SetTrigger(BoneAttackId);
        //     }

        //     int numberOfProjectiles = 2;
        //     foreach (var player in Movement.players)
        //     {
        //         var currentPosition = player.transform.position;
        //         for (int i = 0; i < numberOfProjectiles; i++)
        //         {
        //             Vector2 spread = Random.insideUnitCircle * 1.5f;
        //             var target = currentPosition + new Vector3(spread.x, 0f, spread.y);

        //             var bone = Instantiate(BoneProjectile, transform.position, Quaternion.identity);
        //             bone.GetComponent<Bone>().FlyTo(target);
        //         }
        //     }
        //     bossAttackIntervalTimer = BossAttackInterval;
        //     Invoke(nameof(EndAttack), attackDuration);
        // }

        // private void EndAttack()
        // {
        //     IsAttacking = false;
        // }

        // private void ChargeToPlayer()
        // {

        // }

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
    }
}