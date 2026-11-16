namespace Game50033
{
    using UnityEngine;

    // presentation only: one arc along the far edge of the parry cone, marking the line of parry.
    // It glows and dims while the parry is up and flashes when something is parried.
    // Projectiles are found and destroyed by CymbalsAbilities; this line cannot parry anything.
    [DisallowMultipleComponent]
    public sealed class CymbalsParryArcs : MonoBehaviour
    {
        private const float FadeOutTime = 0.12f;

        [SerializeField] private CymbalsTuning tuning;

        private LineRenderer _arc;
        private Vector3 _direction = Vector3.forward;
        private float _startedAt = float.NegativeInfinity;
        private float _endedAt = float.NegativeInfinity;
        private float _flashAt = float.NegativeInfinity;
        private bool _active;

        private void OnEnable()
        {
            if (tuning == null)
            {
                Debug.LogError("CymbalsParryArcs requires a CymbalsTuning asset.", this);
                enabled = false;
            }
        }

        public void Begin()
        {
            if (!enabled) return;
            EnsureArc();
            _startedAt = Time.time;
            _endedAt = float.PositiveInfinity;
            _active = true;
        }

        public void SetDirection(Vector3 direction) => _direction = direction;

        public void Flash() => _flashAt = Time.time;

        // the line fades out quickly instead of vanishing.
        public void End()
        {
            if (_active) _endedAt = Time.time;
            _active = false;
        }

        private void EnsureArc()
        {
            if (_arc != null) return;
            var layer = LayerMask.NameToLayer("PixelWorldVFX");
            var go = new GameObject("Parry line") { layer = layer < 0 ? 0 : layer };
            go.transform.SetParent(transform, false);
            _arc = go.AddComponent<LineRenderer>();
            _arc.useWorldSpace = true;
            _arc.alignment = LineAlignment.View;
            _arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _arc.receiveShadows = false;
            _arc.numCapVertices = 2;
            _arc.enabled = false;
        }

        private void LateUpdate()
        {
            if (_arc == null || float.IsNegativeInfinity(_startedAt)) return;
            var parry = tuning.parry;
            var now = Time.time;

            // fade in with the cymbals coming forward, fade out after the parry ends.
            var fadeIn = Mathf.Clamp01((now - _startedAt) / parry.raiseTime);
            var fadeOut = _active ? 1f : 1f - (now - _endedAt) / FadeOutTime;
            if (fadeOut <= 0f)
            {
                _arc.enabled = false;
                return;
            }

            // glow: a smooth cycle between dimAlpha and full brightness; a parry flashes it to full and wider.
            var glow = 0.5f - 0.5f * Mathf.Cos((now - _startedAt) * parry.glowRate * Mathf.PI * 2f);
            var flash = Mathf.Clamp01(1f - (now - _flashAt) / parry.parryPopTime);
            var brightness = Mathf.Max(Mathf.Lerp(parry.dimAlpha, 1f, glow), flash);
            var width = Mathf.Lerp(1f, parry.glowWidth, Mathf.Max(glow, flash)) * (1f + flash * 0.5f);

            _arc.enabled = true;
            if (parry.arcMaterial != null) _arc.sharedMaterial = parry.arcMaterial;
            _arc.widthMultiplier = parry.arcWidth * width;
            var color = parry.arcColor;
            color.a *= brightness * fadeIn * Mathf.Clamp01(fadeOut);
            _arc.startColor = _arc.endColor = color;

            var points = parry.arcSegments + 1;
            if (_arc.positionCount != points) _arc.positionCount = points;
            var center = transform.position + Vector3.up * tuning.combo.hitHeight;
            var halfAngle = parry.coneAngle * 0.5f;
            for (var i = 0; i < points; i++)
            {
                var angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)(points - 1));
                _arc.SetPosition(i, center + Quaternion.AngleAxis(angle, Vector3.up) * _direction * parry.range);
            }
        }

        private void OnDisable()
        {
            _active = false;
            _startedAt = _endedAt = float.NegativeInfinity;
            if (_arc != null) _arc.enabled = false;
        }
    }
}
