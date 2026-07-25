using UnityEngine;

namespace TypingDefense.Content.UI
{
    [CreateAssetMenu(
        fileName = "MenuButtonStyle",
        menuName = "IronWord/UI/Menu Button Style")]
    public sealed class MenuButtonStyle : ScriptableObject
    {
        [Header("Layout")]
        [SerializeField] private Vector2 _size = new Vector2(220f, 64f);
        [SerializeField] private int _fontSize = 22;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = new Color(0.12f, 0.28f, 0.22f, 0.98f);
        [SerializeField] private Color _highlightedColor = new Color(0.18f, 0.42f, 0.32f, 1f);
        [SerializeField] private Color _pressedColor = new Color(0.08f, 0.2f, 0.15f, 1f);
        [SerializeField] private Color _disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
        [SerializeField] private Color _labelColor = Color.white;

        [Header("Labels")]
        [SerializeField] private string _englishLabel = "ENGLISH";
        [Tooltip("Logical Hebrew text. Applied with RTL fix for Unity UI Text.")]
        [SerializeField] private string _hebrewLabel = "עברית";
        [SerializeField] private FontStyle _fontStyle = FontStyle.Bold;

        public Vector2 Size => _size.sqrMagnitude < 1f ? new Vector2(220f, 64f) : _size;
        public int FontSize => Mathf.Max(12, _fontSize);
        public Color NormalColor => _normalColor;
        public Color HighlightedColor => _highlightedColor;
        public Color PressedColor => _pressedColor;
        public Color DisabledColor => _disabledColor;
        public Color LabelColor => _labelColor;
        public string EnglishLabel => string.IsNullOrEmpty(_englishLabel) ? "ENGLISH" : _englishLabel;
        public string HebrewLabel => string.IsNullOrEmpty(_hebrewLabel) ? "עברית" : _hebrewLabel;
        public FontStyle FontStyle => _fontStyle;

        /// <summary>
        /// Legacy UI Text is LTR-only — reverse Hebrew glyphs for correct visual order.
        /// </summary>
        public static string FormatRtlForLegacyText(string logical)
        {
            if (string.IsNullOrEmpty(logical))
                return string.Empty;

            var chars = logical.ToCharArray();
            System.Array.Reverse(chars);
            return new string(chars);
        }
    }
}
