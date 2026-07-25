namespace TypingDefense.Domain.Content
{
    /// <summary>Phase D stub — tagged content queries beyond letter bags.</summary>
    public interface IContentCatalog
    {
        bool TryGetDefinition(string id, out object definition);
    }
}
