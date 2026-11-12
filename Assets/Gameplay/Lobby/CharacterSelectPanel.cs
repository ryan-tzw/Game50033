namespace Game50033
{
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// The on-screen card for one player slot in the lobby (one per screen corner).
    /// Empty: "press A to join". Selecting: a strip of every character with a cursor on the
    /// highlighted one; characters taken by other players are greyed out. Ready: a compact
    /// card with the chosen character.
    /// </summary>
    public sealed class CharacterSelectPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform frame;
        [SerializeField] private Image background;
        [SerializeField] private Text playerLabel;
        [SerializeField] private Text characterLabel;
        [SerializeField] private Text statusLabel;

        [Header("Selecting")]
        [SerializeField] private GameObject strip;
        [SerializeField] private Image[] cellPortraits = System.Array.Empty<Image>();
        [SerializeField] private Image[] cellCursors = System.Array.Empty<Image>();

        [Header("Ready")]
        [SerializeField] private GameObject readyGroup;
        [SerializeField] private Image readyPortrait;

        [Header("Look")]
        [SerializeField] private Color accentColor = Color.white;
        [SerializeField] private Vector2 emptySize = new(300f, 110f);
        [SerializeField] private Vector2 selectingSize = new(420f, 190f);
        [SerializeField] private Vector2 readySize = new(260f, 130f);
        [SerializeField] private Color emptyBackground = new(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color activeBackground = new(0.08f, 0.07f, 0.12f, 0.9f);
        [SerializeField] private Color takenTint = new(0.25f, 0.25f, 0.25f, 1f);

        public void ShowEmpty(int playerIndex)
        {
            SetLayout(playerIndex, emptySize, emptyBackground, showStrip: false, showReady: false);
            SetText(characterLabel, string.Empty);
            SetText(statusLabel, "Press A / E to join");
        }

        public void ShowSelecting(int playerIndex, CharacterRoster roster, int highlightIndex, bool[] takenByOthers)
        {
            SetLayout(playerIndex, selectingSize, activeBackground, showStrip: true, showReady: false);

            for (var i = 0; i < cellPortraits.Length; i++)
            {
                var hasCharacter = roster != null && i < roster.Count;
                var character = hasCharacter ? roster[i] : null;
                var taken = hasCharacter && takenByOthers != null && i < takenByOthers.Length && takenByOthers[i];

                var portrait = cellPortraits[i];
                if (portrait)
                {
                    portrait.enabled = character && character.Sprite;
                    if (portrait.enabled)
                    {
                        portrait.sprite = character.Sprite;
                        portrait.color = taken ? takenTint * character.Tint : character.Tint;
                    }
                }

                if (i < cellCursors.Length && cellCursors[i])
                {
                    cellCursors[i].enabled = hasCharacter && i == highlightIndex;
                    cellCursors[i].color = accentColor;
                }
            }

            var highlighted = roster != null && highlightIndex < roster.Count ? roster[highlightIndex] : null;
            var highlightedTaken = takenByOthers != null && highlightIndex < takenByOthers.Length && takenByOthers[highlightIndex];
            SetText(characterLabel, highlighted ? highlighted.DisplayName : string.Empty);
            SetText(statusLabel, highlightedTaken ? "TAKEN" : string.Empty);
        }

        public void ShowReady(int playerIndex, CharacterDefinition character)
        {
            SetLayout(playerIndex, readySize, activeBackground, showStrip: false, showReady: true);

            if (readyPortrait)
            {
                readyPortrait.enabled = character && character.Sprite;
                if (readyPortrait.enabled)
                {
                    readyPortrait.sprite = character.Sprite;
                    readyPortrait.color = character.Tint;
                }
            }

            SetText(characterLabel, character ? character.DisplayName : string.Empty);
            SetText(statusLabel, "READY");
        }

        private void SetLayout(int playerIndex, Vector2 size, Color backgroundColor, bool showStrip, bool showReady)
        {
            SetText(playerLabel, $"P{playerIndex + 1}");
            if (playerLabel) playerLabel.color = accentColor;
            if (frame) frame.sizeDelta = size;
            if (background) background.color = backgroundColor;
            if (strip) strip.SetActive(showStrip);
            if (readyGroup) readyGroup.SetActive(showReady);
        }

        private static void SetText(Text label, string value)
        {
            if (label) label.text = value;
        }
    }
}
