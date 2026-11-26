namespace Game50033
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        [SerializeField] private LobbySpawnZone spawnZone;

        private readonly List<PlayerCharacterSelect> _players = new();
        private readonly Dictionary<CharacterDefinition, PlayerCharacterSelect> _claims = new();
        private PlayerCharacterSelect _replacingPlayer;
        private PlayerInputManager _boundInputManager;

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
            // by now a duplicate join manager loaded with this scene has switched itself off.
            BindInputManager();
            AdoptReturningPlayers();
        }

        private void OnEnable() => BindInputManager();

        private void OnDisable() => UnbindInputManager();

        // on a return to the lobby, the scene's own join manager is a duplicate of the persistent one
        // and destroys itself; use the persistent one that still owns the players and their devices.
        private void BindInputManager()
        {
            var manager = playerInputManager != null && playerInputManager.isActiveAndEnabled
                ? playerInputManager
                : PlayerInputManager.instance;
            if (manager == _boundInputManager) return;

            UnbindInputManager();
            _boundInputManager = manager;
            if (!manager) return;
            manager.onPlayerJoined += HandlePlayerJoined;
            manager.onPlayerLeft += HandlePlayerLeft;
        }

        private void UnbindInputManager()
        {
            if (_boundInputManager)
            {
                _boundInputManager.onPlayerJoined -= HandlePlayerJoined;
                _boundInputManager.onPlayerLeft -= HandlePlayerLeft;
            }
            _boundInputManager = null;
        }

        // players coming back from a run keep their character: claimed, at their slot's spawn, ready.
        // They can still unready in the ring to pick again. Players without a character pick one now.
        private void AdoptReturningPlayers()
        {
            var session = FindFirstObjectByType<GameSession>();
            if (session == null) return;

            foreach (var registered in session.Players.Where(player => player != null).OrderBy(player => player.PlayerIndex))
            {
                if (!registered.TryGetComponent(out PlayerCharacterSelect player) || _players.Contains(player)) continue;
                var playerInput = player.PlayerInput;

                _players.Add(player);
                playerInput.ActivateInput();

                var character = player.Character;
                if (character == null || !TryClaim(character, player))
                {
                    player.BeginSelection(this);
                    continue;
                }

                var controller = playerInput.GetComponent<CharacterController>();
                if (controller) controller.enabled = false;
                player.transform.position = GetSpawnPosition(playerInput.playerIndex, player.transform.position);
                player.CompleteSelection(this, character);
            }
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
            GetComponent<LobbyAudio>()?.PlayJoin();
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
            GetComponent<LobbyAudio>()?.PlayLeave();
        }

        internal bool TrySpawnSelectedCharacter(PlayerCharacterSelect source, CharacterDefinition character)
        {
            var slot = _players.IndexOf(source);
            var prefab = character.PlayerPrefab;
            var inputManager = _boundInputManager;
            var joinManager = inputManager != null
                ? inputManager.GetComponent<PlayerJoinManager>()
                : null;

            if (slot < 0 || _replacingPlayer != null
                || joinManager == null || !inputManager.joiningEnabled
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
            var position = GetSpawnPosition(sourceInput.playerIndex, source.transform.position);
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

                        source.BeginSelection(this);
                    }
                }
                finally
                {
                    _replacingPlayer = null;
                }
            }
        }

        /// <summary>The player's fixed lobby slot, or the fallback when no slot is available.</summary>
        public Vector3 GetSpawnPosition(int playerIndex, Vector3 fallback) =>
            spawnZone != null && spawnZone.TryGetSpawnPosition(playerIndex, out var position) ? position : fallback;

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

    }
}
