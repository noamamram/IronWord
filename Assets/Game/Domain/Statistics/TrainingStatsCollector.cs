using System;
using System.Collections.Generic;

namespace TypingDefense.Domain.Statistics
{
    public sealed class TrainingStatsCollector
    {
        private int _correctInputs;
        private int _incorrectInputs;
        private int _currentStreak;
        private int _maxStreak;
        private float _totalReactionSeconds;
        private int _reactionSamples;
        private readonly Dictionary<string, int> _mistakesByToken = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _hitsByToken = new Dictionary<string, int>(StringComparer.Ordinal);

        public int CorrectInputs => _correctInputs;
        public int IncorrectInputs => _incorrectInputs;
        public int MaxStreak => _maxStreak;
        public float AverageReactionSeconds =>
            _reactionSamples > 0 ? _totalReactionSeconds / _reactionSamples : 0f;

        public float Accuracy
        {
            get
            {
                var total = _correctInputs + _incorrectInputs;
                return total > 0 ? (float)_correctInputs / total : 1f;
            }
        }

        public IReadOnlyDictionary<string, int> MistakesByToken => _mistakesByToken;
        public IReadOnlyDictionary<string, int> HitsByToken => _hitsByToken;

        public void Reset()
        {
            _correctInputs = 0;
            _incorrectInputs = 0;
            _currentStreak = 0;
            _maxStreak = 0;
            _totalReactionSeconds = 0f;
            _reactionSamples = 0;
            _mistakesByToken.Clear();
            _hitsByToken.Clear();
        }

        public void RegisterCorrect(string token, float reactionSeconds)
        {
            _correctInputs++;
            _currentStreak++;
            if (_currentStreak > _maxStreak)
                _maxStreak = _currentStreak;

            _totalReactionSeconds += Math.Max(0f, reactionSeconds);
            _reactionSamples++;

            if (string.IsNullOrEmpty(token))
                return;

            _hitsByToken.TryGetValue(token, out var count);
            _hitsByToken[token] = count + 1;
        }

        public void RegisterIncorrect(string token)
        {
            _incorrectInputs++;
            _currentStreak = 0;

            if (string.IsNullOrEmpty(token))
                return;

            _mistakesByToken.TryGetValue(token, out var count);
            _mistakesByToken[token] = count + 1;
        }
    }
}
