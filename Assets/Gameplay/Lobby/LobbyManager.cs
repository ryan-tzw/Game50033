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
            if (!playerInput.TryGetComponent(out PlayerCharacterSelect player)) return;

            _players.Remove(player);
            Release(player);

            var panel = GetPanel(playerInput.playerIndex);
            if (panel) panel.ShowEmpty(playerInput.playerIndex);
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
