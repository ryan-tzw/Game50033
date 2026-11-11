namespace Game50033
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// The ordered list of characters players can pick from in the lobby.
    /// </summary>
    [CreateAssetMenu(menuName = "Game50033/Character Roster", fileName = "CharacterRoster")]
    public sealed class CharacterRoster : ScriptableObject
    {
        [SerializeField] private List<CharacterDefinition> characters = new();

        public int Count => characters.Count;

        public CharacterDefinition this[int index] => characters[index];
    }
}
