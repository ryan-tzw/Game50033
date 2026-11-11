using Game50033;
using NUnit.Framework;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

public class BaseBoss : MonoBehaviour
{
    [SerializeField] protected float BossMovementSpeed = 5f;
    [SerializeField] protected float BossHealthPoints = 20000f;
    [SerializeField] protected float BossStopDistance = 2f;
    [SerializeField] protected float attackDuration = 1f;

    protected float BossCurrentHealth;
    protected int BossTotalPhases = 3;
    protected int BossCurrentPhase = 1;
    protected float BossTargetClearTime = 120f;
    [SerializeField] protected float BossAttackInterval = 3f;
    protected float bossAttackIntervalTimer;
    protected SpriteRenderer _sr;
    protected Rigidbody _rb;

    protected bool InRange = false;

    protected PlayerController TargetedPlayer;

    private readonly List<PlayerController> _players = new();
    protected bool IsAttacking; //Will probably change to states later on

    [SerializeField] private GameObject BoneProjectile;

    protected void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
        _rb = GetComponent<Rigidbody>();
        BossCurrentHealth = BossHealthPoints;
    }

    protected virtual void FixedUpdate()
    {
        TargetPlayer();
    }

    protected virtual void Update()
    {
        if(bossAttackIntervalTimer > 0f)
        {
            bossAttackIntervalTimer -= Time.deltaTime;
        }
    }

    protected virtual void TargetPlayer()
    {
        if (_players.Count == 0) return;
        TargetedPlayer = _players[Random.Range(0, _players.Count)];
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

        FaceDirection(playerPosition.x - _rb.position.x);

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
        int numberOfProjectiles = 2;
        foreach(var player in _players)
        {
            var currentPosition = player.transform.position;
            for(int i = 0; i < numberOfProjectiles; i++)
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
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;
        if (_players.Contains(player)) return;

        _players.Add(player);
    }

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponentInParent<PlayerController>();
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
