namespace TypingDefense.Application.Persistence
{
    /// <summary>Phase D stub — save / load player progress.</summary>
    public interface ISaveStore
    {
        bool TryLoad(string key, out string json);
        void Save(string key, string json);
        void Delete(string key);
    }

    public sealed class NullSaveStore : ISaveStore
    {
        public bool TryLoad(string key, out string json)
        {
            json = null;
            return false;
        }

        public void Save(string key, string json) { }
        public void Delete(string key) { }
    }
}
