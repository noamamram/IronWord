using System;
using System.Collections.Generic;
using System.Text;

namespace TypingDefense.Domain.Typing
{
    /// <summary>
    /// Multi-letter challenge — type remaining elements in any order; each match removes that letter.
    /// </summary>
    public sealed class SequenceChallenge : ITypingChallenge
    {
        private readonly List<string> _original;
        private readonly List<string> _remaining;
        private readonly ILanguageComparer _comparer;

        public SequenceChallenge(IReadOnlyList<string> elements, ILanguageComparer comparer)
        {
            if (elements == null || elements.Count == 0)
                throw new ArgumentException("Sequence challenge requires at least one element.", nameof(elements));

            _original = new List<string>(elements.Count);
            _remaining = new List<string>(elements.Count);
            for (var i = 0; i < elements.Count; i++)
            {
                _original.Add(elements[i]);
                _remaining.Add(elements[i]);
            }

            _comparer = comparer;
            Length = elements.Count;
        }

        public string DisplayText
        {
            get
            {
                var sb = new StringBuilder(Length);
                for (var i = 0; i < _original.Count; i++)
                    sb.Append(_original[i]);
                return sb.ToString();
            }
        }

        public string RemainingText
        {
            get
            {
                if (_remaining.Count == 0)
                    return string.Empty;

                var sb = new StringBuilder(_remaining.Count);
                for (var i = 0; i < _remaining.Count; i++)
                    sb.Append(_remaining[i]);
                return sb.ToString();
            }
        }

        public int Progress => Length - _remaining.Count;
        public int Length { get; }
        public bool IsCompleted => _remaining.Count == 0;

        public bool Accepts(string textElement)
        {
            if (IsCompleted)
                return false;

            for (var i = 0; i < _remaining.Count; i++)
            {
                if (_comparer.AreEqual(_remaining[i], textElement))
                    return true;
            }

            return false;
        }

        public ChallengeInputResult Submit(string textElement)
        {
            if (IsCompleted)
                return new ChallengeInputResult(ChallengeInputOutcome.Ignored, Progress, Length);

            var matchIndex = -1;
            for (var i = 0; i < _remaining.Count; i++)
            {
                if (_comparer.AreEqual(_remaining[i], textElement))
                {
                    matchIndex = i;
                    break;
                }
            }

            if (matchIndex < 0)
                return new ChallengeInputResult(ChallengeInputOutcome.Incorrect, Progress, Length);

            _remaining.RemoveAt(matchIndex);
            if (IsCompleted)
                return new ChallengeInputResult(ChallengeInputOutcome.Completed, Progress, Length);

            return new ChallengeInputResult(ChallengeInputOutcome.CorrectProgress, Progress, Length);
        }

        public void Reset()
        {
            _remaining.Clear();
            for (var i = 0; i < _original.Count; i++)
                _remaining.Add(_original[i]);
        }
    }
}
