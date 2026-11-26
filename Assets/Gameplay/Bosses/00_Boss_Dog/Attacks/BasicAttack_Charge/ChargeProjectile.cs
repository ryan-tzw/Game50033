using UnityEngine;

namespace Game50033
{
    [RequireComponent(typeof(BoxCollider))]
    public class ChargeProjectile : BaseBossProjectile
    {
        private SpriteRenderer _sr;
        private bool _bossHidden;
        private bool _controlReleased;

        public override bool ControlsBoss => true;

        public override bool CanLaunch(Vector3 position, Vector3 direction)
        {
            if (direction == Vector3.zero) return false;
            var box = GetComponent<BoxCollider>();
            GetBox(box, GetLaunchPosition(position), out var center, out var halfSize);

            var contacts = Physics.OverlapBox(center, halfSize + Vector3.one * Physics.defaultContactOffset,
                Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            foreach (var contact in contacts)
            {
                if (IsWall(contact) && IsMovingIntoWall(contact, center, direction)) return false;
            }

            // Leave room to move at least one body length before hitting a wall.
            float distance = 2f * (Mathf.Abs(direction.x) * halfSize.x + Mathf.Abs(direction.z) * halfSize.z);
            foreach (var hit in Physics.BoxCastAll(center, halfSize, direction, Quaternion.identity,
                distance + Physics.defaultContactOffset, ~0, QueryTriggerInteraction.Collide))
            {
                if (!IsWall(hit.collider)) continue;
                // Touching a wall behind us must not prevent charging back into the arena.
                if (hit.distance <= Physics.defaultContactOffset
                    && !IsMovingIntoWall(hit.collider, center, direction)) continue;
                return false;
            }
            return true;
        }

        public override void OnTriggerEnter(Collider other)
        {
            if (IsWall(other))
            {
                GetBox(GetComponent<BoxCollider>(), transform.position, out var center, out _);
                if (!IsMovingIntoWall(other, center, _targetDirection)) return;
            }
            base.OnTriggerEnter(other);
        }

        private void GetBox(BoxCollider box, Vector3 position, out Vector3 center, out Vector3 halfSize)
        {
            var scale = transform.lossyScale;
            center = position + Vector3.Scale(box.center, scale);
            halfSize = Vector3.Scale(box.size * 0.5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        }

        private static bool IsWall(Collider collider) => collider.CompareTag("Wall")
            || collider.transform.parent != null && collider.transform.parent.CompareTag("Wall");

        private static bool IsMovingIntoWall(Collider wall, Vector3 center, Vector3 direction)
        {
            Vector3 towardWall = wall.ClosestPoint(center) - center;
            return towardWall == Vector3.zero || Vector3.Dot(towardWall, direction) > 0f;
        }

        private void Awake()
        {
            _sr = GetComponentInChildren<SpriteRenderer>();

        }

        protected override void SetSpriteDirection()
        {
            _sr.flipX = _boss.Movement.FlipX;
        }

        protected override void DestroyProjectile()
        {
            if (_controlReleased) return;
            base.DestroyProjectile();
            if (_sr != null) _sr.enabled = false;
            ReleaseControl();
        }

        public override void InterruptBossControl()
        {
            if (_controlReleased) return;
            StopAllCoroutines();
            ReleaseControl();
            Destroy(gameObject);
        }

        private void OnDisable() => InterruptBossControl();
        private void OnDestroy() => ReleaseControl();

        private void ReleaseControl()
        {
            if (_controlReleased) return;
            _controlReleased = true;
            if (_boss != null)
            {
                // Put the real boss where the charge stopped, even if it was interrupted.
                if (_bossHidden)
                {
                    Vector3 position = transform.position;
                    position.y = _boss.transform.position.y;
                    _boss.SetPosition(position);
                }
                _boss.SetVisibility(true);
                _boss.Movement.StopMovement();
            }
            if (_targetedBossAttack != null) _targetedBossAttack.ReleaseBossControl(this);
        }

        protected override void HideBoss()
        {
            if (_controlReleased) return;
            if (!_bossHidden)
            {
                _boss.SetVisibility(false);
                _bossHidden = true;
            }
            Vector3 follow = transform.position;
            follow.y = _boss.transform.position.y;
            _boss.SetPosition(follow);
        }
    }
}
