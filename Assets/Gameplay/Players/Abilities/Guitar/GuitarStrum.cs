namespace Game50033
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class GuitarStrum : MonoBehaviour
    {
        private struct Contact
        {
            public Collider collider;
            public float fraction;
            public Vector3 point;
        }

        private readonly HashSet<IHittable> _hitTargets = new();
        private readonly List<Contact> _contacts = new();
        private RaycastHit[] _castBuffer = new RaycastHit[16];
        private Collider[] _overlapBuffer = new Collider[16];
        private GameObject _attacker;
        private GuitarVFX _impacts;
        private Vector3 _direction;
        private Vector3 _stretchAxis;
        private float _stretch, _radius, _width, _angle, _thickness;
        private float _range, _damage, _travelled, _baseRadius, _duration, _age, _tipTaper;
        private AnimationCurve _travelCurve, _growthCurve, _fadeCurve;
        private GuitarStrumVisual _visual;
        private LayerMask _mask;
        private bool _initialized;
        private bool _firstStep = true;

        public bool IsFinished { get; private set; }

        public void Initialize(Player source, Vector3 direction, GuitarTuning tuning, GuitarVFX impacts)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (tuning == null) throw new ArgumentNullException(nameof(tuning));
            if (_initialized) throw new InvalidOperationException("A Strum wave can only be initialized once.");

            var settings = tuning.secondary;
            _attacker = source.gameObject;
            _impacts = impacts;
            direction.y = 0f;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            _stretchAxis = new Vector3(tuning.attack.stretchAxis.x, 0f, tuning.attack.stretchAxis.y);
            _stretchAxis = _stretchAxis.sqrMagnitude > 0.0001f ? _stretchAxis.normalized : Vector3.forward;
            _stretch = Mathf.Max(1f, tuning.attack.depthStretch);
            _baseRadius = Mathf.Max(0.01f, settings.waveRadius);
            _duration = Mathf.Max(0.01f, settings.travelDuration);
            _travelCurve = settings.travelCurve;
            _growthCurve = settings.growthCurve;
            _fadeCurve = settings.fadeCurve;
            _radius = Mathf.Max(0.01f, _baseRadius * _growthCurve.Evaluate(0f));
            _tipTaper = Mathf.Clamp01(settings.waveTipTaper);
            _width = Mathf.Clamp(settings.waveWidth, 0.01f, _radius * 2f);
            _angle = Mathf.Clamp(settings.waveAngle, 1f, 180f);
            _thickness = Mathf.Max(0.01f, settings.hitThickness);
            _range = Mathf.Max(0.01f, settings.range);
            _damage = Mathf.Max(0f, settings.damage);
            _mask = settings.collisionMask;
            _initialized = true;

            // the supplied spawn point is the front tip, so close targets are not skipped by a hollow arc.
            var apex = _direction + _stretchAxis * (Vector3.Dot(_direction, _stretchAxis) * (_stretch - 1f));
            transform.position -= apex * _radius;
            _visual = GetComponent<GuitarStrumVisual>();
            if (_visual != null && _visual.isActiveAndEnabled)
            {
                _visual.Initialize(_direction, _width, _angle, _stretchAxis, _stretch, _tipTaper, tuning.vfx);
                _visual.SetWaveState(_radius, Mathf.Clamp01(_fadeCurve.Evaluate(0f)), 0f);
            }
        }

        private void Update()
        {
            if (!_initialized || IsFinished) return;

            var nextAge = Mathf.Min(_age + Time.deltaTime, _duration);
            var progress = nextAge / _duration;
            var curveStart = _travelCurve.Evaluate(0f);
            var curveSpan = _travelCurve.Evaluate(1f) - curveStart;
            var travelFraction = Mathf.Abs(curveSpan) > 0.0001f
                ? (_travelCurve.Evaluate(progress) - curveStart) / curveSpan : progress;
            var nextTravel = Mathf.Max(_travelled, _range * Mathf.Clamp01(travelFraction));
            var step = nextTravel - _travelled;
            var nextRadius = Mathf.Max(0.01f, _baseRadius * _growthCurve.Evaluate(progress));
            var origin = transform.position;
            var samples = Mathf.Max(2, Mathf.CeilToInt(_angle / 4f),
                Mathf.CeilToInt(_angle * Mathf.Deg2Rad * (Mathf.Max(_radius, nextRadius) + _width * 0.5f)
                    * _stretch / (_thickness * 2f)));
            _contacts.Clear();

            // each spoke moves by both forward travel and radial growth. sweep that combined motion
            // so expansion cannot skip targets or thin walls between rendered frames.
            for (var sample = 0; sample <= samples; sample++)
            {
                var t = (float)sample / samples;
                var radial = Quaternion.AngleAxis(Mathf.Lerp(-_angle * 0.5f, _angle * 0.5f, t), Vector3.up) * _direction;
                radial += _stretchAxis * (Vector3.Dot(radial, _stretchAxis) * (_stretch - 1f));
                var width = _width * Mathf.Lerp(1f - _tipTaper, 1f, Mathf.Sin(t * Mathf.PI));
                var inside = origin + radial * Mathf.Max(0f, _radius - width * 0.5f);
                var outside = origin + radial * (_radius + width * 0.5f);
                var motion = _direction * step + radial * (nextRadius - _radius);
                var distance = motion.magnitude;
                if (_firstStep)
                {
                    int count;
                    while (true)
                    {
                        count = Physics.OverlapCapsuleNonAlloc(inside, outside, _thickness,
                            _overlapBuffer, _mask, QueryTriggerInteraction.Collide);
                        if (count < _overlapBuffer.Length) break;
                        Array.Resize(ref _overlapBuffer, _overlapBuffer.Length * 2);
                    }
                    for (var i = 0; i < count; i++)
                        _contacts.Add(new Contact { collider = _overlapBuffer[i], fraction = 0f,
                            point = _overlapBuffer[i].ClosestPoint((inside + outside) * 0.5f) });
                }
                if (distance <= 0.000001f) continue;

                int hits;
                while (true)
                {
                    hits = Physics.CapsuleCastNonAlloc(inside, outside, _thickness, motion / distance,
                        _castBuffer, distance, _mask, QueryTriggerInteraction.Collide);
                    if (hits < _castBuffer.Length) break;
                    Array.Resize(ref _castBuffer, _castBuffer.Length * 2);
                }
                for (var i = 0; i < hits; i++)
                {
                    var hit = _castBuffer[i];
                    // initial overlaps can report a zero contact point; use the collider surface instead.
                    var point = hit.distance <= 0f ? hit.collider.ClosestPoint((inside + outside) * 0.5f) : hit.point;
                    _contacts.Add(new Contact { collider = hit.collider, fraction = hit.distance / distance, point = point });
                }
            }
            _firstStep = false;

            var fraction = 1f;
            var blocked = false;
            // find the closest wall across all spokes before resolving enemies, regardless of query order.
            foreach (var contact in _contacts)
            {
                var collider = contact.collider;
                if (collider == null || collider.isTrigger || collider.GetComponentInParent<Player>() != null
                    || collider.GetComponentInParent<IHittable>() != null) continue;
                fraction = Mathf.Min(fraction, contact.fraction);
                blocked = true;
            }
            foreach (var contact in _contacts)
            {
                var collider = contact.collider;
                if (collider == null || contact.fraction > fraction + 0.0001f
                    || collider.GetComponentInParent<Player>() != null) continue;
                var target = collider.GetComponentInParent<IHittable>();
                if (target == null || _hitTargets.Contains(target)
                    || target is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;

                _hitTargets.Add(target);
                target.TakeHit(new HitInfo(_attacker, _damage, _direction));
                if (!isActiveAndEnabled || IsFinished) return;
                if (_impacts != null) _impacts.PlayImpact(contact.point, false);
            }

            transform.position += _direction * (step * fraction);
            _travelled += step * fraction;
            _radius = Mathf.Lerp(_radius, nextRadius, fraction);
            _age = Mathf.Lerp(_age, nextAge, fraction);
            var opacity = Mathf.Clamp01(_fadeCurve.Evaluate(_age / _duration));
            if (_visual != null && _visual.isActiveAndEnabled) _visual.SetWaveState(_radius, opacity, _age);
            if (!blocked && _age < _duration && opacity > 0.001f) return;
            IsFinished = true;
            Destroy(gameObject);
        }
    }
}
