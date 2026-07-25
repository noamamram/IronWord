using System.Collections.Generic;
using TypingDefense.Domain.Languages;
using TypingDefense.Domain.Typing;
using UnityEngine;

namespace TypingDefense.Content.Languages
{
    [CreateAssetMenu(
        fileName = "TrainingLanguageProfile",
        menuName = "IronWord/Languages/Training Language Profile")]
    public sealed class TrainingLanguageProfile : ScriptableObject
    {
        [SerializeField] private string _id = "en";
        [SerializeField] private string _displayName = "English";
        [SerializeField] private TextDirection _direction = TextDirection.LeftToRight;
        [SerializeField] private string[] _allowedCharacters;
        [SerializeField] private TokenEntryData[] _tokens;
        [SerializeField] private bool _caseInsensitive = true;
        [SerializeField] private bool _mapHebrewFinalLetters = true;
        [SerializeField] private bool _stripHebrewDiacritics = true;

        public string Id => _id;
        public string DisplayName => _displayName;
        public TextDirection Direction => _direction;

        public ILanguageRules CreateRules()
        {
            if (_id == "he")
            {
                return new HebrewLanguageRules(
                    _allowedCharacters,
                    _mapHebrewFinalLetters,
                    _stripHebrewDiacritics);
            }

            return new EnglishLanguageRules(_allowedCharacters);
        }

        public ITokenRepository CreateTokenRepository()
        {
            var entries = new List<TokenEntry>();
            if (_tokens != null)
            {
                for (var i = 0; i < _tokens.Length; i++)
                {
                    var data = _tokens[i];
                    if (data == null || string.IsNullOrWhiteSpace(data.Text))
                        continue;

                    entries.Add(new TokenEntry(
                        string.IsNullOrEmpty(data.Id) ? data.Text : data.Id,
                        data.Text,
                        data.DifficultyLevel,
                        data.Weight,
                        data.Tags,
                        data.IsEnabled));
                }
            }

            return new InMemoryTokenRepository(entries);
        }

        public void ApplyEnglishDefaults()
        {
            _id = "en";
            _displayName = "English";
            _direction = TextDirection.LeftToRight;
            _caseInsensitive = true;

            var letters = new string[26];
            var tokens = new TokenEntryData[26];
            for (var i = 0; i < 26; i++)
            {
                var letter = ((char)('a' + i)).ToString();
                letters[i] = letter;
                tokens[i] = new TokenEntryData
                {
                    Id = letter,
                    Text = letter,
                    DifficultyLevel = 1,
                    Weight = 1f,
                    Tags = new[] { "Letter" },
                    IsEnabled = true
                };
            }

            _allowedCharacters = letters;
            _tokens = tokens;
        }

        public void ApplyHebrewDefaults()
        {
            _id = "he";
            _displayName = "עברית";
            _direction = TextDirection.RightToLeft;
            _caseInsensitive = false;
            _mapHebrewFinalLetters = true;
            _stripHebrewDiacritics = true;

            // Common Hebrew consonants/vowels used for letter-drill training.
            var letters = new[]
            {
                "א", "ב", "ג", "ד", "ה", "ו", "ז", "ח", "ט", "י",
                "כ", "ל", "מ", "נ", "ס", "ע", "פ", "צ", "ק", "ר", "ש", "ת"
            };

            var tokens = new TokenEntryData[letters.Length];
            for (var i = 0; i < letters.Length; i++)
            {
                tokens[i] = new TokenEntryData
                {
                    Id = letters[i],
                    Text = letters[i],
                    DifficultyLevel = 1,
                    Weight = 1f,
                    Tags = new[] { "Letter" },
                    IsEnabled = true
                };
            }

            _allowedCharacters = letters;
            _tokens = tokens;
        }
    }

    [System.Serializable]
    public sealed class TokenEntryData
    {
        public string Id;
        public string Text;
        public int DifficultyLevel = 1;
        public float Weight = 1f;
        public string[] Tags = { "Letter" };
        public bool IsEnabled = true;
    }
}
