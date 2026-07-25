using System;
using TypingDefense.Domain.Common;
using UnityEngine;

namespace TypingDefense.Application.Events
{
    public sealed class GameplayEventBus
    {
        public event Action<ThreatId> ThreatSpawned;
        public event Action<ThreatId> ThreatTracked;
        public event Action<ThreatId, int, int> TypingProgressChanged;
        public event Action<ThreatId> ThreatEngaged;
        public event Action<ThreatId, InterceptorId, Vector3> InterceptorLaunched;
        public event Action<ThreatId, Vector3> ThreatIntercepted;
        public event Action<ThreatId, Vector3> ThreatImpacted;
        public event Action<int, int> ScoreChanged;
        public event Action<int> StrikeCountChanged;
        public event Action SessionEnded;
        public event Action<string> MistakeReported;
        public event Action DefenseHitFeedback;
        public event Action<Domain.Sessions.SessionPhase> PhaseChanged;

        public void PublishThreatSpawned(ThreatId id) => ThreatSpawned?.Invoke(id);
        public void PublishThreatTracked(ThreatId id) => ThreatTracked?.Invoke(id);
        public void PublishTypingProgress(ThreatId id, int progress, int length) =>
            TypingProgressChanged?.Invoke(id, progress, length);
        public void PublishThreatEngaged(ThreatId id) => ThreatEngaged?.Invoke(id);
        public void PublishInterceptorLaunched(ThreatId threatId, InterceptorId interceptorId, Vector3 worldPosition) =>
            InterceptorLaunched?.Invoke(threatId, interceptorId, worldPosition);
        public void PublishThreatIntercepted(ThreatId id, Vector3 worldPosition) =>
            ThreatIntercepted?.Invoke(id, worldPosition);
        public void PublishThreatImpacted(ThreatId id, Vector3 worldPosition) =>
            ThreatImpacted?.Invoke(id, worldPosition);
        public void PublishScoreChanged(int score, int combo) => ScoreChanged?.Invoke(score, combo);
        public void PublishStrikeCountChanged(int strikes) => StrikeCountChanged?.Invoke(strikes);
        public void PublishSessionEnded() => SessionEnded?.Invoke();
        public void PublishMistake(string text) => MistakeReported?.Invoke(text);
        public void PublishDefenseHitFeedback() => DefenseHitFeedback?.Invoke();
        public void PublishPhaseChanged(Domain.Sessions.SessionPhase phase) => PhaseChanged?.Invoke(phase);

        public void ClearAll()
        {
            ThreatSpawned = null;
            ThreatTracked = null;
            TypingProgressChanged = null;
            ThreatEngaged = null;
            InterceptorLaunched = null;
            ThreatIntercepted = null;
            ThreatImpacted = null;
            ScoreChanged = null;
            StrikeCountChanged = null;
            SessionEnded = null;
            MistakeReported = null;
            DefenseHitFeedback = null;
            PhaseChanged = null;
        }
    }
}
