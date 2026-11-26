namespace Game50033
{
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [DefaultExecutionOrder(260)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Player), typeof(TromboneAbilities))]
    public sealed class TrombonePressureVisual : MonoBehaviour
    {
        [SerializeField] private TromboneTuning tuning;
        [SerializeField] private Transform pressureRoot;
        [SerializeField] private LineRenderer capacityRing;
        [SerializeField] private LineRenderer fillRing;
        [SerializeField] private SpriteRenderer[] notes;

        private Player _player;
        private TromboneAbilities _abilities;
        private Camera _camera;
        private TromboneWeaponVisual _weapon;

        private readonly Gradient _fillGradient = new();
        private readonly GradientColorKey[] _colorKeys = new GradientColorKey[5];
        private readonly GradientAlphaKey[] _alphaKeys = new GradientAlphaKey[5];
        private Note[] _states;

        private float _displayed;
        private float _previous;
        private float _noteRate;
        private float _emission;
        private int _sequence;
        private float _maxPulseAt = float.NegativeInfinity;
        private float _gainedAt = float.NegativeInfinity;
        private bool _maxed;
        private bool _losing;
        private bool _placed;

        private struct Note
        {
            public Vector3 Origin;
            public float BornAt;
            public float Phase;
            public Color Color;
        }

        private void Awake()
        {
            _player = GetComponent<Player>();
            _abilities = GetComponent<TromboneAbilities>();
            _weapon = GetComponent<TromboneWeaponVisual>();

            _states = new Note[notes.Length];
            capacityRing.sharedMaterial = fillRing.sharedMaterial = tuning.presentation.effectMaterial;
            capacityRing.useWorldSpace = fillRing.useWorldSpace = true;
            fillRing.sortingOrder = capacityRing.sortingOrder + 1;

            foreach (var note in notes)
            {
                note.sprite = tuning.pressureVisuals.noteSprite;
                note.sharedMaterial = tuning.presentation.weaponMaterial;
            }

            Clear();
        }

        private void OnEnable() => SceneManager.activeSceneChanged += HandleSceneChanged;

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            var visible = _camera != null && _player.isActiveAndEnabled && _player.HasActiveInput
                && !_player.Health.IsDowned && _abilities.isActiveAndEnabled;
            pressureRoot.gameObject.SetActive(visible);
            if (!visible)
            {
                Clear();
                return;
            }

            var current = _abilities.Pressure;
            var normalized = _abilities.PressureNormalized;

            if (!_placed)
            {
                _displayed = normalized;
                _previous = current;
                _placed = true;
            }

            TrackPressureChanges(current);
            UpdatePressureRing(current, normalized);
            UpdateNoteEmission(normalized);
            UpdateNotes();
        }

        private void TrackPressureChanges(float current)
        {
            var gaining = current > _previous + 0.0001f;
            if (current <= 0f && _previous > 0f) ClearNotes();

            var dt = Time.deltaTime;
            if (gaining)
            {
                _gainedAt = Time.time;
                _losing = false;
            }
            else if (dt > 0f && !Mathf.Approximately(current, _previous))
                _losing = (current - _previous) / dt < -tuning.pressure.idleDecayPerSecond * 1.1f;

            _previous = current;
        }

        private void UpdatePressureRing(float current, float normalized)
        {
            var settings = tuning.pressureVisuals;
            var blend = 1f - Mathf.Exp(-settings.response * _player.Hitstop.ActionDeltaTime);
            _displayed = current <= 0f ? 0f : Mathf.Lerp(_displayed, normalized, blend);

            var intensity = tuning.presentation.pressureGlow;
            var heat = Mathf.Clamp01(_displayed * intensity);
            var atMax = _displayed >= 0.995f;
            if (atMax && !_maxed) _maxPulseAt = Time.time;
            _maxed = atMax;

            var pulse = 1f - Mathf.Clamp01((Time.time - _maxPulseAt) / 1.4f);
            var throb = atMax ? 0.5f + 0.5f * Mathf.Sin(Time.time * 1.3f) : 0f;
            var track = Color.Lerp(settings.trackColor, tuning.presentation.gold, heat * 0.5f);
            track.a = Mathf.Lerp(settings.trackColor.a, 0.55f, heat);
            DrawRing(capacityRing, 1f, settings.trackWidth * (1f + 0.4f * heat), track);
            var color = _losing ? settings.losingColor : Color.Lerp(tuning.presentation.gold,
                settings.fullColor, Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(settings.fullColorThreshold, 1f, _displayed)));
            color.a *= Mathf.Lerp(0.4f, 1f, heat);

            fillRing.enabled = _displayed > 0.0001f;
            if (fillRing.enabled)
            {
                color = Color.Lerp(color, Color.white, 0.12f * throb + 0.35f * pulse * pulse);
                DrawRing(fillRing, _displayed, settings.fillWidth
                    * (1f + 0.5f * heat + 0.12f * throb + 0.35f * pulse * pulse), color);
                ApplyFillGradient(color, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, heat)) * 0.7f,
                    Time.time * 0.07f);
            }
        }

        private void UpdateNoteEmission(float normalized)
        {
            var settings = tuning.pressureVisuals;
            // Keep notes flowing briefly after each Pressure gain.
            var building = Time.time - _gainedAt < 1.5f || (normalized >= 0.999f && !_losing);
            var emitting = building && !_player.Hitstop.IsStopped && _player.Hitstop.ActionDeltaTime > 0f;
            var targetRate = emitting
                ? Mathf.Lerp(settings.lowNoteRate, settings.highNoteRate, _displayed) : 0f;
            _noteRate = Mathf.Lerp(_noteRate, targetRate, 1f - Mathf.Exp(-settings.emissionResponse * Time.deltaTime));
            _emission = emitting ? Mathf.Min(2f, _emission + _noteRate * Time.deltaTime) : 0f;

            while (_emission >= 1f)
            {
                SpawnNote();
                _emission -= 1f;
            }
        }

        private void DrawRing(LineRenderer line, float fraction, float width, Color color)
        {
            const int segments = 40;
            var settings = tuning.pressureVisuals;
            var center = transform.position + Vector3.up * settings.height;
            line.positionCount = segments + 1;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            for (var i = 0; i <= segments; i++)
            {
                var angle = fraction * i / segments * Mathf.PI * 2f;
                line.SetPosition(i, center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * settings.radius);
            }
        }

        private void ApplyFillGradient(Color baseColor, float highlight, float turn)
        {
            const float half = 0.14f;
            var centre = half + Mathf.Repeat(turn, 1f) * (1f - 2f * half);
            var tip = Color.Lerp(baseColor, Color.white, highlight * 0.6f);
            tip.a = Mathf.Min(1f, baseColor.a * (1f + 0.3f * highlight));
            var times = new[] { 0f, centre - half, centre, centre + half, 1f };
            for (var i = 0; i < 5; i++)
            {
                var c = i == 2 ? tip : baseColor;
                _colorKeys[i] = new GradientColorKey(c, times[i]);
                _alphaKeys[i] = new GradientAlphaKey(c.a, times[i]);
            }
            _fillGradient.SetKeys(_colorKeys, _alphaKeys);
            fillRing.colorGradient = _fillGradient;
        }

        private void SpawnNote()
        {
            for (var i = 0; i < notes.Length; i++)
            {
                if (notes[i].enabled) continue;
                var phase = _sequence++ * 2.39996f;
                _states[i] = new Note
                {
                    Origin = transform.position + Vector3.up * tuning.pressureVisuals.noteHeight
                        + new Vector3(Mathf.Cos(phase), 0f, Mathf.Sin(phase)) * tuning.pressureVisuals.spawnRadius,
                    BornAt = Time.time,
                    Phase = phase,
                    Color = Color.Lerp(tuning.presentation.gold, tuning.pressureVisuals.fullColor, _displayed)
                };
                notes[i].enabled = true;
                notes[i].color = Color.clear;
                return;
            }
        }

        private void UpdateNotes()
        {
            var settings = tuning.pressureVisuals;
            var scale = settings.noteSize / settings.noteSprite.bounds.size.y;
            for (var i = 0; i < notes.Length; i++)
            {
                if (!notes[i].enabled) continue;
                var note = _states[i];
                var progress = (Time.time - note.BornAt) / settings.noteLifetime;
                if (progress >= 1f)
                {
                    notes[i].enabled = false;
                    continue;
                }

                // Each note keeps its birth position while homing on the moving bell.
                var bell = _weapon.BellPosition;
                var pull = progress * progress;
                var arc = Mathf.Sin(progress * Mathf.PI);
                var swirl = Mathf.Sin(note.Phase) * settings.driftDistance * arc * (1f - pull);
                var position = Vector3.Lerp(note.Origin, bell, pull);
                position += Vector3.up * (settings.riseDistance * arc * (1f - pull));
                position += _camera.transform.right * swirl;
                notes[i].transform.SetPositionAndRotation(position, _camera.transform.rotation);
                notes[i].transform.localScale = Vector3.one * (scale * (1f - 0.55f * pull));
                var color = note.Color;
                color.a *= Mathf.Min(1f, progress / 0.2f) * (1f - Mathf.SmoothStep(0.8f, 1f, progress))
                    * Mathf.Lerp(0.3f, 0.8f, _displayed);
                notes[i].color = color;
            }
        }

        private void Clear()
        {
            _placed = _losing = _maxed = false;
            _maxPulseAt = float.NegativeInfinity;
            capacityRing.enabled = true;
            fillRing.enabled = false;
            ClearNotes();
            pressureRoot.gameObject.SetActive(false);
        }

        private void ClearNotes()
        {
            _noteRate = _emission = 0f;
            foreach (var note in notes) note.enabled = false;
        }

        private void HandleSceneChanged(Scene previous, Scene current)
        {
            Clear();
            _camera = null;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            Clear();
        }
    }
}
