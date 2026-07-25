namespace TypingDefense.Application.Meta
{
    /// <summary>Phase D stub — unlocks, cosmetics, long-term progression.</summary>
    public interface IMetaProgressionService
    {
        bool IsUnlocked(string unlockId);
        void Grant(string unlockId);
    }

    public sealed class NullMetaProgressionService : IMetaProgressionService
    {
        public bool IsUnlocked(string unlockId) => true;
        public void Grant(string unlockId) { }
    }
}
