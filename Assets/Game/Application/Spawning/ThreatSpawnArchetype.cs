using System;
using TypingDefense.Domain.Common;
using UnityEngine;

namespace TypingDefense.Application.Spawning
{
    /// <summary>
    /// Content-agnostic spawn archetype. Composition root maps ScriptableObjects into these.
    /// </summary>
    public sealed class ThreatSpawnArchetype
    {
        public DefinitionId DefinitionId { get; }
        public float BaseFlightDuration { get; }
        public float MinFlightDuration { get; }
        public float ArcHeight { get; }
        public int BaseScore { get; }
        public float SpawnCost { get; }
        public float SelectionWeight { get; }
        public int MinTokenLength { get; }
        public int MaxTokenLength { get; }
        public float UnlockAfterSeconds { get; }
        public float WeightGrowthPerMinute { get; }

        public ThreatSpawnArchetype(
            DefinitionId definitionId,
            float baseFlightDuration = 12f,
            float minFlightDuration = 6f,
            float arcHeight = 40f,
            int baseScore = 100,
            float spawnCost = 1f,
            float selectionWeight = 1f,
            int minTokenLength = 1,
            int maxTokenLength = 1,
            float unlockAfterSeconds = 0f,
            float weightGrowthPerMinute = 0f)
        {
            DefinitionId = definitionId;
            BaseFlightDuration = baseFlightDuration;
            MinFlightDuration = minFlightDuration;
            ArcHeight = arcHeight;
            BaseScore = baseScore;
            SpawnCost = Mathf.Max(0.1f, spawnCost);
            SelectionWeight = Mathf.Max(0f, selectionWeight);
            MinTokenLength = Mathf.Max(1, minTokenLength);
            MaxTokenLength = Mathf.Max(MinTokenLength, maxTokenLength);
            UnlockAfterSeconds = Mathf.Max(0f, unlockAfterSeconds);
            WeightGrowthPerMinute = Mathf.Max(0f, weightGrowthPerMinute);
        }

        public float EvaluateWeight(float elapsedSeconds)
        {
            if (elapsedSeconds < UnlockAfterSeconds)
                return 0f;

            var minutesAfterUnlock = (elapsedSeconds - UnlockAfterSeconds) / 60f;
            return SelectionWeight + minutesAfterUnlock * WeightGrowthPerMinute;
        }

        /// <summary>
        /// Rolls a length in [MinTokenLength, Min(MaxTokenLength, difficultyCeiling)].
        /// Returns false when the difficulty ceiling is below this archetype's minimum.
        /// </summary>
        public bool TryRollTokenLength(System.Random random, int difficultyCeiling, out int length)
        {
            length = MinTokenLength;
            var maxAllowed = Math.Min(MaxTokenLength, Math.Max(1, difficultyCeiling));
            if (maxAllowed < MinTokenLength)
                return false;

            if (MinTokenLength >= maxAllowed)
            {
                length = MinTokenLength;
                return true;
            }

            length = random.Next(MinTokenLength, maxAllowed + 1);
            return true;
        }
    }
}
