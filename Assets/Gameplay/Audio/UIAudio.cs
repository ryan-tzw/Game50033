namespace Game50033
{
    using UnityEngine;
    using UnityEngine.UIElements;

    // Plays sounds when a button is highlighted or clicked.
    [DisallowMultipleComponent]
    public sealed class UIAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip navigate;
        [SerializeField] private AudioClip click;
        [SerializeField] private UIDocument document;
        [SerializeField] private string buttonName;
        private Button _button;
        private bool _hovered, _focused;

        private void OnEnable()
        {
            if (source != null) source.ignoreListenerPause = true;
            BindDocument();
        }
        private void Start() => BindDocument();
        private void BindDocument()
        {
            if (document != null && !string.IsNullOrEmpty(buttonName))
                Bind(document.rootVisualElement?.Q<Button>(buttonName));
        }
        public void Bind(Button button)
        {
            if (_button == button) return;
            Unbind();
            _button = button;
            if (_button == null) return;
            _button.RegisterCallback<PointerEnterEvent>(PointerEnter);
            _button.RegisterCallback<PointerLeaveEvent>(PointerLeave);
            _button.RegisterCallback<FocusInEvent>(FocusIn);
            _button.RegisterCallback<FocusOutEvent>(FocusOut);
            _button.clicked += PlayClick; // Also handles keyboard and controller confirmation.
        }
        public void PlayNavigate() => Play(navigate);
        public void PlayClick() => Play(click);
        private void Play(AudioClip clip)
        {
            if (isActiveAndEnabled && source != null && source.isActiveAndEnabled && clip != null
                && (_button == null || _button.enabledInHierarchy)) source.PlayOneShot(clip);
        }
        private void PointerEnter(PointerEnterEvent evt) { if (!_hovered && !_focused) PlayNavigate(); _hovered = true; }
        private void PointerLeave(PointerLeaveEvent evt) => _hovered = false;
        private void FocusIn(FocusInEvent evt) { if (!_focused && !_hovered) PlayNavigate(); _focused = true; }
        private void FocusOut(FocusOutEvent evt) => _focused = false;
        private void Unbind()
        {
            if (_button != null)
            {
                _button.UnregisterCallback<PointerEnterEvent>(PointerEnter);
                _button.UnregisterCallback<PointerLeaveEvent>(PointerLeave);
                _button.UnregisterCallback<FocusInEvent>(FocusIn);
                _button.UnregisterCallback<FocusOutEvent>(FocusOut);
                _button.clicked -= PlayClick;
            }
            _button = null;
            _hovered = _focused = false;
        }
        private void OnDisable() => Unbind();
    }
}
