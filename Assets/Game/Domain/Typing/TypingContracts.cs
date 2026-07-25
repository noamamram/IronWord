namespace TypingDefense.Domain.Typing
{
    public interface ILanguageComparer
    {
        bool AreEqual(string left, string right);
    }

    public interface ITextInputSource
    {
        event System.Action<string> TextCommitted;
        event System.Action BackspacePressed;
    }

    public enum TypingErrorPolicy
    {
        IgnoreIncorrect,
        ResetProgress,
        UnlockTarget,
        ResetCombo
    }
}
