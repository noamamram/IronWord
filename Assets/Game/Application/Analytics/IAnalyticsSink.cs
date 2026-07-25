namespace TypingDefense.Application.Analytics
{
    /// <summary>Phase D stub — session / funnel analytics.</summary>
    public interface IAnalyticsSink
    {
        void Track(string eventName, string payloadJson = null);
    }

    public sealed class NullAnalyticsSink : IAnalyticsSink
    {
        public void Track(string eventName, string payloadJson = null) { }
    }
}
