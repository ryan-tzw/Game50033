namespace Game50033
{
    using UnityEngine;

    // plays a strip of frames once, facing the camera, then removes itself. Spawned by HitEffect.
    public sealed class HitSpriteAnimation : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Sprite[] _frames;
        private float _frameDuration;
        private float _startedAt;

        public static void Spawn(Vector3 position, Sprite[] frames, float frameDuration, float scale, Color color, Material material, int layer)
        {
            if (frames == null || frames.Length == 0) return;
            var go = new GameObject("Hit sprite") { layer = layer };
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var animation = go.AddComponent<HitSpriteAnimation>();
            animation._renderer = go.AddComponent<SpriteRenderer>();
            animation._renderer.sharedMaterial = material;
            animation._renderer.color = color;
            animation._renderer.sprite = frames[0];
            animation._frames = frames;
            animation._frameDuration = Mathf.Max(0.01f, frameDuration);
            animation._startedAt = Time.time;
            animation.FaceCamera();
        }

        private void LateUpdate()
        {
            var frame = Mathf.FloorToInt((Time.time - _startedAt) / _frameDuration);
            if (frame >= _frames.Length)
            {
                Destroy(gameObject);
                return;
            }
            _renderer.sprite = _frames[frame];
            FaceCamera();
        }

        private void FaceCamera()
        {
            var camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
        }
    }
}
