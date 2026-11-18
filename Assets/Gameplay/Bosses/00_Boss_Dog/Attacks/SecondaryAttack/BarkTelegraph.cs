using Game50033;
using UnityEngine;

// Pixel-art circle warning, generated in code (no drawing needed).
// A faint pink circle shows the full danger area; a red circle grows from the centre to the edge
// over the wind-up, then flashes just before the attack lands.
//
// Setup: put this on an empty GameObject / prefab and assign two materials
// made with URP > Unlit, Surface Type: Transparent (colour left white). The script supplies the textures.
public class BarkTelegraph : BaseTelegraph
{
    [Header("Materials (URP/Unlit, Surface Type: Transparent)")]
    [SerializeField] private Material areaMaterial;
    [SerializeField] private Material fillMaterial;

    [Header("Size")]
    [SerializeField] private float radius = 2f;
    [SerializeField] private float heightAboveGround = 0.02f;
    [SerializeField, Min(1)] private int pixelsPerUnit = 16;   // match your sprites so the pixels look the same size

    [Header("Pixel style")]
    [SerializeField, Min(1)] private int outlinePixels = 1;
    [SerializeField, Min(0)] private int highlightPixels = 1;
    [SerializeField, Min(1)] private int stripeSpacing = 3;

    [Header("Area colours (soft pink, see-through)")]
    [SerializeField] private Color areaOutline = new Color(0.60f, 0.10f, 0.25f, 0.85f);
    [SerializeField] private Color areaHighlight = new Color(1.00f, 0.70f, 0.80f, 0.55f);
    [SerializeField] private Color areaInner = new Color(1.00f, 0.45f, 0.60f, 0.25f);
    [SerializeField] private Color areaStripe = new Color(1.00f, 0.35f, 0.50f, 0.40f);

    [Header("Fill colours (danger red)")]
    [SerializeField] private Color fillOutline = new Color(0.40f, 0.00f, 0.05f, 1.00f);
    [SerializeField] private Color fillHighlight = new Color(1.00f, 0.40f, 0.45f, 1.00f);
    [SerializeField] private Color fillInner = new Color(0.90f, 0.05f, 0.15f, 0.90f);
    [SerializeField] private Color fillStripe = new Color(0.70f, 0.00f, 0.10f, 0.90f);

    [Header("Warning flash near the end")]
    [SerializeField, Range(0f, 1f)] private float flashStart = 0.85f;
    [SerializeField] private float flashInterval = 0.08f;
    [SerializeField] private Color flashColor = new Color(1f, 0.85f, 0.9f, 1f);

    private Transform _area, _fill;
    private Material _areaMat, _fillMat;
    private Texture2D _areaTex, _fillTex;

    private float _duration, _elapsed;
    private int _fullRadiusPx;
    private int _builtFillRadiusPx = -1;
    private bool _builtFillFlash;

    private void Awake()
    {
        _areaMat = areaMaterial != null ? new Material(areaMaterial) : null;
        _fillMat = fillMaterial != null ? new Material(fillMaterial) : null;

        _area = CreateQuad("Area", _areaMat, heightAboveGround);
        _fill = CreateQuad("Fill", _fillMat, heightAboveGround + 0.01f);   // just above the area
    }

    // 'to' is where the circle is centred (the target). 'from' isn't needed for a circle.
    public override void Begin(Vector3 from, Vector3 to, float duration)
    {
        _duration = Mathf.Max(duration, 0.01f);
        _elapsed = 0f;

        transform.position = to;
        transform.rotation = Quaternion.identity;

        // full danger area
        _fullRadiusPx = Mathf.Max(2, Mathf.RoundToInt(radius * pixelsPerUnit));
        if (_areaTex != null) Destroy(_areaTex);
        _areaTex = BuildCircle(_fullRadiusPx, areaOutline, areaHighlight, areaInner, areaStripe);
        if (_areaMat != null) _areaMat.mainTexture = _areaTex;

        float diameter = _fullRadiusPx * 2f / pixelsPerUnit;
        _area.localScale = new Vector3(diameter, diameter, 1f);

        _builtFillRadiusPx = -1;
        UpdateFill(0f);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        UpdateFill(Mathf.Clamp01(_elapsed / _duration));
    }

    // Grows the red circle from the centre. Rebuilds its texture only when its pixel radius
    // (or flash state) changes, so the pixel art stays crisp at every size.
    private void UpdateFill(float progress)
    {
        int radiusPx = Mathf.RoundToInt(_fullRadiusPx * progress);
        bool flashOn = progress >= flashStart && Mathf.FloorToInt(_elapsed / flashInterval) % 2 == 0;

        if (radiusPx < 1)
        {
            _fill.gameObject.SetActive(false);
            return;
        }
        _fill.gameObject.SetActive(true);

        if (radiusPx != _builtFillRadiusPx || flashOn != _builtFillFlash)
        {
            _builtFillRadiusPx = radiusPx;
            _builtFillFlash = flashOn;

            if (_fillTex != null) Destroy(_fillTex);
            _fillTex = flashOn
                ? BuildCircle(radiusPx, fillOutline, flashColor, flashColor, fillHighlight)
                : BuildCircle(radiusPx, fillOutline, fillHighlight, fillInner, fillStripe);

            if (_fillMat != null) _fillMat.mainTexture = _fillTex;

            float diameter = radiusPx * 2f / pixelsPerUnit;
            _fill.localScale = new Vector3(diameter, diameter, 1f);
        }
    }

    private Texture2D BuildCircle(int radiusPx, Color outline, Color highlight, Color inner, Color stripe)
    {
        int size = radiusPx * 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,   // crisp, blocky pixels
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // distance from this pixel's centre to the circle's centre
                float dx = x + 0.5f - radiusPx;
                float dy = y + 0.5f - radiusPx;
                float edge = radiusPx - Mathf.Sqrt(dx * dx + dy * dy);   // pixels inside the edge

                Color c;
                if (edge < 0f) c = Color.clear;                                   // outside the circle
                else if (edge < outlinePixels) c = outline;
                else if (edge < outlinePixels + highlightPixels) c = highlight;
                else c = ((x + y) / stripeSpacing) % 2 == 0 ? stripe : inner;    // diagonal stripes

                pixels[y * size + x] = c;
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

    private void OnDestroy()
    {
        if (_areaTex != null) Destroy(_areaTex);
        if (_fillTex != null) Destroy(_fillTex);
        if (_areaMat != null) Destroy(_areaMat);
        if (_fillMat != null) Destroy(_fillMat);
    }
}
