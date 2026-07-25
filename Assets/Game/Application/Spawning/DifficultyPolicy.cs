using TypingDefense.Domain.Statistics;

namespace TypingDefense.Application.Spawning
{
    public readonly struct DifficultySnapshot
    {
        public float SpawnRatePerSecond { get; }
        public int MaxConcurrentThreats { get; }
        public float SpeedMultiplier { get; }
        public int MaxTokenLength { get; }
        public float SpecialThreatChance { get; }

        public DifficultySnapshot(
            float spawnRatePerSecond,
            int maxConcurrentThreats,
            float speedMultiplier,
            int maxTokenLength,
            float specialThreatChance)
        {
            SpawnRatePerSecond = spawnRatePerSecond;
            MaxConcurrentThreats = maxConcurrentThreats;
            SpeedMultiplier = speedMultiplier;
            MaxTokenLength = maxTokenLength;
            SpecialThreatChance = specialThreatChance;
        }
    }

    public interface IDifficultyPolicy
    {
        DifficultySnapshot Evaluate(float elapsedSeconds, PerformanceSnapshot performance);
    }

    public readonly struct PerformanceSnapshot
    {
        public float Accuracy { get; }
        public float AverageReactionSeconds { get; }

        public PerformanceSnapshot(float accuracy, float averageReactionSeconds)
        {
            Accuracy = accuracy;
            AverageReactionSeconds = averageReactionSeconds;
        }

        public static PerformanceSnapshot From(TrainingStatsCollector stats)
        {
            return new PerformanceSnapshot(stats.Accuracy, stats.AverageReactionSeconds);
        }
    }

    /// <summary>
    /// Aggressive time-based difficulty — hard from the start, steep ramp.
    /// </summary>
    public sealed class CurveDifficultyPolicy : IDifficultyPolicy
    {
        private readonly float _baseSpawnRate;
        private readonly float _spawnRatePerMinute;
        private readonly int _baseMaxConcurrent;
        private readonly int _maxConcurrentCap;
        private readonly float _baseSpeed;
        private readonly float _speedPerMinute;

        public CurveDifficultyPolicy(
            float baseSpawnRate = 0.65f,
            float spawnRatePerMinute = 0.45f,
            int baseMaxConcurrent = 5,
            int maxConcurrentCap = 12,
            float baseSpeed = 1.25f,
            float speedPerMinute = 0.22f)
        {
            _baseSpawnRate = baseSpawnRate;
            _spawnRatePerMinute = spawnRatePerMinute;
            _baseMaxConcurrent = baseMaxConcurrent;
            _maxConcurrentCap = maxConcurrentCap;
            _baseSpeed = baseSpeed;
            _speedPerMinute = speedPerMinute;
        }

        public DifficultySnapshot Evaluate(float elapsedSeconds, PerformanceSnapshot performance)
        {
            var minutes = elapsedSeconds / 60f;
            var spawnRate = _baseSpawnRate + minutes * _spawnRatePerMinute;
            var maxConcurrent = System.Math.Min(
                _maxConcurrentCap,
                _baseMaxConcurrent + (int)(minutes * 2.5f));
            var speed = _baseSpeed + minutes * _speedPerMinute;

            return new DifficultySnapshot(
                spawnRate,
                maxConcurrent,
                speed,
                maxTokenLength: ResolveMaxTokenLength(elapsedSeconds),
                specialThreatChance: Clamp01(0.28f + minutes * 0.35f));
        }

        /// <summary>
        /// Multi-letter ceiling: 2 from start, 3 after 20s, 4 after 40s.
        /// </summary>
        private static int ResolveMaxTokenLength(float elapsedSeconds)
        {
            if (elapsedSeconds < 20f)
                return 2;
            if (elapsedSeconds < 40f)
                return 3;
            return 4;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
