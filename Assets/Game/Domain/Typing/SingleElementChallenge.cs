namespace TypingDefense.Domain.Typing
{
    public sealed class SingleElementChallenge : ITypingChallenge
    {
        private readonly string _element;
        private readonly ILanguageComparer _comparer;
        private bool _completed;

        public SingleElementChallenge(string element, ILanguageComparer comparer)
        {
            _element = element ?? string.Empty;
            _comparer = comparer;
            Progress = 0;
            Length = 1;
        }

        public string DisplayText => _element;
        public string RemainingText => _completed ? string.Empty : _element;
        public int Progress { get; private set; }
        public int Length { get; }
        public bool IsCompleted => _completed;

        public bool Accepts(string textElement) =>
            !_completed && _comparer.AreEqual(_element, textElement);

        public ChallengeInputResult Submit(string textElement)
        {
            if (_completed)
                return new ChallengeInputResult(ChallengeInputOutcome.Ignored, Progress, Length);

            if (!_comparer.AreEqual(_element, textElement))
                return new ChallengeInputResult(ChallengeInputOutcome.Incorrect, Progress, Length);

            Progress = 1;
            _completed = true;
            return new ChallengeInputResult(ChallengeInputOutcome.Completed, Progress, Length);
        }

        public void Reset()
        {
            Progress = 0;
            _completed = false;
        }
    }
}
