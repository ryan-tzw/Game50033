namespace Game50033
{
    using UnityEngine;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(TromboneProjectile))]
    public sealed class TromboneProjectileVisual : MonoBehaviour
    {
        [SerializeField] private TromboneTuning tuning;
        [SerializeField] private bool special;
        [SerializeField] private LineRenderer core;
        [SerializeField] private LineRenderer halo;
        [SerializeField] private TrailRenderer trail;

        private ResonatingAttack _resonating;
        private TromboneProjectile _projectile;
        private Vector3 _origin;

        private void Awake()
        {
            _resonating = GetComponent<ResonatingAttack>();
            _projectile = GetComponent<TromboneProjectile>();
        }

        private void OnEnable()
        {
            var settings = tuning.presentation;
            _origin = transform.position;
            core.sharedMaterial = halo.sharedMaterial = trail.sharedMaterial = settings.effectMaterial;
            trail.time = special ? settings.specialTrailTime : settings.primaryTrailTime;
            trail.minVertexDistance = 0.015f;
            trail.startWidth = special ? settings.specialShotWidth : settings.primaryShotWidth;
            trail.endWidth = 0f;
            trail.emitting = true;
            trail.Clear();
            Draw();
        }

        private void LateUpdate() => Draw();

        private void Draw()
        {
            var settings = tuning.presentation;
            var color = !special && _resonating.enabled ? settings.resonatingColor : settings.gold;
            var width = special ? settings.specialShotWidth : settings.primaryShotWidth;

            width *= 1f + (special ? 0.8f : 0.5f) * _projectile.ShotPressureNormalized;

            var distance = Vector3.Distance(_origin, transform.position);
            var length = Mathf.Min(distance, special ? settings.specialShotLength : settings.primaryShotLength);

            DrawLine(core, length, width, Color.Lerp(color, Color.white, 0.85f));
            color.a *= special ? 0.5f : 0.35f;
            DrawLine(halo, length, width * 2.4f, color);
            color.a = 0.8f;
            trail.startColor = color;
            color.a = 0f;
            trail.endColor = color;
        }

        private void DrawLine(LineRenderer line, float length, float width, Color color)
        {
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, transform.position - transform.forward * length);
            line.SetPosition(1, transform.position);
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
        }

        private void OnDisable()
        {
            trail.emitting = false;
            trail.Clear();
        }
    }
}
