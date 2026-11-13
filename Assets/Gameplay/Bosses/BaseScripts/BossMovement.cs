namespace Game50033
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    /// <summary>
    /// Handles targeting and movement
    /// </summary>
    public class BossMovement : MonoBehaviour
    {
        private bool _initialized;

        [SerializeField] protected float BossMovementSpeed = 5f;
        [SerializeField] protected float BossStopDistance = 2f;

        private Rigidbody _rb;
        private SpriteRenderer _sr;

        private Player _followTarget;
        private List<Player> _players;
        private bool _canMove;

        public bool IsMoving => (_rb.linearVelocity.sqrMagnitude > 0.01f) ? true : false;
        public bool InRange = false;

        private void Awake()
        {
            Initialize();
        }

        internal void Initialize()
        {
            if (_initialized) return;

            _rb = GetComponent<Rigidbody>();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _players = GetComponent<BaseBoss>().players;
            _initialized = true;
        }

        private void OnDisable()
        {
            CancelInvoke();
            // IsAttacking = false;
            // if (_attackAnimator != null && _attackAnimator.isActiveAndEnabled)
            // {
            //     _attackAnimator.SetBool(IsWalkingId, false);
            // }
        }

        protected virtual void FixedUpdate()
        {
            TargetPlayer();
        }

        public void StopMovement(bool lockMovement = true)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _canMove = !lockMovement;
        }

        protected virtual void TargetPlayer()
        {
            // _players may leave or be destroyed during a scene transition without a trigger exit.
            // _players.RemoveAll(player => player == null);
            if (_players.Count == 0)
            {
                _followTarget = null;
                // _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                StopMovement();
                return;
            }
            // Keep the current target while it remains in detection range.
            if (_followTarget == null || !_players.Contains(_followTarget))
            {
                Debug.Log("Adding Player as follow target!");
                _followTarget = _players[Random.Range(0, _players.Count)];
                StopMovement(false);
            }
            FollowPlayer();
        }

        protected virtual void FollowPlayer()
        {
            if (_followTarget == null) return;

            // if (IsAttacking)
            // {
            //     StopMovement();
            //     return;
            // }

            if (_canMove)
            {
                // Debug.Log("Starting to Follow Player!");
                var playerPosition = _followTarget.transform.position;
                Vector3 toPlayer = playerPosition - _rb.position;
                toPlayer.y = 0f;

                InRange = toPlayer.sqrMagnitude <= BossStopDistance * BossStopDistance;

                FaceDirection(toPlayer.x);
                if (InRange)
                {
                    // Debug.Log("Player in range, stopping follow.");
                    StopMovement(true);
                    // _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);

                    // if (bossAttackIntervalTimer <= 0f)
                    // {
                    //     AttackTargeting.Attack();
                    // }
                    return;
                }

                // Debug.Log("Player not in range, proceeding to continue to follow!");
                var moveDir = toPlayer.normalized;
                _rb.linearVelocity = new Vector3(moveDir.x * BossMovementSpeed, _rb.linearVelocity.y, moveDir.z * BossMovementSpeed);
            }
        }

        private void FaceDirection(float x)
        {
            // if (IsAttacking) return;
            if (Mathf.Abs(x) < 0.01f) return;

            bool faceLeft = x < 0f;
            _sr.flipX = !faceLeft;
        }

    }
}
