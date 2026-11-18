using Game50033;
using UnityEngine;

// "Something is about to fall on you" warning, generated in code (no drawing needed).
// - A dashed ring marks the full impact area from the start.
// - A dithered pixel shadow grows from small and faint to full size and dark, speeding up like a falling object.
// - The ring flashes just before impact.
//
// Setup: put this on an empty GameObject / prefab and assign two materials
// made with URP > Unlit, Surface Type: Transparent (colour left white). The script supplies the textures.
public class FallingTelegraph : BaseTelegraph
{
    [Header("Materials (URP/Unlit, Surface Type: Transparent)")]
    [SerializeField] private Material ringMaterial;
    [SerializeField] private Material shadowMaterial;

    [Header("Size")]
    [SerializeField] private float radius = 2f;
    [SerializeField, Range(0f, 1f)] private float startSize = 0.2f;   // shadow size at the start, as a fraction of the full radius
    [SerializeField] private float heightAboveGround = 0.02f;
    [SerializeField, Min(1)] private int pixelsPerUnit = 16;          // match your sprites so the pixels look the same size

    [Header("Ring")]
    [SerializeField, Min(1)] private int ringPixels = 2;
    [SerializeField, Min(0)] private int dashCount = 16;              // 0 = solid ring
    [SerializeField] private Color ringColor = new Color(0.95f, 0.10f, 0.20f, 0.90f);

    [Header("Shadow")]
    [SerializeField] private Color shadowColor = new Color(0.10f, 0.02f, 0.08f, 0.75f);
    [SerializeField, Range(0f, 1f)] private float startDarkness = 0.15f;   // how dark the shadow is at the start

    [Header("Warning flash near the end")]
    [SerializeField, Range(0f, 1f)] private float flashStart = 0.85f;
    [SerializeField] private float flashInterval = 0.08f;
    [SerializeField] private Color flashColor = new Color(1f, 0.9f, 0.9f, 1f);

    [Header("Following")]
    [SerializeField] private float followSmoothing = 15f;

    // 4x4 Bayer pattern: decides which pixels appear first as the shadow darkens (classic pixel-art dithering)
    private static readonly int[,] Bayer =
    {
        {  0,  8,  2, 10 },
        { 12,  4, 14,  6 },
        {  3, 11,  1,  9 },
        { 15,  7, 13,  5 }
    };

    private Transform _ring, _shadow;
    private Material _ringMat, _shadowMat;
    private Texture2D _ringTex, _ringFlashTex, _shadowTex;

    private float _duration, _elapsed;
    private int _fullRadiusPx;
    private int _builtShadowRadiusPx = -1;
    private int _builtDensityStep = -1;
    private bool _ringFlashing;
    private float _groundY;
    private Vector3 _followTarget;

    private void Awake()
    {
        _ringMat = ringMaterial != null ? new Material(ringMaterial) : null;
        _shadowMat = shadowMaterial != null ? new Material(shadowMaterial) : null;

        _shadow = CreateQuad("Shadow", _shadowMat, heightAboveGround);
        _ring = CreateQuad("Ring", _ringMat, heightAboveGround + 0.01f);   // ring draws on top of the shadow
    }

    public override void Begin(Vector3 from, Vector3 to, float duration)
    {
        _duration = Mathf.Max(duration, 0.01f);
        _elapsed = 0f;

        Vector3 ground = SnapToGround(to);
        _groundY = ground.y;                 // remember the floor height once
        transform.position = ground;
        _followTarget = ground;
        transform.rotation = Quaternion.identity;

        _fullRadiusPx = Mathf.Max(3, Mathf.RoundToInt(radius * pixelsPerUnit));

        DestroyRingTextures();
        _ringTex = BuildRing(_fullRadiusPx, ringColor);
        _ringFlashTex = BuildRing(_fullRadiusPx, flashColor);
        if (_ringMat != null) _ringMat.mainTexture = _ringTex;
        _ringFlashing = false;

        float diameter = _fullRadiusPx * 2f / pixelsPerUnit;
        _ring.localScale = new Vector3(diameter, diameter, 1f);

        _builtShadowRadiusPx = -1;
        _builtDensityStep = -1;
        UpdateVisuals(0f);
    }

