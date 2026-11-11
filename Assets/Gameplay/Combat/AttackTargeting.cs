namespace Game50033
{
    using UnityEngine;
    // Logic for this right now (to revisit later): 
    // 1. Find all colliders within attack range (enemy + rev)
    // 2. Filter invalid targets and blocked paths based 
    // 3. Choose nearest valid REV, otherwise nearest valid enemy. ( a bit iffy rn ngl)
    public static class AttackTargeting
    {
        public static Collider FindTarget(
            Player source,
            Vector3 origin,
            float range,
            int enemyMask,
            int collisionMask,
            Collider attackVolume = null,
            ResonatingAttack resonatingAttack = null)
        {
            if (source == null || range <= 0f)
            {
                return null;
            }

            Collider nearestEnemy = null;
            Collider nearestReverberation = null;
            var nearestEnemyDistanceSquared = range * range;
            var nearestReverberationDistanceSquared = nearestEnemyDistanceSquared;
            var attackLayer = attackVolume != null ? attackVolume.gameObject.layer : source.gameObject.layer;
            var radius = 0f;

            if (attackVolume is SphereCollider sphere)
            {
                var scale = sphere.transform.lossyScale;
                radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            }

            foreach (var candidate in Physics.OverlapSphere(
                origin,
                range,
                collisionMask,
                QueryTriggerInteraction.Collide))
            {
                if (!CanCollideWith(candidate, attackLayer, attackVolume))
                {
                    continue;
                }

                if (candidate.GetComponentInParent<Player>() != null)
                {
                    continue;
                }

                var isReverberation = candidate.TryGetComponent(out Reverberation reverberation);
                if (isReverberation)
                {
                    if (!IsValidReverberationTarget(reverberation, source, resonatingAttack))
                    {
                        continue;
                    }
                }
                else if (!IsValidEnemyTarget(candidate, enemyMask))
                {
                    continue;
                }

                var aimPoint = GetAimPoint(candidate, origin);
                var distanceSquared = (aimPoint - origin).sqrMagnitude;
                var nearestDistanceSquared = isReverberation
                    ? nearestReverberationDistanceSquared
                    : nearestEnemyDistanceSquared;

                if (distanceSquared > nearestDistanceSquared)
                {
                    continue;
                }

                if (!HasClearPath(
                    origin,
                    aimPoint,
                    candidate,
                    radius,
                    collisionMask,
                    attackLayer,
                    attackVolume))
                {
                    continue;
                }

                if (isReverberation)
                {
                    nearestReverberation = candidate;
                    nearestReverberationDistanceSquared = distanceSquared;
                }
                else
                {
                    nearestEnemy = candidate;
                    nearestEnemyDistanceSquared = distanceSquared;
                }
            }

            // Prioritize compatible teammate REV over enemies, even if an enemy is closer.
            // TODO: figure out if this implementation is okay
            return nearestReverberation != null ? nearestReverberation : nearestEnemy;
        }

        private static bool IsValidReverberationTarget(
            Reverberation reverberation,
            Player source,
            ResonatingAttack resonatingAttack)
        {
            if (resonatingAttack == null || !reverberation.IsAvailable)
            {
                return false;
            }

            if (reverberation.Creator == source)
            {
                return false;
            }

            return resonatingAttack.Accepts(reverberation.Effect);
        }

        private static bool IsValidEnemyTarget(Collider candidate, int enemyMask)
        {
            if ((enemyMask & (1 << candidate.gameObject.layer)) == 0)
            {
                return false;
            }

            var target = candidate.GetComponentInParent<IHittable>();
            var boss = candidate.GetComponentInParent<BaseBoss>();

            if (boss != null && !boss.isActiveAndEnabled)
            {
                return false;
            }

            if (candidate.isTrigger && boss == null)
            {
                return false;
            }

            if (target == null && boss == null)
            {
                return false;
            }

            if (target is Behaviour behaviour)
            {
                return behaviour.isActiveAndEnabled;
            }

            return true;
        }

        public static Vector3 GetAimPoint(Collider target, Vector3 origin)
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

            // Aim near the shot's height to avoid passing above or below the target.
            point.y = Mathf.Clamp(origin.y, bounds.min.y + inset, bounds.max.y - inset);
            return boss != null ? point : target.ClosestPoint(point);
        }

        private static bool CanCollideWith(Collider other, int attackLayer, Collider attackVolume)
        {
            if (other == null || !other.enabled || !other.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (Physics.GetIgnoreLayerCollision(attackLayer, other.gameObject.layer))
            {
                return false;
            }

            if (attackVolume == null)
            {
                return true;
            }

            if (other.transform.IsChildOf(attackVolume.transform))
            {
                return false;
            }

            return !attackVolume.gameObject.scene.IsValid() || !Physics.GetIgnoreCollision(attackVolume, other);
        }

        private static bool HasClearPath(
            Vector3 origin,
            Vector3 point,
            Collider target,
            float radius,
            int collisionMask,
            int attackLayer,
            Collider attackVolume)
        {
            foreach (var overlap in Physics.OverlapSphere(
                origin,
                Mathf.Max(0.001f, radius),
                collisionMask,
                QueryTriggerInteraction.Collide))
            {
                if (IsBlockingCollider(overlap, target, attackLayer, attackVolume))
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

            RaycastHit[] hits;
            if (radius > 0f)
            {
                hits = Physics.SphereCastAll(
                    origin,
                    radius,
                    offset / distance,
                    distance,
                    collisionMask,
                    QueryTriggerInteraction.Collide);
            }
            else
            {
                hits = Physics.RaycastAll(
                    origin,
                    offset / distance,
                    distance,
                    collisionMask,
                    QueryTriggerInteraction.Collide);
            }

            foreach (var hit in hits)
            {
                if (IsBlockingCollider(hit.collider, target, attackLayer, attackVolume))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsBlockingCollider(
            Collider obstacle,
            Collider target,
            int attackLayer,
            Collider attackVolume)
        {
            if (!CanCollideWith(obstacle, attackLayer, attackVolume))
            {
                return false;
            }

            if (obstacle.isTrigger || obstacle == target)
            {
                return false;
            }

            if (obstacle.GetComponentInParent<Player>() != null)
            {
                return false;
            }

            var targetEntity = target.GetComponentInParent<IHittable>();
            var targetBoss = target.GetComponentInParent<BaseBoss>();
            if (targetBoss != null && obstacle.GetComponentInParent<BaseBoss>() == targetBoss)
            {
                return false;
            }

            return targetEntity == null || !ReferenceEquals(targetEntity, obstacle.GetComponentInParent<IHittable>());
        }
    }
}
