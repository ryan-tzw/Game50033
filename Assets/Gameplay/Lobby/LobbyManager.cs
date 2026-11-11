namespace Game50033
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    /// Owns lobby state: which players have joined, and which character each one has claimed.
    /// Characters are first come, first served and unique per player.
    /// </summary>
    public sealed class LobbyManager : MonoBehaviour
    {
        [SerializeField] private PlayerInputManager playerInputManager;
        [SerializeField] private CharacterRoster roster;
        [Tooltip("One panel per player slot, indexed by PlayerInput.playerIndex.")]
        [SerializeField] private CharacterSelectPanel[] panels = Array.Empty<CharacterSelectPanel>();

        private readonly List<PlayerCharacterSelect> _players = new();
        private readonly Dictionary<CharacterDefinition, PlayerCharacterSelect> _claims = new();
        private PlayerCharacterSelect _replacingPlayer;

        /// <summary>Raised whenever a character is claimed or released.</summary>
        public event Action SelectionChanged;

        public CharacterRoster Roster => roster;
        public IReadOnlyList<PlayerCharacterSelect> Players => _players;

        /// <summary>True when at least one player joined and every joined player has spawned.</summary>
        public bool AllPlayersSpawned
        {
            get
            {
                if (_players.Count == 0) return false;
                foreach (var player in _players)
                {
                    if (!player.IsSpawned) return false;
                }
                return true;
            }
        }

        private void Start()
        {
            for (var i = 0; i < panels.Length; i++)
            {
                if (panels[i]) panels[i].ShowEmpty(i);
            }
        }

        private void OnEnable()
        {
            if (!playerInputManager) return;
            playerInputManager.onPlayerJoined += HandlePlayerJoined;
            playerInputManager.onPlayerLeft += HandlePlayerLeft;
        }

        private void OnDisable()
        {
            if (!playerInputManager) return;
            playerInputManager.onPlayerJoined -= HandlePlayerJoined;
            playerInputManager.onPlayerLeft -= HandlePlayerLeft;
        }

        private void HandlePlayerJoined(PlayerInput playerInput)
        {
            if (_replacingPlayer != null)
            {
                return;
            }

            if (!playerInput.TryGetComponent(out PlayerCharacterSelect player))
            {
                Debug.LogError($"{playerInput.name} has no {nameof(PlayerCharacterSelect)}; it can't pick a character.", playerInput);
                return;
            }

            _players.Add(player);
            player.BeginSelection(this);
        }

        private void HandlePlayerLeft(PlayerInput playerInput)
        {
            if (_replacingPlayer != null)
            {
                return;
            }

            if (!playerInput.TryGetComponent(out PlayerCharacterSelect player)) return;

            _players.Remove(player);
            Release(player);

            var panel = GetPanel(playerInput.playerIndex);
            if (panel) panel.ShowEmpty(playerInput.playerIndex);
        }

        internal bool TrySpawnSelectedCharacter(PlayerCharacterSelect source, CharacterDefinition character)
        {
            var slot = _players.IndexOf(source);
            var prefab = character.PlayerPrefab;
            var joinManager = playerInputManager != null
                ? playerInputManager.GetComponent<PlayerJoinManager>()
                : null;

            if (slot < 0 || _replacingPlayer != null
                || joinManager == null || !playerInputManager.joiningEnabled
                || prefab == null || !prefab.gameObject.activeSelf
                || prefab.GetComponent<PlayerCharacterSelect>() == null
                || prefab.GetComponents<PlayerAbilities>().Length != 1)
            {
                Debug.LogError(
                    "Character spawning needs an active player prefab with PlayerCharacterSelect and one PlayerAbilities, and an enabled join manager.",
                    this);
                return false;
            }

            var sourceInput = source.PlayerInput;

            // Preserve the player's index and input devices when changing character prefabs.
            var devices = new InputDevice[sourceInput.devices.Count];
            for (var i = 0; i < devices.Length; i++)
            {
                devices[i] = sourceInput.devices[i];
            }

            var playerIndex = sourceInput.playerIndex;
            var splitScreenIndex = sourceInput.splitScreenIndex;
            var scheme = sourceInput.currentControlScheme;
            var position = source.transform.position;
            var rotation = source.transform.rotation;
            var completed = false;
            PlayerInput replacementInput = null;

            // Ignore replacement join/leave callbacks until the Lobby slot is updated.
            _replacingPlayer = source;

            try
            {
                sourceInput.enabled = false;
                replacementInput = joinManager.JoinPlayerWithPrefab(
                    prefab,
                    playerIndex,
                    splitScreenIndex,
                    scheme,
                    devices);

                if (replacementInput == null)
                {
                    return false;
                }

                var replacement = replacementInput.GetComponent<PlayerCharacterSelect>();
                var controller = replacementInput.GetComponent<CharacterController>();
                controller.enabled = false;
                replacement.transform.SetPositionAndRotation(position, rotation);

                _players[slot] = replacement;
                _claims[character] = replacement;
                replacement.CompleteSelection(this, character);

                source.DetachFromLobby();
                source.gameObject.SetActive(false);
                completed = true;
                Destroy(source.gameObject);
                SelectionChanged?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return completed;
            }
            finally
            {
                try
                {
                    if (!completed)
                    {
                        if (replacementInput != null)
                        {
                            replacementInput.enabled = false;
                            replacementInput.gameObject.SetActive(false);
                            Destroy(replacementInput.gameObject);
                        }

                        // Restore the original selection and devices if replacement fails.
                        _players[slot] = source;
                        _claims[character] = source;
                        var defaultScheme = sourceInput.defaultControlScheme;
                        sourceInput.defaultControlScheme = scheme;
                        sourceInput.enabled = true;
                        sourceInput.defaultControlScheme = defaultScheme;
                        if (!string.IsNullOrEmpty(scheme))
                        {
                            sourceInput.SwitchCurrentControlScheme(scheme, devices);
                        }

                        if (sourceInput.playerIndex != playerIndex)
                        {
                            GetPanel(playerIndex)?.ShowEmpty(playerIndex);
                        }

                        source.BeginSelection(this);
                    }
                }
                finally
                {
                    _replacingPlayer = null;
                }
            }
        }

        public CharacterSelectPanel GetPanel(int playerIndex) =>
            playerIndex >= 0 && playerIndex < panels.Length ? panels[playerIndex] : null;

        public bool IsClaimedByOther(CharacterDefinition character, PlayerCharacterSelect player) =>
            _claims.TryGetValue(character, out var owner) && owner != null && owner != player;

        public int FirstAvailableCharacterIndex()
        {
            for (var i = 0; i < roster.Count; i++)
            {
                if (!IsClaimedByOther(roster[i], null)) return i;
            }
            return 0;
        }

        public bool TryClaim(CharacterDefinition character, PlayerCharacterSelect player)
        {
            if (character == null || IsClaimedByOther(character, player)) return false;

            Release(player, notify: false);
            _claims[character] = player;
            SelectionChanged?.Invoke();
            return true;
        }

        public void Release(PlayerCharacterSelect player, bool notify = true)
        {
            CharacterDefinition owned = null;
            foreach (var (character, owner) in _claims)
            {
                if (owner == player) { owned = character; break; }
            }

            if (owned == null) return;

            _claims.Remove(owned);
            if (notify) SelectionChanged?.Invoke();
        }

        public void NotifyPlayerSpawned(PlayerCharacterSelect player)
        {
            var panel = GetPanel(player.PlayerIndex);
            if (panel) panel.ShowReady(player.PlayerIndex, player.Character);
        }
    }
}
