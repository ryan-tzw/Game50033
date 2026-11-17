namespace Game50033
{
    using UnityEngine;

    public static class AttackTargeting
    {
        public static Behaviour FindNearestTarget(Vector3 origin)
        {
            Behaviour nearest = null;
            var nearestDistanceSquared = float.PositiveInfinity;
            // selection happens only on request, so no registry or per-frame physics scan is needed.
            foreach (var boss in Object.FindObjectsByType<BaseBoss>(FindObjectsSortMode.None))
            {
                if (!boss.isActiveAndEnabled || boss.IsDead) continue;
                var distanceSquared = (boss.transform.position - origin).sqrMagnitude;
                if (distanceSquared >= nearestDistanceSquared) continue;
                nearest = boss;
                nearestDistanceSquared = distanceSquared;
            }
            // training dummies already expose this shared combat contract; no instrument-specific lookup is needed.
            var hurtboxes = CombatHurtbox.Active;
            for (var i = 0; i < hurtboxes.Count; i++)
            {
                var hurtbox = hurtboxes[i];
                if (hurtbox == null || !hurtbox.isActiveAndEnabled
                    || hurtbox.Target is not Behaviour target || !target.isActiveAndEnabled
                    || target.GetComponentInParent<Player>() != null
                    || target.GetComponentInParent<Reverberation>() != null) continue;
                var boss = target.GetComponentInParent<BaseBoss>();
                if (boss != null && (!boss.isActiveAndEnabled || boss.IsDead)) continue;
                var distanceSquared = (target.transform.position - origin).sqrMagnitude;
                if (distanceSquared >= nearestDistanceSquared) continue;
                nearest = target;
                nearestDistanceSquared = distanceSquared;
            }
            return nearest;
        }

        public static Vector3 GetAimPoint(Behaviour target, Vector3 origin)
        {
            var visual = target.GetComponentInChildren<Renderer>();
            var collider = target.GetComponentInChildren<Collider>();
            if (visual == null && collider == null) return target.transform.position;
            var bounds = visual != null ? visual.bounds : collider.bounds;
            var point = bounds.center;
            var inset = Mathf.Min(0.05f, bounds.extents.y * 0.5f);
            // preserve launch-height aiming for projectiles rather than aiming over or under the target.
            point.y = Mathf.Clamp(origin.y, bounds.min.y + inset, bounds.max.y - inset);
            return point;
        }
    }
}
