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

            foreach (var candidate in Physics.OverlapSphere(origin, range, collisionMask, QueryTriggerInteraction.Collide))
            {
                if (!CanCollide(candidate, attackLayer, attackVolume)
                    || candidate.GetComponentInParent<Player>() != null)
                {
                    continue;
                }

                var isReverberation = candidate.TryGetComponent(out Reverberation reverberation);
                if (isReverberation)
                {
                    if (resonatingAttack == null || !reverberation.IsAvailable || reverberation.Creator == source
                        || !resonatingAttack.Accepts(reverberation.Effect))
                    {
                        continue;
                    }
                }
                else
                {
                    var target = candidate.GetComponentInParent<IHittable>();
                    var boss = candidate.GetComponentInParent<BaseBoss>();
                    if ((target == null && boss == null) || (candidate.isTrigger && boss == null)
                        || (boss != null && !boss.isActiveAndEnabled)
                        || (target is Behaviour receiver && !receiver.isActiveAndEnabled))
                    {
                        continue;
                    }
                }

                var point = GetAimPoint(candidate, origin);
                var distance = (point - origin).sqrMagnitude;
                if (distance > (isReverberation ? reverberationDistance : enemyDistance)
                    || !HasClearPath(origin, point, candidate, radius, collisionMask, attackLayer, attackVolume))
                {
                    continue;
                }
                if (isReverberation)
                {
                    nearestReverberation = candidate;
                    reverberationDistance = distance;
                }
                else
                {
                    nearestEnemy = candidate;
                    enemyDistance = distance;
                }
            }
            return nearestReverberation != null ? nearestReverberation : nearestEnemy;
        }

        internal static Vector3 GetAimPoint(Collider target, Vector3 origin)
        {
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
