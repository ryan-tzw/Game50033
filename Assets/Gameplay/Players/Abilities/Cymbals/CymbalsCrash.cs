namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;

    // encore repeats immediately while the original slam's prism is still active.
    internal sealed class CymbalsCrash : IAttackReplay
    {
        private readonly Player _owner;
        private readonly MeshCollider _volume;
        private readonly Vector3 _tip, _direction, _left, _right;
        private readonly Quaternion _rotation;
        private readonly float _length, _width, _height, _damage;
        private readonly LayerMask _hitMask;
        private readonly HitEffect _hitEffect;

        internal CymbalsCrash(Player owner, MeshCollider volume, Vector3 tip, Vector3 direction,
            Quaternion rotation, CymbalsTuning.CrashSettings settings, CymbalsTuning.ComboSettings combo)
        {
            _owner = owner;
            _volume = volume;
            _tip = tip;
            _direction = direction;
            _rotation = rotation;
            _length = settings.length;
            _width = settings.width;
            _height = combo.hitColumnHeight;
            _damage = settings.damage;
            _hitMask = combo.hitMask;
            _hitEffect = combo.hitEffect;
            settings.GetTriangle(tip, direction, out _left, out _right);
        }

        public void Replay(float damageMultiplier)
        {
            if (_owner == null || _volume == null || !_volume.enabled) return;
            if (_owner.Session?.Debug is { ShowHitboxes: true, ShowReverberationAreas: true } debug)
            {
                // replay places the existing collider at the stored impact pose.
                var scale = _volume.transform.lossyScale;
                var left = _tip + _rotation * Vector3.Scale(new Vector3(-_width * 0.5f, 0f, _length), scale);
                var right = _tip + _rotation * Vector3.Scale(new Vector3(_width * 0.5f, 0f, _length), scale);
                var height = _rotation * Vector3.Scale(Vector3.up * _height, scale);
                debug.RecordAttack(DebugAttackSnapshot.Triangle(_tip, left, right, height, reverberation: true));
            }
            var colliders = new Collider[64];
            Resolve(damageMultiplier, colliders, Collect(colliders), new HashSet<IHittable>());
        }

        internal int Collect(Collider[] colliders)
        {
            var center = _tip + _direction * (_length * 0.5f) + Vector3.up * (_height * 0.5f);
            var extents = new Vector3(_width * 0.5f, _height * 0.5f, _length * 0.5f);
            return Physics.OverlapBoxNonAlloc(center, extents, colliders, _rotation, Physics.AllLayers, QueryTriggerInteraction.Collide);
        }

        internal void Resolve(float damageMultiplier, Collider[] colliders, int count, HashSet<IHittable> hitTargets,
            CymbalsAbilities live = null)
        {
            var liveImpact = live != null;
            var damage = _damage * damageMultiplier;
            for (var i = 0; i < count; i++)
            {
                var candidate = colliders[i];
                if (candidate == null || candidate.isTrigger || !candidate.enabled
                    || (_hitMask.value & (1 << candidate.gameObject.layer)) == 0
                    || candidate.GetComponentInParent<Player>() != null) continue;
                var target = candidate.GetComponentInParent<IHittable>();
                if (target == null || hitTargets.Contains(target)
                    || target is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;

                // non-convex meshes retain the ground triangle's centre-point fallback.
                var nonConvex = candidate is MeshCollider { convex: false };
                var overlaps = nonConvex ? Contains(candidate.bounds.center)
                    : Physics.ComputePenetration(_volume, _tip, _rotation,
                        candidate, candidate.transform.position, candidate.transform.rotation, out _, out _);
                if (!overlaps) continue;

                hitTargets.Add(target);
                target.TakeHit(HitInfo.FromResonatingAttack(_owner.gameObject, damage, _direction, _damage,
                    isReplay: !liveImpact));
                if (_owner == null || liveImpact && (live == null || !live.isActiveAndEnabled || _owner.Health.IsDowned)) return;
                if (_hitEffect != null)
                {
                    var point = _tip + _direction * (_length * 0.4f);
                    var contact = nonConvex ? candidate.bounds.ClosestPoint(point) : candidate.ClosestPoint(point);
                    _hitEffect.Play(new HitEffectRequest(contact, _direction, damage, true));
                }
            }
        }

        private bool Contains(Vector3 point)
        {
            static float Side(Vector3 p, Vector3 from, Vector3 to) => (to.x - from.x) * (p.z - from.z) - (to.z - from.z) * (p.x - from.x);
            var a = Side(point, _tip, _left);
            var b = Side(point, _left, _right);
            var c = Side(point, _right, _tip);
            return !((a < 0f || b < 0f || c < 0f) && (a > 0f || b > 0f || c > 0f));
        }
    }
}
