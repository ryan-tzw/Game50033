namespace Game50033
{
    using UnityEngine;
    using UnityEngine.Rendering;

    // Static presentation only. No attack poses, following, recoil or animation.
    [DisallowMultipleComponent]
    public sealed class TrombonePlaceholder : MonoBehaviour
    {
        [SerializeField] private TromboneTuning tuning;
        [SerializeField] private Transform avatarVisual;
        private GameObject _rectangle;
        private Sprite _sprite;

        private void Start()
        {
            var settings = tuning.placeholder;
            _rectangle = new GameObject("Trombone rectangle placeholder");
            _rectangle.layer = LayerMask.NameToLayer("SpriteVisual");
            _rectangle.transform.SetParent(avatarVisual, false);
            _rectangle.transform.localPosition = settings.offset;
            _rectangle.transform.localScale = new Vector3(settings.size.x, settings.size.y, 1f);
            _rectangle.transform.rotation = Camera.main.transform.rotation;
            _sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f), 2f, 0, SpriteMeshType.FullRect);
            var renderer = _rectangle.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sharedMaterial = settings.material;
            renderer.color = settings.color;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void OnEnable()
        {
            if (_rectangle != null) _rectangle.SetActive(true);
        }

        private void OnDisable()
        {
            if (_rectangle != null) _rectangle.SetActive(false);
        }

        private void OnDestroy()
        {
            Destroy(_rectangle);
            Destroy(_sprite);
        }
    }
}
