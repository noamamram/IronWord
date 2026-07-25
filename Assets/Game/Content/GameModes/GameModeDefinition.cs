using TypingDefense.Domain.Sessions;
using UnityEngine;

namespace TypingDefense.Content.GameModes
{
    [CreateAssetMenu(
        fileName = "GameModeDefinition",
        menuName = "IronWord/Game Modes/Game Mode Definition")]
    public sealed class GameModeDefinition : ScriptableObject
    {
        [SerializeField] private string _id = "letter_defense";
        [SerializeField] private string _displayName = "Letter Defense";
        [SerializeField] private int _maxStrikes = 3;
        [SerializeField] private float _countdownSeconds = 3f;
        [SerializeField] private bool _endOnMaxStrikes = true;
        [SerializeField] private float _timedDurationSeconds = 0f;

        public string Id => _id;
        public string DisplayName => _displayName;

        public SessionConfig CreateSessionConfig()
        {
            return new SessionConfig
            {
                MaxStrikes = _maxStrikes,
                CountdownSeconds = _countdownSeconds,
                EndOnMaxStrikes = _endOnMaxStrikes,
                TimedDurationSeconds = _timedDurationSeconds > 0f
                    ? _timedDurationSeconds
                    : (float?)null
            };
        }
    }
}
