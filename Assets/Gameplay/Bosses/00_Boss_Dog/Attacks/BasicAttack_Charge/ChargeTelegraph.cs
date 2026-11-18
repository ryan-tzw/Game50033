using UnityEngine;

namespace Game50033
{
    public class ChargeTelegraph : BaseTelegraph
    {
        [Header("Materials (URP/Unlit, Surface Type: Transparent)")]
        [SerializeField] private Material laneMaterial;
        [SerializeField] private Material fillMaterial;

        [Header("Size")]
        [SerializeField] private float width = 1.5f;
        [SerializeField] private float heightAboveGround = 0.02f;
        [SerializeField, Min(1)] private int pixelsPerUnit = 16;   // match your sprites so the pixels look the same size

        [Header("Pixel style")]
        [SerializeField, Min(1)] private int outlinePixels = 1;
        [SerializeField, Min(0)] private int highlightPixels = 1;
        [SerializeField, Min(1)] private int stripeSpacing = 3;

        [Header("Lane colours (soft pink, see-through)")]
        [SerializeField] private Color laneOutline = new Color(0.60f, 0.10f, 0.25f, 0.85f);
        [SerializeField] private Color laneHighlight = new Color(1.00f, 0.70f, 0.80f, 0.55f);
        [SerializeField] private Color laneInner = new Color(1.00f, 0.45f, 0.60f, 0.25f);
        [SerializeField] private Color laneStripe = new Color(1.00f, 0.35f, 0.50f, 0.40f);

        [Header("Fill colours (danger red)")]
        [SerializeField] private Color fillOutline = new Color(0.40f, 0.00f, 0.05f, 1.00f);
        [SerializeField] private Color fillHighlight = new Color(1.00f, 0.40f, 0.45f, 1.00f);
        [SerializeField] private Color fillInner = new Color(0.90f, 0.05f, 0.15f, 0.90f);
        [SerializeField] private Color fillStripe = new Color(0.70f, 0.00f, 0.10f, 0.90f);

        [Header("Warning flash near the end")]
        [SerializeField, Range(0f, 1f)] private float flashStart = 0.85f;   // start flashing at 85% full
        [SerializeField] private float flashInterval = 0.08f;
        [SerializeField] private Color flashColor = new Color(1f, 0.85f, 0.9f, 1f);   // pale pink-white

        private Transform _lane, _fill;
        private Material _laneMat, _fillMat;
        private Texture2D _laneTex, _fillTex, _flashTex;
        private int _texWidth, _texHeight;

        private float _length, _duration, _elapsed;

        private void Awake()
        {
            _laneMat = laneMaterial != null ? new Material(laneMaterial) : null;
            _fillMat = fillMaterial != null ? new Material(fillMaterial) : null;

            _lane = CreateQuad("Lane", _laneMat, heightAboveGround);
            _fill = CreateQuad("Fill", _fillMat, heightAboveGround + 0.01f);   // just above the lane
        }

        // Call once when the wind-up starts.
        public override void Begin(Vector3 from, Vector3 to, float duration)
        {
            _duration = Mathf.Max(duration, 0.01f);
            _elapsed = 0f;
            SetEnds(from, to);
            UpdateFill(0f);
        }

        // Call again if the start or end point moves.
        public void SetEnds(Vector3 from, Vector3 to)
        {
            Vector3 flat = to - from;
            flat.y = 0f;
            _length = Mathf.Max(flat.magnitude, 0.01f);

            transform.position = from;
            if (flat.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);

            _lane.localScale = new Vector3(width, _length, 1f);
            _lane.localPosition = new Vector3(0f, heightAboveGround, _length * 0.5f);

            RebuildTexturesIfNeeded();
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_elapsed / _duration);
            UpdateFill(progress);

            // flash between the red fill and a light version just before the charge
            if (_fillMat != null && _fillTex != null)
            {
                bool flashOn = progress >= flashStart && Mathf.FloorToInt(_elapsed / flashInterval) % 2 == 0;
                _fillMat.mainTexture = flashOn ? _flashTex : _fillTex;
            }
        }

        // 0 = empty, 1 = full. Crops the texture instead of squashing it, so pixels keep their size.
        private void UpdateFill(float progress)
        {
            float filled = Mathf.Max(_length * progress, 0.0001f);
            _fill.localScale = new Vector3(width, filled, 1f);
            _fill.localPosition = new Vector3(0f, heightAboveGround + 0.01f, filled * 0.5f);

            if (_fillMat != null)
                _fillMat.mainTextureScale = new Vector2(1f, Mathf.Max(progress, 0.0001f));
        }

        // Makes textures with exactly one texel per screen "pixel" for this lane size.
        private void RebuildTexturesIfNeeded()
        {
            int w = Mathf.Max(4, Mathf.RoundToInt(width * pixelsPerUnit));
            int h = Mathf.Max(4, Mathf.RoundToInt(_length * pixelsPerUnit));
            if (w == _texWidth && h == _texHeight && _laneTex != null) return;

            _texWidth = w;
            _texHeight = h;
            DestroyTextures();

            _laneTex = BuildTexture(w, h, laneOutline, laneHighlight, laneInner, laneStripe);
            _fillTex = BuildTexture(w, h, fillOutline, fillHighlight, fillInner, fillStripe);
            _flashTex = BuildTexture(w, h, fillOutline, flashColor, flashColor, fillHighlight);

            if (_laneMat != null) _laneMat.mainTexture = _laneTex;
            if (_fillMat != null) _fillMat.mainTexture = _fillTex;
        }

        private Texture2D BuildTexture(int w, int h, Color outline, Color highlight, Color inner, Color stripe)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,          // crisp, blocky pixels
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // distance (in pixels) to the nearest edge
                    int edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));

                    Color c;
                    if (edge < outlinePixels) c = outline;
                    else if (edge < outlinePixels + highlightPixels) c = highlight;
                    else c = ((x + y) / stripeSpacing) % 2 == 0 ? stripe : inner;   // diagonal stripes

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Transform CreateQuad(string quadName, Material material, float y)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = quadName;
            Destroy(quad.GetComponent<Collider>());   // visual only

            quad.transform.SetParent(transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat, facing up
            quad.transform.localPosition = new Vector3(0f, y, 0f);

            var renderer = quad.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return quad.transform;
        }

        private void DestroyTextures()
        {
            if (_laneTex != null) Destroy(_laneTex);
            if (_fillTex != null) Destroy(_fillTex);
            if (_flashTex != null) Destroy(_flashTex);
        }

        private void OnDestroy()
        {
            DestroyTextures();
            if (_laneMat != null) Destroy(_laneMat);
            if (_fillMat != null) Destroy(_fillMat);
        }
    }
}
