namespace TypingDefense.Domain.Typing
{
    public enum ChallengeInputOutcome
    {
        Ignored,
        Incorrect,
        CorrectProgress,
        Completed
    }

    public readonly struct ChallengeInputResult
    {
        public ChallengeInputOutcome Outcome { get; }
        public int Progress { get; }
        public int Length { get; }

        public ChallengeInputResult(ChallengeInputOutcome outcome, int progress, int length)
        {
            Outcome = outcome;
            Progress = progress;
            Length = length;
        }
    }

    public interface ITypingChallenge
    {
        string DisplayText { get; }
        string RemainingText { get; }
        int Progress { get; }
        int Length { get; }
        bool IsCompleted { get; }

        /// <summary>True if this challenge still has a remaining element matching the input.</summary>
        bool Accepts(string textElement);

        ChallengeInputResult Submit(string textElement);
        void Reset();
    }
}
