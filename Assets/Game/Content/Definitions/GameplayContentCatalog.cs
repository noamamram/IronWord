using TypingDefense.Content.GameModes;
using TypingDefense.Content.Languages;
using TypingDefense.Content.Threats;
using UnityEngine;

namespace TypingDefense.Content.Definitions
{
    [CreateAssetMenu(
        fileName = "GameplayContentCatalog",
        menuName = "IronWord/Content/Gameplay Content Catalog")]
    public sealed class GameplayContentCatalog : ScriptableObject
    {
        [SerializeField] private TrainingLanguageProfile _english;
        [SerializeField] private TrainingLanguageProfile _hebrew;
        [SerializeField] private GameModeDefinition _defaultMode;
        [SerializeField] private ThreatDefinition _basicThreat;
        [SerializeField] private ThreatDefinition _specialThreat;
        [SerializeField] private InterceptorDefinition _basicInterceptor;

        public TrainingLanguageProfile English => _english;
        public TrainingLanguageProfile Hebrew => _hebrew;
        public GameModeDefinition DefaultMode => _defaultMode;
        public ThreatDefinition BasicThreat => _basicThreat;
        public ThreatDefinition SpecialThreat => _specialThreat;
        public InterceptorDefinition BasicInterceptor => _basicInterceptor;

        public TrainingLanguageProfile ResolveLanguage(string languageId)
        {
            if (string.Equals(languageId, "he", System.StringComparison.OrdinalIgnoreCase) && _hebrew != null)
                return _hebrew;

            return _english;
        }
    }
}