    public override void SetTarget(Vector3 from, Vector3 to)
    {
        // only follow on the ground plane; keep the floor height found in Begin
        _followTarget = new Vector3(to.x, _groundY, to.z);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        // glide toward the target instead of snapping, so small jitters are smoothed out
        transform.position = Vector3.Lerp(transform.position, _followTarget, 1f - Mathf.Exp(-followSmoothing * Time.deltaTime));

        UpdateVisuals(Mathf.Clamp01(_elapsed / _duration));
    }

    private void UpdateVisuals(float progress)
    {
        // falling feel: slow at first, then rushing in at the end
        float fall = progress * progress;

        // shadow size and darkness
        int radiusPx = Mathf.Max(1, Mathf.RoundToInt(_fullRadiusPx * Mathf.Lerp(startSize, 1f, fall)));
        float darkness = Mathf.Lerp(startDarkness, 1f, fall);
        int densityStep = Mathf.RoundToInt(darkness * 16f);   // 17 levels, matching the 4x4 dither pattern

        if (radiusPx != _builtShadowRadiusPx || densityStep != _builtDensityStep)
        {
            _builtShadowRadiusPx = radiusPx;
            _builtDensityStep = densityStep;

            if (_shadowTex != null) Destroy(_shadowTex);
            _shadowTex = BuildShadow(radiusPx, densityStep / 16f);
            if (_shadowMat != null) _shadowMat.mainTexture = _shadowTex;

            float diameter = radiusPx * 2f / pixelsPerUnit;
            _shadow.localScale = new Vector3(diameter, diameter, 1f);
        }

        // ring flash just before impact
        bool flashOn = progress >= flashStart && Mathf.FloorToInt(_elapsed / flashInterval) % 2 == 0;
        if (flashOn != _ringFlashing && _ringMat != null)
        {
            _ringFlashing = flashOn;
            _ringMat.mainTexture = flashOn ? _ringFlashTex : _ringTex;
        }
    }

    // A filled circle drawn with dithered pixels. density 0 = nothing, 1 = fully filled.
    // The centre is denser than the edge, so it looks like a soft shadow.
    private Texture2D BuildShadow(int radiusPx, float density)
    {
        int size = radiusPx * 2;
        var tex = NewTexture(size);
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - radiusPx;
                float dy = y + 0.5f - radiusPx;
                float edge = radiusPx - Mathf.Sqrt(dx * dx + dy * dy);   // pixels inside the edge

                if (edge < 0f) { pixels[y * size + x] = Color.clear; continue; }

                // softer towards the edge
                float softness = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(edge / (radiusPx * 0.5f)));
                float threshold = (Bayer[y % 4, x % 4] + 0.5f) / 16f;

                pixels[y * size + x] = density * softness > threshold ? shadowColor : Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // A ring outline, optionally broken into dashes.
    private Texture2D BuildRing(int radiusPx, Color color)
    {
        int size = radiusPx * 2;
        var tex = NewTexture(size);
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - radiusPx;
                float dy = y + 0.5f - radiusPx;
                float edge = radiusPx - Mathf.Sqrt(dx * dx + dy * dy);

                bool onRing = edge >= 0f && edge < ringPixels;

                if (onRing && dashCount > 0)
                {
                    float angle01 = (Mathf.Atan2(dy, dx) / (Mathf.PI * 2f)) + 0.5f;   // 0 to 1 around the circle
                    onRing = Mathf.FloorToInt(angle01 * dashCount * 2f) % 2 == 0;     // every other segment
                }

                pixels[y * size + x] = onRing ? color : Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D NewTexture(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,   // crisp, blocky pixels
            wrapMode = TextureWrapMode.Clamp
        };
    }

    // Finds the floor directly below a point, so the warning lies on the ground.
    private static Vector3 SnapToGround(Vector3 point)
    {
        Vector3 rayStart = point + Vector3.up * 5f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;
        return point;
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

    private void DestroyRingTextures()
    {
        if (_ringTex != null) Destroy(_ringTex);
        if (_ringFlashTex != null) Destroy(_ringFlashTex);
    }

    private void OnDestroy()
    {
        DestroyRingTextures();
        if (_shadowTex != null) Destroy(_shadowTex);
        if (_ringMat != null) Destroy(_ringMat);
        if (_shadowMat != null) Destroy(_shadowMat);
    }
}