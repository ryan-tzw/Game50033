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
        private Note[] _states;
        private float _displayed;
        private float _previous;
        private float _noteRate;
        private float _emission;
        private int _sequence;
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
            if (tuning == null || pressureRoot == null || capacityRing == null || fillRing == null
                || notes == null || notes.Length == 0 || tuning.pressureVisuals.noteSprite == null
                || tuning.presentation.effectMaterial == null || tuning.presentation.weaponMaterial == null)
            {
                Debug.LogError("TrombonePressureVisual requires tuning, its authored rings and pooled note sprites.", this);
                enabled = false;
                return;
            }
            _states = new Note[notes.Length];
            capacityRing.sharedMaterial = fillRing.sharedMaterial = tuning.presentation.effectMaterial;
            capacityRing.useWorldSpace = fillRing.useWorldSpace = true;
            foreach (var note in notes)
            {
                if (note == null)
                {
                    Debug.LogError("TrombonePressureVisual has a missing note renderer.", this);
                    enabled = false;
                    return;
                }
                note.sprite = tuning.pressureVisuals.noteSprite;
                note.sharedMaterial = tuning.presentation.weaponMaterial;
            }
            Clear();
        }

        private void OnEnable() => SceneManager.activeSceneChanged += HandleSceneChanged;

        private void LateUpdate()
        {
            if (pressureRoot == null || _states == null) return;
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
            var gaining = current > _previous + 0.0001f;
            if (current <= 0f && _previous > 0f) ClearNotes();
            if (!Mathf.Approximately(current, _previous)) _losing = current < _previous;
            _previous = current;

            var settings = tuning.pressureVisuals;
            var blend = 1f - Mathf.Exp(-settings.response * _player.Hitstop.ActionDeltaTime);
            _displayed = current <= 0f ? 0f : Mathf.Lerp(_displayed, normalized, blend);
            DrawRing(capacityRing, 1f, settings.trackWidth, settings.trackColor);
            var color = _losing ? settings.losingColor : Color.Lerp(tuning.presentation.gold,
                settings.fullColor, Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(settings.fullColorThreshold, 1f, _displayed)));
            fillRing.enabled = _displayed > 0.0001f;
            if (fillRing.enabled) DrawRing(fillRing, _displayed, settings.fillWidth, color);

            var building = gaining || (normalized >= 0.999f && !_losing);
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
            UpdateNotes();
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
            var scale = settings.noteSize / Mathf.Max(0.001f, settings.noteSprite.bounds.size.y);
            for (var i = 0; i < notes.Length; i++)
            {
                if (!notes[i].enabled) continue;
                var note = _states[i];
                var progress = (Time.time - note.BornAt) / Mathf.Max(0.01f, settings.noteLifetime);
                if (progress >= 1f)
                {
                    notes[i].enabled = false;
                    continue;
                }
                var envelope = Mathf.Sin(progress * Mathf.PI);
                var drift = Mathf.Sin(note.Phase + progress * 2f) * settings.driftDistance * envelope;
                notes[i].transform.SetPositionAndRotation(note.Origin + Vector3.up * (settings.riseDistance * progress)
                    + _camera.transform.right * drift, _camera.transform.rotation
                    * Quaternion.AngleAxis(Mathf.Sin(note.Phase) * 8f * envelope, Vector3.forward));
                notes[i].transform.localScale = Vector3.one * scale;
                var color = note.Color;
                color.a *= Mathf.Min(1f, progress / 0.12f) * (1f - progress) * (1f - progress)
                    * Mathf.Lerp(0.35f, 0.85f, _displayed);
                notes[i].color = color;
            }
        }

        private void Clear()
        {
            _placed = _losing = false;
            if (capacityRing != null) capacityRing.enabled = true;
            if (fillRing != null) fillRing.enabled = false;
            ClearNotes();
            if (pressureRoot != null) pressureRoot.gameObject.SetActive(false);
        }

        private void ClearNotes()
        {
            _noteRate = _emission = 0f;
            if (notes != null)
                foreach (var note in notes)
                    if (note != null) note.enabled = false;
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
