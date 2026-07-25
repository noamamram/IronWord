using System.Collections.Generic;
using TypingDefense.Domain.Common;
using TypingDefense.Domain.Sessions;
using TypingDefense.Domain.Typing;
using TypingDefense.Simulation.Threats;

namespace TypingDefense.Application.Targeting
{
    public sealed class TypingTarget
    {
        public ThreatId Id { get; }
        public ITypingChallenge Challenge { get; }
        public float TimeToImpact { get; }
        public float SpawnedAtElapsed { get; }
        public float Distance { get; }

        public TypingTarget(
            ThreatId id,
            ITypingChallenge challenge,
            float timeToImpact,
            float spawnedAtElapsed,
            float distance)
        {
            Id = id;
            Challenge = challenge;
            TimeToImpact = timeToImpact;
            SpawnedAtElapsed = spawnedAtElapsed;
            Distance = distance;
        }
    }

    public interface ITypingTargetSelector
    {
        TypingTarget Select(
            string textElement,
            TypingTarget lockedTarget,
            IReadOnlyList<TypingTarget> candidates);
    }

    public sealed class PriorityTypingTargetSelector : ITypingTargetSelector
    {
        public TypingTarget Select(
            string textElement,
            TypingTarget lockedTarget,
            IReadOnlyList<TypingTarget> candidates)
        {
            // Prefer the locked threat only when it still accepts this letter.
            if (lockedTarget != null &&
                lockedTarget.Challenge != null &&
                !lockedTarget.Challenge.IsCompleted &&
                lockedTarget.Challenge.Accepts(textElement))
            {
                return lockedTarget;
            }

            TypingTarget best = null;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate?.Challenge == null || candidate.Challenge.IsCompleted)
                    continue;

                if (!candidate.Challenge.Accepts(textElement))
                    continue;

                if (best == null || IsHigherPriority(candidate, best))
                    best = candidate;
            }

            return best;
        }

        private static bool IsHigherPriority(TypingTarget candidate, TypingTarget currentBest)
        {
            if (candidate.TimeToImpact < currentBest.TimeToImpact)
                return true;
            if (candidate.TimeToImpact > currentBest.TimeToImpact)
                return false;
            if (candidate.SpawnedAtElapsed < currentBest.SpawnedAtElapsed)
                return true;
            if (candidate.SpawnedAtElapsed > currentBest.SpawnedAtElapsed)
                return false;
            return candidate.Distance < currentBest.Distance;
        }
    }

    public static class TypingTargetFactory
    {
        public static List<TypingTarget> BuildFromRegistry(
            IThreatRegistry registry,
            UnityEngine.Vector3 sensorOrigin)
        {
            var list = new List<TypingTarget>();
            var threats = registry.ActiveThreats;
            for (var i = 0; i < threats.Count; i++)
            {
                var threat = threats[i];
                if (threat.LifecycleState != ThreatLifecycleState.Active)
                    continue;
                if (threat.TrackState != SensorTrackState.Tracked)
                    continue;
                if (threat.TypingState != TypingTrackState.Available &&
                    threat.TypingState != TypingTrackState.Locked)
                    continue;
                if (threat.Challenge == null || threat.Challenge.IsCompleted)
                    continue;

                var distance = UnityEngine.Vector3.Distance(sensorOrigin, threat.Position);
                list.Add(new TypingTarget(
                    threat.Id,
                    threat.Challenge,
                    threat.TimeToImpact,
                    threat.SpawnedAtElapsed,
                    distance));
            }

            return list;
        }
    }
}
