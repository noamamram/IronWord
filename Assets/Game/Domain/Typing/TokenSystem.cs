using System;
using System.Collections.Generic;

namespace TypingDefense.Domain.Typing
{
    public sealed class TokenEntry
    {
        public string Id { get; }
        public string Text { get; }
        public int DifficultyLevel { get; }
        public float Weight { get; }
        public IReadOnlyList<string> Tags { get; }
        public bool IsEnabled { get; }

        public TokenEntry(
            string id,
            string text,
            int difficultyLevel = 1,
            float weight = 1f,
            IReadOnlyList<string> tags = null,
            bool isEnabled = true)
        {
            Id = id ?? text;
            Text = text ?? string.Empty;
            DifficultyLevel = difficultyLevel;
            Weight = weight;
            Tags = tags ?? Array.Empty<string>();
            IsEnabled = isEnabled;
        }
    }

    public interface ITokenRepository
    {
        IReadOnlyList<TokenEntry> GetEnabledTokens();
        IReadOnlyList<TokenEntry> GetTokensByTag(string tag);
    }

    public sealed class InMemoryTokenRepository : ITokenRepository
    {
        private readonly List<TokenEntry> _tokens;

        public InMemoryTokenRepository(IEnumerable<TokenEntry> tokens)
        {
            _tokens = new List<TokenEntry>(tokens ?? Array.Empty<TokenEntry>());
        }

        public IReadOnlyList<TokenEntry> GetEnabledTokens()
        {
            var result = new List<TokenEntry>();
            for (var i = 0; i < _tokens.Count; i++)
            {
                if (_tokens[i].IsEnabled)
                    result.Add(_tokens[i]);
            }

            return result;
        }

        public IReadOnlyList<TokenEntry> GetTokensByTag(string tag)
        {
            var result = new List<TokenEntry>();
            for (var i = 0; i < _tokens.Count; i++)
            {
                var token = _tokens[i];
                if (!token.IsEnabled)
                    continue;

                for (var t = 0; t < token.Tags.Count; t++)
                {
                    if (string.Equals(token.Tags[t], tag, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(token);
                        break;
                    }
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Leases letters so active threats don't share the same glyph when avoidable.
    /// Uses a shuffled bag so each full pass through the alphabet feels evenly random.
    /// </summary>
    public sealed class TokenAllocator
    {
        private readonly ITokenRepository _repository;
        private readonly HashSet<string> _leasedNormalized;
        private readonly Languages.ILanguageRules _rules;
        private Random _random;
        private readonly List<TokenEntry> _drawBag = new List<TokenEntry>(32);

        public TokenAllocator(ITokenRepository repository, Languages.ILanguageRules rules, int seed)
        {
            _repository = repository;
            _rules = rules;
            _leasedNormalized = new HashSet<string>(StringComparer.Ordinal);
            _random = new Random(seed);
            RefillBag();
        }

        public bool TryAllocateLetter(out TokenEntry token)
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                while (_drawBag.Count > 0)
                {
                    var index = _drawBag.Count - 1;
                    var candidate = _drawBag[index];
                    _drawBag.RemoveAt(index);

                    var normalized = _rules.Normalize(candidate.Text);
                    if (_leasedNormalized.Contains(normalized))
                        continue;

                    _leasedNormalized.Add(normalized);
                    token = candidate;
                    return true;
                }

                RefillBag();
            }

            // Bag exhausted for leased set — allow a random duplicate as last resort.
            var all = _repository.GetTokensByTag("Letter");
            if (all.Count == 0)
                all = _repository.GetEnabledTokens();
            if (all.Count == 0)
            {
                token = null;
                return false;
            }

            token = all[_random.Next(all.Count)];
            _leasedNormalized.Add(_rules.Normalize(token.Text));
            return true;
        }

        /// <summary>
        /// Allocates <paramref name="length"/> distinct letters (when possible) and returns them as elements.
        /// </summary>
        public bool TryAllocateSequence(int length, out IReadOnlyList<string> elements)
        {
            length = Math.Max(1, length);
            var allocated = new List<string>(length);
            for (var i = 0; i < length; i++)
            {
                if (!TryAllocateLetter(out var token))
                {
                    for (var r = 0; r < allocated.Count; r++)
                        Release(allocated[r]);
                    elements = Array.Empty<string>();
                    return false;
                }

                allocated.Add(token.Text);
            }

            elements = allocated;
            return true;
        }

        public void Release(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // Multi-letter leases store concatenated glyphs — release each element.
            var elements = _rules.SplitIntoTextElements(text);
            if (elements.Count == 0)
            {
                _leasedNormalized.Remove(_rules.Normalize(text));
                return;
            }

            for (var i = 0; i < elements.Count; i++)
                _leasedNormalized.Remove(_rules.Normalize(elements[i]));
        }

        public void Clear()
        {
            _leasedNormalized.Clear();
            RefillBag();
        }

        /// <summary>
        /// Fresh RNG + full letter reshuffle. Call at the start of every run.
        /// </summary>
        public void ReshuffleForNewRun(int? seed = null)
        {
            var resolved = seed ?? (Environment.TickCount ^ Guid.NewGuid().GetHashCode());
            if (resolved == 0)
                resolved = Environment.TickCount ^ 0x5F3759DF;
            _leasedNormalized.Clear();
            _random = new Random(resolved);
            RefillBag();
        }

        private void RefillBag()
        {
            _drawBag.Clear();
            var source = _repository.GetTokensByTag("Letter");
            if (source.Count == 0)
                source = _repository.GetEnabledTokens();

            for (var i = 0; i < source.Count; i++)
                _drawBag.Add(source[i]);

            // Fisher–Yates shuffle
            for (var i = _drawBag.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                var tmp = _drawBag[i];
                _drawBag[i] = _drawBag[j];
                _drawBag[j] = tmp;
            }
        }
    }
}
