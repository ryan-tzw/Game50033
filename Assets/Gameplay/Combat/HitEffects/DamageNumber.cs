namespace Game50033
{
    using UnityEngine;

    // a floating number built from pixel glyph sprites; rises, fades and removes itself. Spawned by HitEffect.
    public sealed class DamageNumber : MonoBehaviour
    {
        private SpriteRenderer[] _glyphs;
        private Vector3 _origin;
        private Color _color;
        private float _rise;
        private float _lifetime;
        private float _startedAt;

        public static void Spawn(Vector3 position, string text, string glyphOrder, Sprite[] glyphSprites, float spacing,
            HitEffect.DamageNumberStyle style, Material material, int layer)
        {
            if (string.IsNullOrEmpty(text) || glyphSprites == null || glyphSprites.Length == 0) return;

            var go = new GameObject($"Damage {text}") { layer = layer };
            go.transform.position = position;
            go.transform.localScale = Vector3.one * style.scale;
            var number = go.AddComponent<DamageNumber>();

            // lay glyphs out left to right in the number's own plane, centred on its origin.
            var renderers = new SpriteRenderer[text.Length];
            var widths = new float[text.Length];
            var total = 0f;
            for (var i = 0; i < text.Length; i++)
            {
                var index = glyphOrder.IndexOf(text[i]);
                var sprite = index >= 0 && index < glyphSprites.Length ? glyphSprites[index] : null;
                widths[i] = sprite != null ? sprite.bounds.size.x : 0f;
                total += widths[i] + (i > 0 ? spacing : 0f);
                if (sprite == null) continue;
                var glyph = new GameObject(text[i].ToString()) { layer = layer };
                glyph.transform.SetParent(go.transform, false);
                var renderer = glyph.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = material;
                renderer.sprite = sprite;
                renderer.color = style.color;
                renderers[i] = renderer;
            }

            var x = -total * 0.5f;
            for (var i = 0; i < text.Length; i++)
            {
                if (renderers[i] != null) renderers[i].transform.localPosition = new Vector3(x + widths[i] * 0.5f, 0f, 0f);
                x += widths[i] + spacing;
            }

            number._glyphs = renderers;
            number._origin = position;
            number._color = style.color;
            number._rise = style.riseDistance;
            number._lifetime = Mathf.Max(0.01f, style.lifetime);
            number._startedAt = Time.time;
            number.UpdatePose(0f);
        }

        private void LateUpdate()
        {
            var t = (Time.time - _startedAt) / _lifetime;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            UpdatePose(t);
        }

        private void UpdatePose(float t)
        {
            // quick ease-out rise, holding opaque for the first half before fading.
            var eased = 1f - (1f - t) * (1f - t);
            transform.position = _origin + Vector3.up * (_rise * eased);
            var camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
            var color = _color;
            color.a *= t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
            foreach (var glyph in _glyphs)
                if (glyph != null) glyph.color = color;
        }
    }
}
