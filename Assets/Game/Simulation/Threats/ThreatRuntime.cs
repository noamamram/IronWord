using TypingDefense.Domain.Common;
using TypingDefense.Domain.Sessions;
using TypingDefense.Domain.Typing;
using TypingDefense.Simulation.Trajectories;
using UnityEngine;

namespace TypingDefense.Simulation.Threats
{
    public sealed class ThreatRuntime
    {
        public ThreatId Id { get; }
        public DefinitionId DefinitionId { get; private set; }
        public ThreatLifecycleState LifecycleState { get; private set; }
        public SensorTrackState TrackState { get; private set; }
        public TypingTrackState TypingState { get; private set; }

        public float FlightProgress { get; private set; }
        public float FlightDuration { get; private set; }
        public float TimeToImpact => Mathf.Max(0f, (1f - FlightProgress) * FlightDuration);
        public float RemainingTimeNormalized => FlightDuration > 0f ? TimeToImpact / FlightDuration : 0f;

        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public ITrajectory Trajectory { get; private set; }
        public ITypingChallenge Challenge { get; private set; }
        public string LeasedTokenText { get; private set; }
        public float SpawnedAtElapsed { get; private set; }
        public int BaseScore { get; private set; }

        public ThreatRuntime(ThreatId id)
        {
            Id = id;
            LifecycleState = ThreatLifecycleState.Pooled;
        }

        public void Activate(
            DefinitionId definitionId,
            ITrajectory trajectory,
            float flightDuration,
            ITypingChallenge challenge,
            string leasedTokenText,
            float spawnedAtElapsed,
            int baseScore)
        {
            DefinitionId = definitionId;
            Trajectory = trajectory;
            FlightDuration = Mathf.Max(0.1f, flightDuration);
            FlightProgress = 0f;
            Challenge = challenge;
            LeasedTokenText = leasedTokenText;
            SpawnedAtElapsed = spawnedAtElapsed;
            BaseScore = baseScore;
            LifecycleState = ThreatLifecycleState.Active;
            TrackState = SensorTrackState.Undetected;
            TypingState = challenge != null ? TypingTrackState.Available : TypingTrackState.Unassigned;
            Position = trajectory.EvaluatePosition(0f);
            Velocity = trajectory.EvaluateVelocity(0f);
        }

        public void Tick(float deltaSeconds)
        {
            if (LifecycleState != ThreatLifecycleState.Active &&
                LifecycleState != ThreatLifecycleState.Engaged)
                return;

            FlightProgress = Mathf.Clamp01(FlightProgress + deltaSeconds / FlightDuration);
            Position = Trajectory.EvaluatePosition(FlightProgress);
            Velocity = Trajectory.EvaluateVelocity(FlightProgress);
        }

        public bool HasReachedImpact()
        {
            return FlightProgress >= 1f - 0.0001f &&
                   (LifecycleState == ThreatLifecycleState.Active ||
                    LifecycleState == ThreatLifecycleState.Engaged);
        }

        public void SetTracked(bool tracked)
        {
            if (LifecycleState != ThreatLifecycleState.Active &&
                LifecycleState != ThreatLifecycleState.Engaged)
                return;

            TrackState = tracked ? SensorTrackState.Tracked : SensorTrackState.Lost;
        }

        public void SetTypingLocked()
        {
            if (TypingState == TypingTrackState.Available)
                TypingState = TypingTrackState.Locked;
        }

        public void SetTypingCompleted()
        {
            TypingState = TypingTrackState.Completed;
        }

        public bool TryReserveForInterception()
        {
            if (LifecycleState != ThreatLifecycleState.Active)
                return false;

            LifecycleState = ThreatLifecycleState.Engaged;
            TypingState = TypingTrackState.Completed;
            return true;
        }

        public void MarkIntercepted()
        {
            LifecycleState = ThreatLifecycleState.Intercepted;
        }

        public void MarkImpacted()
        {
            LifecycleState = ThreatLifecycleState.Impacted;
        }

        public void Recycle()
        {
            LifecycleState = ThreatLifecycleState.Recycled;
            TrackState = SensorTrackState.Undetected;
            TypingState = TypingTrackState.Unassigned;
            Challenge = null;
            Trajectory = null;
            LeasedTokenText = null;
            FlightProgress = 0f;
            Position = Vector3.zero;
            Velocity = Vector3.zero;
        }

        public void ReturnToPool()
        {
            Recycle();
            LifecycleState = ThreatLifecycleState.Pooled;
        }
    }
}
