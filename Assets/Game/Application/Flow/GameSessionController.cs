using System;
using TypingDefense.Application.Events;
using TypingDefense.Application.Gameplay;
using TypingDefense.Application.Spawning;
using TypingDefense.Domain.Common;
using TypingDefense.Domain.Scoring;
using TypingDefense.Domain.Sessions;
using TypingDefense.Domain.Statistics;
using TypingDefense.Domain.Typing;
using TypingDefense.Simulation.Interceptors;
using TypingDefense.Simulation.Threats;

namespace TypingDefense.Application.Flow
{
    public sealed class GameSessionController : IDisposable
    {
        private readonly SessionState _session;
        private readonly SessionConfig _config;
        private readonly ThreatSimulation _threatSimulation;
        private readonly SensorSystem _sensorSystem;
        private readonly InterceptorSimulation _interceptorSimulation;
        private readonly ThreatSpawnDirector _spawnDirector;
        private readonly LauncherService _launcher;
        private readonly TypingCoordinator _typingCoordinator;
        private readonly IThreatRegistry _threatRegistry;
        private readonly TokenAllocator _tokenAllocator;
        private readonly ScoreService _scoreService;
        private readonly TrainingStatsCollector _stats;
        private readonly GameplayEventBus _events;
        private readonly Action<ThreatRuntime> _releaseThreat;
        private readonly Action<InterceptorRuntime> _releaseInterceptor;

        private SessionPhase _lastPublishedPhase;

        public SessionState Session => _session;
        public ScoreService Score => _scoreService;
        public TrainingStatsCollector Stats => _stats;

        public GameSessionController(
            SessionState session,
            SessionConfig config,
            ThreatSimulation threatSimulation,
            SensorSystem sensorSystem,
            InterceptorSimulation interceptorSimulation,
            ThreatSpawnDirector spawnDirector,
            LauncherService launcher,
            TypingCoordinator typingCoordinator,
            IThreatRegistry threatRegistry,
            TokenAllocator tokenAllocator,
            ScoreService scoreService,
            TrainingStatsCollector stats,
            GameplayEventBus events,
            Action<ThreatRuntime> releaseThreat,
            Action<InterceptorRuntime> releaseInterceptor)
        {
            _session = session;
            _config = config;
            _threatSimulation = threatSimulation;
            _sensorSystem = sensorSystem;
            _interceptorSimulation = interceptorSimulation;
            _spawnDirector = spawnDirector;
            _launcher = launcher;
            _typingCoordinator = typingCoordinator;
            _threatRegistry = threatRegistry;
            _tokenAllocator = tokenAllocator;
            _scoreService = scoreService;
            _stats = stats;
            _events = events;
            _releaseThreat = releaseThreat;
            _releaseInterceptor = releaseInterceptor;
            _lastPublishedPhase = session.Phase;
        }

        public void StartFromMenu(string languageId)
        {
            ResetRuntime();
            _session.ConfigureLanguage(languageId);
            _session.EnterMenu();
            PublishPhaseIfChanged();
        }

        public void StartCountdown()
        {
            ResetRuntime();
            // New run = brand-new letter order (ignore previous bag leftover).
            _tokenAllocator.ReshuffleForNewRun();
            _session.BeginCountdown(_config.CountdownSeconds);
            PublishPhaseIfChanged();
        }

        public void TogglePause()
        {
            if (_session.Phase == SessionPhase.Playing)
                _session.Pause();
            else if (_session.Phase == SessionPhase.Paused)
                _session.Resume();
            PublishPhaseIfChanged();
        }

        public void Tick(float deltaSeconds)
        {
            if (_session.Phase == SessionPhase.Countdown)
            {
                _session.TickCountdown(deltaSeconds);
                PublishPhaseIfChanged();
                return;
            }

            if (_session.Phase != SessionPhase.Playing)
                return;

            _session.TickPlay(deltaSeconds);
            _spawnDirector.Tick(deltaSeconds);
            _sensorSystem.Scan();

            var threats = _threatRegistry.ActiveThreats;
            for (var i = 0; i < threats.Count; i++)
            {
                if (threats[i].TrackState == SensorTrackState.Tracked)
                    _typingCoordinator.NotifyThreatTracked(threats[i].Id);
            }

            _launcher.Tick(deltaSeconds);

            var impacts = _threatSimulation.Tick(deltaSeconds);
            for (var i = 0; i < impacts.Count; i++)
                ResolveImpact(impacts[i]);

            _interceptorSimulation.Tick(id =>
            {
                if (_threatRegistry.TryGet(id, out var threat) &&
                    (threat.LifecycleState == ThreatLifecycleState.Engaged ||
                     threat.LifecycleState == ThreatLifecycleState.Active))
                    return threat.Position;
                return null;
            }, deltaSeconds);

            var hits = _interceptorSimulation.LastHits;
            for (var i = 0; i < hits.Count; i++)
                ResolveInterception(hits[i]);

            var expired = _interceptorSimulation.LastExpired;
            for (var i = 0; i < expired.Count; i++)
                _releaseInterceptor(expired[i]);

            if (_config.TimedDurationSeconds.HasValue &&
                _session.ElapsedPlaySeconds >= _config.TimedDurationSeconds.Value)
            {
                _session.EndWithResults();
                _events.PublishSessionEnded();
                PublishPhaseIfChanged();
            }
        }

