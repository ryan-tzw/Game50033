using Game50033;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BaseBoss : MonoBehaviour, IHittable
{
    [SerializeField] protected float BossMovementSpeed = 5f;
    [SerializeField, Min(0.01f)] protected float BossHealthPoints = 20000f;
    [SerializeField] protected float BossStopDistance = 2f;
    [SerializeField] protected float attackDuration = 1f;

    [SerializeField, Min(-1f)]
    [Tooltip("Use -1 to start at full HP. Shows current HP during Play.")]
    protected float BossCurrentHealth = -1f;
    protected int BossTotalPhases = 3;
    protected int BossCurrentPhase = 1;
    protected float BossTargetClearTime = 120f;
    [SerializeField] protected float BossAttackInterval = 3f;
    protected float bossAttackIntervalTimer;
    protected SpriteRenderer _sr;
    protected Rigidbody _rb;

    protected bool InRange = false;

    protected Player TargetedPlayer;

    // TODO: Add Player Runtime Set so that player positions...etc can be easily accessed by boss!
    private readonly List<Player> _players = new();
    protected bool IsAttacking; //Will probably change to states later on

    [SerializeField] private GameObject BoneProjectile;

    [SerializeField] private string bossName;
    [SerializeField] private Slider healthBarFill;
    [SerializeField] private Text bossText;
    [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.15f;

    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    private static readonly int BoneAttackId = Animator.StringToHash("BoneAttack");
    private static readonly int IsWalkingId = Animator.StringToHash("IsWalking");
    private MaterialPropertyBlock _hitProperties;
    private float _lastHitTime = float.NegativeInfinity;
    private Animator _attackAnimator;

    public float MaxHealth => BossHealthPoints;
    public float CurrentHealth => BossCurrentHealth;
    public bool IsDead => BossCurrentHealth <= 0f;

    protected void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
        _rb = GetComponent<Rigidbody>();

        if (_sr != null)
        {
            _attackAnimator = _sr.GetComponent<Animator>();
        }

        // Keep the boss at its arena height when players collide with it.
        _rb.constraints |= RigidbodyConstraints.FreezePositionY;
        _hitProperties = new MaterialPropertyBlock();
        BossHealthPoints = Mathf.Max(0.01f, BossHealthPoints);
        BossCurrentHealth = BossCurrentHealth < 0f
            ? BossHealthPoints
            : Mathf.Clamp(BossCurrentHealth, 0f, BossHealthPoints);
        UpdateHealthBar();
        UpdateBossText();

        if (IsDead)
        {
            Die();
        }
    }

    protected virtual void FixedUpdate()
    {
        TargetPlayer();
    }

    protected virtual void Update()
    {
        if (bossAttackIntervalTimer > 0f)
        {
            bossAttackIntervalTimer -= Time.deltaTime;
        }

        var elapsed = Time.time - _lastHitTime;
        SetHitFlash(Mathf.Clamp01(1f - elapsed / Mathf.Max(0.01f, hitFlashDuration)));

        if (_attackAnimator != null)
        {
            var velocity = _rb.linearVelocity;
            velocity.y = 0f;
            _attackAnimator.SetBool(IsWalkingId, !IsAttacking && velocity.sqrMagnitude > 0.01f);
        }
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(EndAttack));
        IsAttacking = false;
        if (_attackAnimator != null && _attackAnimator.isActiveAndEnabled)
        {
            _attackAnimator.SetBool(IsWalkingId, false);
        }
    }

    public void TakeHit(in HitInfo hit)
    {
        if (!isActiveAndEnabled || IsDead || hit.Damage <= 0f
            || float.IsNaN(hit.Damage) || float.IsInfinity(hit.Damage))
        {
            return;
        }

        BossCurrentHealth = Mathf.Max(0f, BossCurrentHealth - hit.Damage);
        UpdateHealthBar();
        UpdateBossText();
        _lastHitTime = Time.time;
        SetHitFlash(1f);

        if (IsDead)
        {
            Die();
        }
    }

    private void UpdateHealthBar()
    {
        if (healthBarFill != null)
        {
            healthBarFill.value = BossCurrentHealth / BossHealthPoints;
        }
    }

    private void UpdateBossText()
    {
        bossText.text = $"{bossName}: {BossCurrentHealth}/{MaxHealth}";
    }

    private void SetHitFlash(float amount)
    {
        if (_sr == null)
        {
            return;
        }

        _sr.GetPropertyBlock(_hitProperties);
        _hitProperties.SetFloat(FlashAmountId, amount);
        _sr.SetPropertyBlock(_hitProperties);
    }

    private void Die()
    {
        CancelInvoke();
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        gameObject.SetActive(false);
    }

    protected virtual void TargetPlayer()
    {
        // Players may leave or be destroyed during a scene transition without a trigger exit.
        _players.RemoveAll(player => player == null);
        if (_players.Count == 0)
        {
            TargetedPlayer = null;
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            return;
        }
        // Keep the current target while it remains in detection range.
        if (TargetedPlayer == null || !_players.Contains(TargetedPlayer))
        {
            TargetedPlayer = _players[Random.Range(0, _players.Count)];
        }
        FollowPlayer();
    }

    protected virtual void FollowPlayer()
    {
        if (TargetedPlayer == null) return;

        if (IsAttacking)
        {
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            return;
        }

        var playerPosition = TargetedPlayer.transform.position;
        Vector3 toPlayer = playerPosition - _rb.position;
        toPlayer.y = 0f;

        InRange = toPlayer.sqrMagnitude <= BossStopDistance * BossStopDistance;

        FaceDirection(toPlayer.x);

        if (InRange)
        {
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);

            if (bossAttackIntervalTimer <= 0f)
            {
                BasicAttack();
            }
            return;
        }

        var moveDir = toPlayer.normalized;
        _rb.linearVelocity = new Vector3(moveDir.x * BossMovementSpeed, _rb.linearVelocity.y, moveDir.z * BossMovementSpeed);
    }


    //Will clean up and use a new script for boss1 later on, this is temporary for checkoff
    private void BasicAttack()
    {
        IsAttacking = true;
        if (_attackAnimator != null)
        {
            _attackAnimator.SetTrigger(BoneAttackId);
        }

        int numberOfProjectiles = 2;
        foreach (var player in _players)
        {
            var currentPosition = player.transform.position;
            for (int i = 0; i < numberOfProjectiles; i++)
            {
                Vector2 spread = Random.insideUnitCircle * 1.5f;
                var target = currentPosition + new Vector3(spread.x, 0f, spread.y);

                var bone = Instantiate(BoneProjectile, transform.position, Quaternion.identity);
                bone.GetComponent<Bone>().FlyTo(target);
            }
        }
        bossAttackIntervalTimer = BossAttackInterval;
        Invoke(nameof(EndAttack), attackDuration);
    }

    private void EndAttack()
    {
        IsAttacking = false;
    }

    private void ChargeToPlayer()
    {

    }

    //Temporary implementation for finding players, will change later on
    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<Player>();
        if (player == null) return;
        if (_players.Contains(player)) return;

        _players.Add(player);
    }

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponentInParent<Player>();
        if (player == null) return;

        _players.Remove(player);
    }

    private void FaceDirection(float x)
    {
        if (IsAttacking) return;
        if (Mathf.Abs(x) < 0.01f) return;

        bool faceLeft = x < 0f;
        _sr.flipX = !faceLeft;
    }

}
