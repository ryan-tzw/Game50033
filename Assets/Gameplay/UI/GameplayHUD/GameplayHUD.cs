namespace Game50033
{
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UIElements;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameplayHUD : MonoBehaviour
    {
        [SerializeField] private GameSession session;
        [SerializeField] private GameplayHudTuning tuning;
        [SerializeField] private VisualTreeAsset playerPanel;
        [SerializeField] private StyleSheet styleSheet;
        private readonly PlayerHudPanel[] _panels = new PlayerHudPanel[4];
        private readonly bool[] _present = new bool[4];
        private VisualElement _row;
        private Label _joinHint;
        private LobbyManager _lobby;
        private bool _started;
        private bool _rosterDirty;
        private bool _hidden;
        private int _appliedRevision = -1;

        private void Start()
        {
            _started = true;
            OnEnable();
        }

        private void OnEnable()
        {
            if (!_started) return;
            if (session == null) session = GetComponentInParent<GameSession>();
            if (session == null) session = FindFirstObjectByType<GameSession>();
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            _row = root.Q("players");
            _joinHint = root.Q<Label>("join-hint");
            if (tuning == null || playerPanel == null || styleSheet == null || _row == null || _joinHint == null || session == null)
            {
                Debug.LogError("GameplayHUD requires its session, tuning, template, stylesheet and document roots.", this);
                return;
            }
            if (!root.styleSheets.Contains(styleSheet)) root.styleSheets.Add(styleSheet);
            session.PlayerRegistered += RosterChanged;
            session.PlayerUnregistered += RosterChanged;
            SceneManager.sceneLoaded += SceneLoaded;
            _appliedRevision = -1;
            _rosterDirty = true;
            SceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _lobby = scene.name == SceneNames.Lobby ? FindFirstObjectByType<LobbyManager>() : null;
            _hidden = scene.name == SceneNames.Cutscene1;
            _row.style.display = _hidden ? DisplayStyle.None : DisplayStyle.Flex;
            // hide the document's contents, not its component: panel instances and listeners survive the cutscene.
            if (_hidden) _joinHint.style.display = DisplayStyle.None;
        }

        private void RosterChanged(Player changed) => _rosterDirty = true;

        private void LateUpdate()
        {
            if (_row == null || _joinHint == null || session == null || tuning == null) return;
            if (_appliedRevision != tuning.Revision)
            {
                _appliedRevision = tuning.Revision;
                GetComponent<UIDocument>().rootVisualElement.style.unityFontDefinition = FontDefinition.FromFont(tuning.font);
                _joinHint.style.unityFontDefinition = FontDefinition.FromFont(tuning.font);
                _row.style.bottom = tuning.bottomMargin;
                _joinHint.style.bottom = tuning.bottomMargin;
                _joinHint.style.fontSize = tuning.previewFontSize;
                foreach (var panel in _panels) panel?.ApplyTuning();
            }

            // reconcile once after all join/leave callbacks this frame. Prefab replacement can unregister and
            // register the same index in one operation; rebind that card instead of briefly destroying its UI.
            if (_rosterDirty)
            {
                _rosterDirty = false;
                for (var i = 0; i < _present.Length; i++) _present[i] = false;
                foreach (var player in session.Players)
                {
                    if (player == null || player.PlayerIndex < 0 || player.PlayerIndex >= _panels.Length) continue;
                    var index = player.PlayerIndex;
                    _present[index] = true;
                    if (_panels[index] == null)
                    {
                        _panels[index] = new PlayerHudPanel(player, tuning, playerPanel) { name = "player-panel-" + index };
                        _row.Add(_panels[index]);
                    }
                    else if (_panels[index].BoundPlayer != player) _panels[index].BindPlayer(player);
                }
                var order = 0;
                for (var i = 0; i < _panels.Length; i++)
                {
                    if (!_present[i] && _panels[i] != null)
                    {
                        _panels[i].RemoveFromHierarchy();
                        _panels[i] = null;
                    }
                    if (_panels[i] == null) continue;
                    if (_row.IndexOf(_panels[i]) != order) _row.Insert(order, _panels[i]);
                    order++;
                }
            }
            _joinHint.style.display = !_hidden && _lobby != null && _row.childCount < 4 ? DisplayStyle.Flex : DisplayStyle.None;
            _joinHint.style.left = _row.childCount == 0 ? new StyleLength(0f) : new StyleLength(StyleKeyword.Auto);
            _joinHint.style.right = _row.childCount == 0 ? 0 : 20;
            if (_hidden) return;
            foreach (var panel in _panels) panel?.Refresh(_lobby);
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.PlayerRegistered -= RosterChanged;
                session.PlayerUnregistered -= RosterChanged;
            }
            SceneManager.sceneLoaded -= SceneLoaded;
            for (var i = 0; i < _panels.Length; i++) _panels[i] = null;
            _row?.Clear();
        }
    }
}
