namespace Game50033
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    /// Per-player character selection in the lobby. While selecting, the avatar is hidden and
    /// Move left/right browses the roster; Special (E / bottom face button) confirms; Defensive (Q / right face button) backs out
    /// of the lobby. Once confirmed the avatar spawns with the chosen character and can move and attack.
    ///
    /// Outside the lobby (nobody calls <see cref="BeginSelection"/>) this component does nothing,
    /// so the player prefab still works in test scenes.
    /// </summary>
    [RequireComponent(typeof(PlayerInput), typeof(PlayerController))]
    public sealed class PlayerCharacterSelect : MonoBehaviour
    {
        public enum SelectState
        {
            NotInLobby,
            Selecting,
            Spawned,
        }

        [Tooltip("Visual root hidden while the player is still choosing a character.")]
        [SerializeField] private GameObject avatarVisual;

        [SerializeField, Range(0.1f, 1f)] private float navigateThreshold = 0.6f;

        private PlayerInput _playerInput;
        private PlayerController _controller;
        private PlayerCombat _combat;
        private CharacterController _characterController;
        private InputAction _moveAction;
        private InputAction _confirmAction;
        private InputAction _backAction;

        private LobbyManager _lobby;
        private int _highlightIndex;
        private bool _navigateHeld;
        private int _selectionStartFrame;

        public SelectState State { get; private set; } = SelectState.NotInLobby;
        public PlayerInput PlayerInput => _playerInput;
        public int PlayerIndex => _playerInput.playerIndex;
        public CharacterDefinition Character { get; private set; }
        public bool IsSpawned => State == SelectState.Spawned;

        private void Awake() => CacheComponents();

        // PlayerInputManager raises onPlayerJoined from PlayerInput.OnEnable, i.e. during Instantiate
        // and before this component's Awake has run, so BeginSelection must be able to init us itself.
        private void CacheComponents()
        {
            if (_playerInput) return;

            _playerInput = GetComponent<PlayerInput>();
            _controller = GetComponent<PlayerController>();
            _combat = GetComponent<PlayerCombat>();
            _characterController = GetComponent<CharacterController>();
            _moveAction = _playerInput.actions.FindAction("Player/Move", true);
            _confirmAction = _playerInput.actions.FindAction("Player/Special", true); // E / bottom face button
            _backAction = _playerInput.actions.FindAction("Player/Defensive", true);
        }

        public void BeginSelection(LobbyManager lobby)
        {
            CacheComponents();
            _lobby = lobby;
            State = SelectState.Selecting;
            _selectionStartFrame = Time.frameCount;
            _navigateHeld = true; // require the stick to return to centre before the first step
            SetAvatarActive(false);

            _highlightIndex = _lobby.FirstAvailableCharacterIndex();
            _lobby.SelectionChanged += RefreshPanel;
            RefreshPanel();
        }

        private void Update()
        {
            if (State != SelectState.Selecting || _lobby == null) return;

            var x = _moveAction.ReadValue<Vector2>().x;
            if (!_navigateHeld && Mathf.Abs(x) >= navigateThreshold)
            {
                _navigateHeld = true;
                StepHighlight(x > 0f ? 1 : -1);
            }
            else if (Mathf.Abs(x) < navigateThreshold * 0.5f)
            {
                _navigateHeld = false;
            }

            // ignore the press that made this player join
            if (Time.frameCount == _selectionStartFrame) return;

            if (_backAction.WasPressedThisFrame())
            {
                LeaveLobby();
                return;
            }

            if (_confirmAction.WasPressedThisFrame())
            {
                TryConfirm();
            }
        }

        private void LeaveLobby()
        {
            // destroying the PlayerInput unpairs the device and makes PlayerInputManager raise
            // onPlayerLeft, which frees this slot's panel; the player can press A again to rejoin
            State = SelectState.NotInLobby;
            Destroy(gameObject);
        }

        private void StepHighlight(int direction)
        {
            var count = _lobby.Roster.Count;
            if (count == 0) return;

            _highlightIndex = (_highlightIndex + direction + count) % count;
            RefreshPanel();
        }

        private void TryConfirm()
        {
            if (_lobby.Roster.Count == 0) return;

            var character = _lobby.Roster[_highlightIndex];
            if (!_lobby.TryClaim(character, this)) return; // someone else already has it

            Character = character;
            State = SelectState.Spawned;
            _lobby.SelectionChanged -= RefreshPanel;

            _controller.ApplyCharacter(character);
            SetAvatarActive(true);
            _lobby.NotifyPlayerSpawned(this);
        }

        private void RefreshPanel()
        {
            if (_lobby == null || State != SelectState.Selecting || _lobby.Roster.Count == 0) return;

            var panel = _lobby.GetPanel(PlayerIndex);
            if (!panel) return;

            var roster = _lobby.Roster;
            var takenByOthers = new bool[roster.Count];
            for (var i = 0; i < roster.Count; i++) takenByOthers[i] = _lobby.IsClaimedByOther(roster[i], this);

            panel.ShowSelecting(PlayerIndex, roster, _highlightIndex, takenByOthers);
        }

        private void SetAvatarActive(bool active)
        {
            if (avatarVisual) avatarVisual.SetActive(active);
            _characterController.enabled = active;
            _controller.enabled = active;
            if (_combat) _combat.enabled = active;
        }

        private void OnDestroy()
        {
            if (_lobby != null) _lobby.SelectionChanged -= RefreshPanel;
        }
    }
}
