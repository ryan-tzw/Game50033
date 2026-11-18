namespace Game50033
{
    using UnityEngine;

    // presentation only: slanted shockwaves that sweep forward through the Crash triangle and spread
    // to its sides. Damage and resonance are resolved once by CymbalsAbilities; these waves cannot hit.
    [DisallowMultipleComponent]
    public sealed class CymbalsCrashWaves : MonoBehaviour
    {
        [SerializeField] private CymbalsTuning tuning;

        private LineRenderer[] _waves = System.Array.Empty<LineRenderer>();
        private Vector3 _tip;
        private Vector3 _direction = Vector3.forward;
        private float _startedAt = float.NegativeInfinity;

        private void OnEnable()
        {
            if (tuning == null)
            {
                Debug.LogError("CymbalsCrashWaves requires a CymbalsTuning asset.", this);
                enabled = false;
            }
        }

        public void Play(Vector3 tip, Vector3 direction)
        {
            EnsureWaves();
            _tip = tip + Vector3.up * 0.05f;
            _direction = direction;
            _startedAt = Time.time;
        }

        private void EnsureWaves()
        {
            var settings = tuning.crash;
            if (_waves.Length == settings.waveCount) return;
            foreach (var wave in _waves)
                if (wave != null) Destroy(wave.gameObject);

            // world-space children on the pixelated effects layer; they never move with the player.
            var layer = LayerMask.NameToLayer("PixelWorldVFX");
            _waves = new LineRenderer[settings.waveCount];
            for (var i = 0; i < _waves.Length; i++)
            {
                var go = new GameObject($"Crash wave {i + 1}") { layer = layer < 0 ? 0 : layer };
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 3;
                line.alignment = LineAlignment.View;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.numCornerVertices = 2;
                line.enabled = false;
                _waves[i] = line;
            }
        }

        private void LateUpdate()
        {
            if (_waves.Length == 0) return;
            var settings = tuning.crash;
            var side = Vector3.Cross(Vector3.up, _direction);
            var elapsed = Time.time - _startedAt;

            for (var i = 0; i < _waves.Length; i++)
            {
                var wave = _waves[i];
                if (wave == null) continue;
                var t = (elapsed - i * settings.waveStagger) / settings.waveDuration;
                if (t < 0f || t > 1f)
                {
                    wave.enabled = false;
                    continue;
                }

                // a forward-pointing chevron: its ends ride the triangle's edges, its centre leads by the slant.
                var depth = t * settings.length;
                var half = settings.width * 0.5f * t;
                var ends = _tip + _direction * depth;
                wave.enabled = true;
                if (settings.waveMaterial != null) wave.sharedMaterial = settings.waveMaterial;
                wave.widthMultiplier = settings.waveWidth;
                var color = settings.waveColor;
                color.a *= 1f - t * t;
                wave.startColor = wave.endColor = color;
                wave.SetPosition(0, ends - side * half);
                wave.SetPosition(1, _tip + _direction * (depth + settings.waveSlant));
                wave.SetPosition(2, ends + side * half);
            }
        }

        private void OnDisable()
        {
            _startedAt = float.NegativeInfinity;
            foreach (var wave in _waves)
                if (wave != null) wave.enabled = false;
        }
    }
}
