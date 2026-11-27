using UnityEngine;

namespace Game50033
{
    public sealed class DogChargeAttack : TargetedBossAttack
    {
        [SerializeField] private DogChargeTuning tuning;

        public override bool CanStart => tuning != null && base.CanStart;

        protected override void Awake()
        {
            base.Awake();
            if (tuning == null)
            {
                //Debug.LogError($"{name} requires a dog charge tuning asset.", this);
                enabled = false;
                return;
            }

            windUpTime = Mathf.Max(0f, tuning.windUpDuration);
        }

        protected override Vector3 GetTelegraphEnd(Vector3 from, Vector3 target)
        {
            if (tuning == null) return from;

            Vector3 wallEnd = base.GetTelegraphEnd(from, target);
            Vector3 offset = target - from;
            offset.y = 0f;
            Vector3 direction = offset.sqrMagnitude > 0f
                ? offset.normalized
                : boss.Movement.FlipX ? Vector3.right : Vector3.left;
            float distance = Mathf.Min(offset.magnitude + Mathf.Max(0f, tuning.distancePastTarget),
                Vector3.Distance(from, wallEnd));
            return from + direction * distance;
        }
    }
}
