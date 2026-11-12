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
    [RequireComponent(typeof(PlayerInput), typeof(Player))]
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
        private Player _controller;
        private PlayerAbilities _abilities;
        private CharacterController _characterController;
        private InputActionAsset _boundActions;
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
            if (!_playerInput)
            {
                _playerInput = GetComponent<PlayerInput>();
                _controller = GetComponent<Player>();
                _controller.Initialize();
                _abilities = GetComponent<PlayerAbilities>();
                _characterController = GetComponent<CharacterController>();
            }

            // Rebind when the keyboard demo swaps this player's runtime actions.
            if (_boundActions == _playerInput.actions)
            {
                return;
            }

            _boundActions = _playerInput.actions;
            _moveAction = _boundActions.FindAction("Player/Move", true);
            _confirmAction = _boundActions.FindAction("Player/Special", true); // E / bottom face button
            _backAction = _boundActions.FindAction("Player/Defensive", true);
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

            CacheComponents();

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

            var lobby = _lobby;
            if (character.PlayerPrefab != null)
            {
                if (!lobby.TrySpawnSelectedCharacter(this, character))
                {
                    lobby.Release(this);
                }

                return;
            }

            CompleteSelection(lobby, character);
        }

        internal void CompleteSelection(LobbyManager lobby, CharacterDefinition character)
        {
            CacheComponents();
            if (_lobby != null)
            {
                _lobby.SelectionChanged -= RefreshPanel;
            }

            _lobby = lobby;
            Character = character;
            State = SelectState.Spawned;

            _controller.ApplyCharacter(character);
            SetAvatarActive(true);
            _lobby.NotifyPlayerSpawned(this);
        }

        internal void DetachFromLobby()
        {
            if (_lobby != null)
            {
                _lobby.SelectionChanged -= RefreshPanel;
            }

            _lobby = null;
            State = SelectState.NotInLobby;
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
            if (_abilities) _abilities.enabled = active;
        }

        private void OnDestroy()
        {
            if (_lobby != null) _lobby.SelectionChanged -= RefreshPanel;
        }
    }
}
