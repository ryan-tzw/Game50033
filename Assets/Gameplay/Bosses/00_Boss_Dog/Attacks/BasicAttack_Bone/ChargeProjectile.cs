using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace Game50033
{
    public class ChargeProjectile : BaseBossProjectile
    {
        private SpriteRenderer _sr;
        private bool _bossHidden;

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
            base.DestroyProjectile();
            _sr.enabled = false;
            _boss.SetVisibility(true);
            
        }
        protected override void WindUpAttack()
        {
            if (_elapsed >= _windUpDuration) {
                if (!_bossHidden)
                {
                    _boss.SetVisibility(false);
                    _bossHidden = true;
                }
                StrikePosition();
                Vector3 follow = transform.position;
                follow.y = _boss.transform.position.y;
                _boss.SetPosition(follow);
            }
            else
            {
                _target = _targetedBossAttack.RetrievePlayerPosition(_targetedPlayer) + new Vector3(0f, _targetedBossAttack.projectileHomingHeight, 0f);
                MoveTowards(_target);
            }
        }
    }
}
