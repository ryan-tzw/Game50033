namespace Game50033
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    internal sealed class GuitarSmash : IAttackReplay
    {
        private readonly Player _owner;
        private readonly Vector3 _centre;
        private readonly Vector3 _direction;
        private readonly float _radius;
        private readonly float _sweetSpotRadius;
        private readonly float _hitHeight;
        private readonly LayerMask _hitMask;
        private readonly float _damage;
        private readonly float _sweetSpotMultiplier;
        private readonly float _hitstopDuration;
        private readonly float _sweetSpotHitstopDuration;
        private readonly HitEffect _hitEffect;
        private readonly GuitarVFX _vfx;

        internal GuitarSmash(Player owner, Vector3 centre, Vector3 direction, float radius, float sweetSpotRadius,
            float damageMultiplier, GuitarTuning tuning, GuitarVFX vfx)
        {
            var special = tuning.special;
            _owner = owner;
            _centre = centre;
            _direction = direction;
            _radius = radius;
            _sweetSpotRadius = sweetSpotRadius;
            _hitHeight = special.hitHeight;
            _hitMask = special.hitMask;
            _damage = special.damage * damageMultiplier;
            _sweetSpotMultiplier = special.sweetSpotMultiplier;
            _hitstopDuration = special.hitstopDuration;
            _sweetSpotHitstopDuration = special.sweetSpotHitstopDuration;
            _hitEffect = tuning.vfx.hitEffect;
            _vfx = vfx;
        }

        public void Replay(float damageMultiplier)
        {
            if (_owner == null) return;
            var colliders = new Collider[32];
            Resolve(damageMultiplier, new HashSet<IHittable>(), new List<CombatHurtbox>(), ref colliders);
        }

        internal float Resolve(float damageMultiplier, HashSet<IHittable> hitTargets,
            List<CombatHurtbox> hurtboxes, ref Collider[] colliders, GuitarAbilities live = null)
        {
            var hitstop = 0f;
            var liveImpact = live != null;
            var camera = Camera.main;
            hurtboxes.Clear();
            foreach (var hurtbox in CombatHurtbox.Active) hurtboxes.Add(hurtbox);
            foreach (var hurtbox in hurtboxes)
            {
                if (hurtbox == null || !hurtbox.isActiveAndEnabled
                    || (_hitMask.value & (1 << hurtbox.gameObject.layer)) == 0
                    || hurtbox.GetComponentInParent<Player>() != null) continue;
                var target = hurtbox.Target;
                if (target == null || hitTargets.Contains(target)
                    || target is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
                if (!hurtbox.TryOverlapSector(camera, _centre, _direction, 0f, _radius,
                        360f, Vector3.forward, 1f, out var contact)) continue;

                if (!ApplyHit(target, contact, hurtbox.WorldCenter)) return 0f;
            }

            // targets without an authored projected hurtbox still use their physics colliders.
            int count;
            var halfHeight = Vector3.up * Mathf.Max(0f, _hitHeight) * 0.5f;
            while (true)
            {
                count = Physics.OverlapCapsuleNonAlloc(_centre - halfHeight, _centre + halfHeight, _radius,
                    colliders, _hitMask, QueryTriggerInteraction.Collide);
                if (count < colliders.Length) break;
                Array.Resize(ref colliders, colliders.Length * 2);
            }

            for (var i = 0; i < count; i++)
            {
                var candidate = colliders[i];
                if (candidate == null || candidate.GetComponentInParent<Player>() != null
                    || candidate.GetComponentInParent<CombatHurtbox>() != null) continue;
                var target = candidate.GetComponentInParent<IHittable>();
                if (target == null || hitTargets.Contains(target)
                    || target is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
                var contact = candidate.ClosestPoint(_centre);
                var near = contact - _centre;
                near.y = 0f;
                if (near.sqrMagnitude > _radius * _radius + 0.000001f) continue;

                if (!ApplyHit(target, contact, candidate.bounds.center)) return 0f;
            }
            return hitstop;

            bool ApplyHit(IHittable target, Vector3 contact, Vector3 targetCentre)
            {
                // the inner sweet spot uses the target centre on XZ, independent of camera height.
                var offset = targetCentre - _centre;
                offset.y = 0f;
                var sweet = offset.sqrMagnitude <= _sweetSpotRadius * _sweetSpotRadius + 0.000001f;
                var direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : _direction;
                var dealt = _damage * damageMultiplier * (sweet ? _sweetSpotMultiplier : 1f);
                hitTargets.Add(target);
                target.TakeHit(new HitInfo(_owner.gameObject, dealt, direction));
                if (_hitEffect != null) _hitEffect.Play(new HitEffectRequest(contact, direction, dealt, true));
                if (liveImpact && (live == null || !live.IsResolvingSmash)) return false;

                if (liveImpact) hitstop = Mathf.Max(hitstop, sweet ? _sweetSpotHitstopDuration : _hitstopDuration);
                if (_vfx != null) _vfx.PlayImpact(contact, sweet);
                return _owner != null;
            }
        }
    }
}
