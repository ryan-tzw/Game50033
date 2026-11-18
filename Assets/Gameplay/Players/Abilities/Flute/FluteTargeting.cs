namespace Game50033
{
    using UnityEngine;

    // Keep projectile acquisition and teammate REV priority independent of shared idle-facing aim.
    internal static class FluteTargeting
    {
        internal static Collider FindTarget(Player source, Vector3 origin, float range, int collisionMask,
            Collider attackVolume, ResonatingAttack resonatingAttack = null)
        {
            if (source == null || range <= 0f)
            {
                return null;
            }

            Collider nearestEnemy = null;
            Collider nearestReverberation = null;
            var enemyDistance = range * range;
            var reverberationDistance = enemyDistance;
            var attackLayer = attackVolume != null ? attackVolume.gameObject.layer : source.gameObject.layer;
            var radius = 0f;
            if (attackVolume is SphereCollider sphere)
            {
                var scale = sphere.transform.lossyScale;
                radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            }

            // a ground field can overlap the shot on screen even when its old sphere is out of range in 3d.
            var fields = Reverberation.Active;
            var camera = resonatingAttack != null && fields.Count > 0 ? Camera.main : null;
            for (var i = 0; resonatingAttack != null && i < fields.Count; i++)
            {
                var field = fields[i];
                var candidate = field.Volume;
                if (!field.IsAvailable || field.Creator == source || !resonatingAttack.Accepts(field.Effect)
                    || (collisionMask & (1 << candidate.gameObject.layer)) == 0
                    || !CanCollide(candidate, attackLayer, attackVolume)) continue;

                var point = GetProjectedAimPoint(field, origin, camera);
                var distance = (point - origin).sqrMagnitude;
                if (distance > reverberationDistance
                    || !HasClearPath(origin, point, candidate, radius, collisionMask, attackLayer, attackVolume))
                    continue;

                nearestReverberation = candidate;
                reverberationDistance = distance;
            }
            if (nearestReverberation != null) return nearestReverberation;

            foreach (var candidate in Physics.OverlapSphere(origin, range, collisionMask, QueryTriggerInteraction.Collide))
            {
                if (!CanCollide(candidate, attackLayer, attackVolume)
                    || candidate.GetComponentInParent<Player>() != null || candidate.TryGetComponent<Reverberation>(out _))
                {
                    continue;
                }

                var target = candidate.GetComponentInParent<IHittable>();
                var boss = candidate.GetComponentInParent<BaseBoss>();
                if ((target == null && boss == null) || (candidate.isTrigger && boss == null)
                    || (boss != null && !boss.isActiveAndEnabled)
                    || (target is Behaviour receiver && !receiver.isActiveAndEnabled))
                {
                    continue;
                }

                var point = GetAimPoint(candidate, origin);
                var distance = (point - origin).sqrMagnitude;
                if (distance > enemyDistance
                    || !HasClearPath(origin, point, candidate, radius, collisionMask, attackLayer, attackVolume))
                {
                    continue;
                }
                nearestEnemy = candidate;
                enemyDistance = distance;
            }
            return nearestEnemy;
        }

        internal static Vector3 GetAimPoint(Collider target, Vector3 origin)
        {
            if (target.TryGetComponent<Reverberation>(out var field))
                return GetProjectedAimPoint(field, origin, Camera.main);

            var bounds = target.bounds;
            var boss = target.isTrigger ? target.GetComponentInParent<BaseBoss>() : null;
            if (boss != null)
            {
                var visual = boss.GetComponentInChildren<Renderer>();
                if (visual == null)
                {
                    return boss.transform.position;
                }
                bounds = visual.bounds;
            }
            var point = bounds.center;
            var inset = Mathf.Min(0.05f, bounds.extents.y * 0.5f);
            point.y = Mathf.Clamp(origin.y, bounds.min.y + inset, bounds.max.y - inset);
            return boss != null ? point : target.ClosestPoint(point);
        }

        private static Vector3 GetProjectedAimPoint(Reverberation field, Vector3 origin, Camera camera)
        {
            var point = field.GroundAimPoint;
            if (camera != null && camera.orthographic && Mathf.Abs(camera.transform.forward.y) > 0.0001f)
            {
                var forward = camera.transform.forward;
                return point + forward * ((origin.y - point.y) / forward.y);
            }
            point.y = origin.y;
            return point;
        }

        private static bool CanCollide(Collider other, int attackLayer, Collider attackVolume)
        {
            if (other == null || !other.enabled || !other.gameObject.activeInHierarchy
                || Physics.GetIgnoreLayerCollision(attackLayer, other.gameObject.layer))
            {
                return false;
            }
            if (attackVolume == null)
            {
                return true;
            }
            return !other.transform.IsChildOf(attackVolume.transform)
                && (!attackVolume.gameObject.scene.IsValid() || !Physics.GetIgnoreCollision(attackVolume, other));
        }

        private static bool HasClearPath(Vector3 origin, Vector3 point, Collider target, float radius,
            int collisionMask, int attackLayer, Collider attackVolume)
        {
            foreach (var overlap in Physics.OverlapSphere(origin, Mathf.Max(0.001f, radius),
                collisionMask, QueryTriggerInteraction.Collide))
            {
                if (IsBlocking(overlap, target, attackLayer, attackVolume))
                {
                    return false;
                }
            }
            var offset = point - origin;
            var distance = offset.magnitude;
            if (distance < 0.0001f)
            {
                return true;
            }
            var hits = radius > 0f
                ? Physics.SphereCastAll(origin, radius, offset / distance, distance, collisionMask, QueryTriggerInteraction.Collide)
                : Physics.RaycastAll(origin, offset / distance, distance, collisionMask, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                if (IsBlocking(hit.collider, target, attackLayer, attackVolume))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsBlocking(Collider obstacle, Collider target, int attackLayer, Collider attackVolume)
        {
            if (!CanCollide(obstacle, attackLayer, attackVolume) || obstacle.isTrigger || obstacle == target
                || obstacle.GetComponentInParent<Player>() != null)
            {
                return false;
            }
            var boss = target.GetComponentInParent<BaseBoss>();
            if (boss != null && obstacle.GetComponentInParent<BaseBoss>() == boss)
            {
                return false;
            }
            var receiver = target.GetComponentInParent<IHittable>();
            return receiver == null || !ReferenceEquals(receiver, obstacle.GetComponentInParent<IHittable>());
        }
    }
}
