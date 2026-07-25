using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TypingDefense.Domain.Languages
{
    public enum TextDirection
    {
        LeftToRight,
        RightToLeft
    }

    public interface ILanguageRules
    {
        string LanguageId { get; }
        TextDirection Direction { get; }
        string Normalize(string value);
        IReadOnlyList<string> SplitIntoTextElements(string normalizedValue);
        bool IsAllowed(string textElement);
        bool AreEqual(string left, string right);
    }

    public sealed class EnglishLanguageRules : ILanguageRules, Typing.ILanguageComparer
    {
        private readonly HashSet<string> _allowed;

        public EnglishLanguageRules(IEnumerable<string> allowedCharacters)
        {
            LanguageId = "en";
            Direction = TextDirection.LeftToRight;
            _allowed = new HashSet<string>(StringComparer.Ordinal);
            if (allowedCharacters != null)
            {
                foreach (var c in allowedCharacters)
                {
                    var normalized = Normalize(c);
                    if (!string.IsNullOrEmpty(normalized))
                        _allowed.Add(normalized);
                }
            }
        }

        public string LanguageId { get; }
        public TextDirection Direction { get; }

        public string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Trim().ToLowerInvariant();
        }

        public IReadOnlyList<string> SplitIntoTextElements(string normalizedValue)
        {
            if (string.IsNullOrEmpty(normalizedValue))
                return Array.Empty<string>();

            var list = new List<string>(normalizedValue.Length);
            var enumerator = StringInfo.GetTextElementEnumerator(normalizedValue);
            while (enumerator.MoveNext())
            {
                var element = enumerator.GetTextElement();
                if (!string.IsNullOrEmpty(element))
                    list.Add(element);
            }

            return list;
        }

        public bool IsAllowed(string textElement)
        {
            if (string.IsNullOrEmpty(textElement))
                return false;

            if (_allowed.Count == 0)
                return textElement.Length == 1 && char.IsLetter(textElement[0]);

            return _allowed.Contains(Normalize(textElement));
        }

        public bool AreEqual(string left, string right)
        {
            return string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Hebrew letter rules: final-letter mapping, optional niqqud strip, RTL direction.
    /// </summary>
    public sealed class HebrewLanguageRules : ILanguageRules, Typing.ILanguageComparer
    {
        private static readonly Dictionary<char, char> FinalLetterMap = new Dictionary<char, char>
        {
            { 'ך', 'כ' },
            { 'ם', 'מ' },
            { 'ן', 'נ' },
            { 'ף', 'פ' },
            { 'ץ', 'צ' }
        };

        private readonly HashSet<string> _allowed;
        private readonly bool _mapFinalLetters;
        private readonly bool _stripDiacritics;

        public HebrewLanguageRules(
            IEnumerable<string> allowedCharacters,
            bool mapFinalLetters = true,
            bool stripDiacritics = true)
        {
            LanguageId = "he";
            Direction = TextDirection.RightToLeft;
            _mapFinalLetters = mapFinalLetters;
            _stripDiacritics = stripDiacritics;
            _allowed = new HashSet<string>(StringComparer.Ordinal);
            if (allowedCharacters != null)
            {
                foreach (var c in allowedCharacters)
                {
                    var normalized = Normalize(c);
                    if (!string.IsNullOrEmpty(normalized))
                        _allowed.Add(normalized);
                }
            }
        }

        public string LanguageId { get; }
        public TextDirection Direction { get; }

        public string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            foreach (var ch in value.Normalize(NormalizationForm.FormC))
            {
                if (_stripDiacritics && IsHebrewDiacritic(ch))
                    continue;

                var mapped = ch;
                if (_mapFinalLetters && FinalLetterMap.TryGetValue(ch, out var baseLetter))
                    mapped = baseLetter;

                builder.Append(mapped);
            }

            return builder.ToString().Trim();
        }

        public IReadOnlyList<string> SplitIntoTextElements(string normalizedValue)
        {
            if (string.IsNullOrEmpty(normalizedValue))
                return Array.Empty<string>();

            var list = new List<string>(normalizedValue.Length);
            var enumerator = StringInfo.GetTextElementEnumerator(normalizedValue);
            while (enumerator.MoveNext())
            {
                var element = enumerator.GetTextElement();
                if (!string.IsNullOrEmpty(element))
                    list.Add(element);
            }

            return list;
        }

        public bool IsAllowed(string textElement)
        {
            if (string.IsNullOrEmpty(textElement))
                return false;

            var normalized = Normalize(textElement);
            if (_allowed.Count == 0)
                return normalized.Length > 0;

            return _allowed.Contains(normalized);
        }

        public bool AreEqual(string left, string right)
        {
            return string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
        }

        private static bool IsHebrewDiacritic(char ch)
        {
            return ch >= '\u0591' && ch <= '\u05C7';
        }
    }
}
