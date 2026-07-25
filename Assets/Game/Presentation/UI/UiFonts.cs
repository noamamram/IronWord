using UnityEngine;

namespace TypingDefense.Presentation.UI
{
    /// <summary>
    /// Resolves a UI font that includes Hebrew glyphs (bundled Arial).
    /// LegacyRuntime.ttf has no Hebrew — blank buttons in WebGL without this.
    /// </summary>
    public static class UiFonts
    {
        private static Font _cached;

        public static Font Resolve()
        {
            if (_cached != null)
                return _cached;

            _cached = Resources.Load<Font>("Fonts/ArialHebrew");
            if (_cached == null)
                _cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return _cached;
        }
    }
}
