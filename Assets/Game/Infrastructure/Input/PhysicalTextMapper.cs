using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace TypingDefense.Infrastructure.Input
{
    /// <summary>
    /// Maps physical keyboard keys to training text, independent of OS keyboard language.
    /// </summary>
    public interface IPhysicalTextMapper
    {
        bool TryMapKey(Key key, out string textElement);
    }

    public sealed class EnglishPhysicalTextMapper : IPhysicalTextMapper
    {
        private static readonly Dictionary<Key, string> Map = new Dictionary<Key, string>
        {
            { Key.A, "a" }, { Key.B, "b" }, { Key.C, "c" }, { Key.D, "d" },
            { Key.E, "e" }, { Key.F, "f" }, { Key.G, "g" }, { Key.H, "h" },
            { Key.I, "i" }, { Key.J, "j" }, { Key.K, "k" }, { Key.L, "l" },
            { Key.M, "m" }, { Key.N, "n" }, { Key.O, "o" }, { Key.P, "p" },
            { Key.Q, "q" }, { Key.R, "r" }, { Key.S, "s" }, { Key.T, "t" },
            { Key.U, "u" }, { Key.V, "v" }, { Key.W, "w" }, { Key.X, "x" },
            { Key.Y, "y" }, { Key.Z, "z" }
        };

        public bool TryMapKey(Key key, out string textElement)
        {
            return Map.TryGetValue(key, out textElement);
        }
    }

    /// <summary>
    /// Standard Israeli Hebrew layout mapped from physical QWERTY key positions.
    /// Works even when the OS input language is English.
    /// </summary>
    public sealed class HebrewPhysicalTextMapper : IPhysicalTextMapper
    {
        private static readonly Dictionary<Key, string> Map = new Dictionary<Key, string>
        {
            { Key.Q, "/" }, { Key.W, "'" }, { Key.E, "ק" }, { Key.R, "ר" },
            { Key.T, "א" }, { Key.Y, "ט" }, { Key.U, "ו" }, { Key.I, "ן" },
            { Key.O, "ם" }, { Key.P, "פ" },
            { Key.A, "ש" }, { Key.S, "ד" }, { Key.D, "ג" }, { Key.F, "כ" },
            { Key.G, "ע" }, { Key.H, "י" }, { Key.J, "ח" }, { Key.K, "ל" },
            { Key.L, "ך" }, { Key.Semicolon, "ף" },
            { Key.Z, "ז" }, { Key.X, "ס" }, { Key.C, "ב" }, { Key.V, "ה" },
            { Key.B, "נ" }, { Key.N, "מ" }, { Key.M, "צ" },
            { Key.Comma, "ת" }, { Key.Period, "ץ" }
        };

        public bool TryMapKey(Key key, out string textElement)
        {
            return Map.TryGetValue(key, out textElement);
        }
    }

    public static class PhysicalTextMapperFactory
    {
        public static IPhysicalTextMapper Create(string languageId)
        {
            if (languageId == "he")
                return new HebrewPhysicalTextMapper();
            return new EnglishPhysicalTextMapper();
        }
    }
}