        private void ResolveInterception(InterceptorRuntime interceptor)
        {
            if (!_threatRegistry.TryGet(interceptor.TargetId, out var threat))
            {
                _releaseInterceptor(interceptor);
                return;
            }

            if (threat.LifecycleState == ThreatLifecycleState.Intercepted ||
                threat.LifecycleState == ThreatLifecycleState.Impacted)
            {
                _releaseInterceptor(interceptor);
                return;
            }

            threat.MarkIntercepted();
            var scoreResult = _scoreService.AwardInterception(
                threat.Challenge?.Length ?? 1,
                threat.RemainingTimeNormalized);

            var blastPosition = interceptor.Position;
            _tokenAllocator.Release(threat.LeasedTokenText);
            _events.PublishThreatIntercepted(threat.Id, blastPosition);
            _events.PublishScoreChanged(scoreResult.TotalScore, scoreResult.Combo);

            _threatRegistry.Unregister(threat.Id);
            _releaseThreat(threat);
            _releaseInterceptor(interceptor);
        }

        private void ResolveImpact(ThreatId threatId)
        {
            if (!_threatRegistry.TryGet(threatId, out var threat))
                return;

            // Typed in time but interceptor still in flight — count as success at ground.
            if (threat.LifecycleState == ThreatLifecycleState.Engaged)
            {
                var blastPosition = threat.Position;
                threat.MarkIntercepted();
                var scoreResult = _scoreService.AwardInterception(
                    threat.Challenge?.Length ?? 1,
                    0f);
                _tokenAllocator.Release(threat.LeasedTokenText);
                _events.PublishThreatIntercepted(threat.Id, blastPosition);
                _events.PublishScoreChanged(scoreResult.TotalScore, scoreResult.Combo);
                _threatRegistry.Unregister(threat.Id);
                _releaseThreat(threat);
                return;
            }

            if (threat.LifecycleState != ThreatLifecycleState.Active)
                return;

            var impactPosition = threat.Position;
            threat.MarkImpacted();
            _tokenAllocator.Release(threat.LeasedTokenText);
            _events.PublishThreatImpacted(threat.Id, impactPosition);

            var gameOver = _session.RegisterStrike(_config.MaxStrikes);
            _events.PublishStrikeCountChanged(_session.Strikes);
            _threatRegistry.Unregister(threat.Id);
            _releaseThreat(threat);

            if (gameOver && _config.EndOnMaxStrikes)
            {
                _events.PublishSessionEnded();
                // Stay in Playing — HUD reacts to DefenseFailed; do not freeze the clock.
            }
        }

        private void ResetRuntime()
        {
            var threats = _threatRegistry.ActiveThreats;
            for (var i = threats.Count - 1; i >= 0; i--)
            {
                var threat = threats[i];
                _tokenAllocator.Release(threat.LeasedTokenText);
                _threatRegistry.Unregister(threat.Id);
                _releaseThreat(threat);
            }

            _threatRegistry.Clear();
            _interceptorSimulation.Clear(_releaseInterceptor);
            _launcher.Clear();
            _spawnDirector.Reset();
            _typingCoordinator.ResetSession();
            _scoreService.Reset();
            _stats.Reset();
            _events.PublishStrikeCountChanged(0);
            _events.PublishScoreChanged(0, 0);
        }

        private void PublishPhaseIfChanged()
        {
            if (_session.Phase == _lastPublishedPhase)
                return;
            _lastPublishedPhase = _session.Phase;
            _events.PublishPhaseChanged(_session.Phase);
        }

        public void Dispose()
        {
            ResetRuntime();
            _typingCoordinator.Dispose();
        }
    }
}
