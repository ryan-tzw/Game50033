namespace Game50033
{
    using UnityEngine;

    public sealed class GuitarDamageDummy : MonoBehaviour, IHittable
    {
        [SerializeField] private Renderer visual;
        [SerializeField] private TextMesh damageLabel;
        [SerializeField] private Color restColor = new Color(0.3f, 0.4f, 0.5f);

        private MaterialPropertyBlock _properties;
        private Camera _camera;
        private float _lastHitAt = float.NegativeInfinity;

        public int HitCount { get; private set; }
        public float LastDamage { get; private set; }
        public float TotalDamage { get; private set; }

        private void OnEnable()
        {
            _properties ??= new MaterialPropertyBlock();
        }

        public void TakeHit(in HitInfo hit)
        {
            HitCount++;
            LastDamage = hit.Damage;
            TotalDamage += hit.Damage;
            _lastHitAt = Time.time;
            if (damageLabel != null) damageLabel.text = $"{LastDamage:0.#}\ntotal {TotalDamage:0.#}";
        }

        private void LateUpdate()
        {
            if (visual != null)
            {
                var flash = Mathf.Clamp01(1f - (Time.time - _lastHitAt) / 0.15f);
                visual.GetPropertyBlock(_properties);
                _properties.SetColor("_BaseColor", Color.Lerp(restColor, Color.white, flash));
                visual.SetPropertyBlock(_properties);
            }

            if (_camera == null) _camera = Camera.main;
            if (_camera != null && damageLabel != null) damageLabel.transform.rotation = _camera.transform.rotation;
        }
    }
}
