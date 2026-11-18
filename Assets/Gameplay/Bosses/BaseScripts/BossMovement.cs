namespace Game50033
{
    using UnityEngine;
    using UnityEngine.EventSystems;

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
        private BaseBoss _boss;
        public bool canMove;

        public bool IsMoving => (_rb.linearVelocity.sqrMagnitude > 0.01f) ? true : false;
        public bool InRange = false;
        public bool FlipX => _sr.flipX;

        public bool movingToCenter = false;

        internal void Initialize()
        {
            if (_initialized) return;

            _rb = GetComponent<Rigidbody>();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _boss = GetComponent<BaseBoss>();
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
            if (movingToCenter) MoveToCenter();
            else TargetPlayer();
        }

        public void StopMovement(bool lockMovement = true)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            canMove = !lockMovement;
        }

        protected virtual void TargetPlayer()
        {
            var targets = _boss.Targets;
            var targetCount = targets.Count;
            if (targetCount == 0)
            {
                _followTarget = null;
                StopMovement();
                return;
            }

            // keep following the same player while they remain eligible.
            var keepTarget = false;
            if (_followTarget != null)
            {
                for (var i = 0; i < targetCount; i++)
                {
                    if (targets[i] != _followTarget) continue;
                    keepTarget = true;
                    break;
                }
            }

            if (!keepTarget)
            {
                Debug.Log("Adding Player as follow target!");
                _followTarget = targets[Random.Range(0, targetCount)];
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

            if (canMove)
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

        public bool MoveToCenter()
        {
            movingToCenter = true;
            if ((Mathf.Abs(transform.position.x) > 0.05f) && (Mathf.Abs(transform.position.z) > 0.05f))
            {
                StopMovement(false);
                Vector3 moveDist = new Vector3(0f, 0f, 0f) - transform.position;
                Vector3 moveDir = moveDist.normalized;
                _rb.linearVelocity = new Vector3(moveDir.x * BossMovementSpeed, _rb.linearVelocity.y, moveDir.z * BossMovementSpeed);
                movingToCenter = true;
                return false;
            }
            else
            {
                StopMovement(true);
                return true;
            }
        }

        private void FaceDirection(float x)
        {
            // if (IsAttacking) return;
            if (Mathf.Abs(x) < 0.01f) return;

            bool faceLeft = x < 0f;
            _sr.flipX = !faceLeft;
        }

        public void FaceTowards(Vector3 position)
        {
            float x = position.x - _rb.position.x;
            if (Mathf.Abs(x) < 0.01f) return;

            bool faceLeft = x < 0f;
            _sr.flipX = !faceLeft;
        }

    }
}
