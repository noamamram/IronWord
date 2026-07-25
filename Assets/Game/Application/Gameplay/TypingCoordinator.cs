using System;
using System.Collections.Generic;
using TypingDefense.Application.Events;
using TypingDefense.Application.Targeting;
using TypingDefense.Domain.Common;
using TypingDefense.Domain.Languages;
using TypingDefense.Domain.Scoring;
using TypingDefense.Domain.Sessions;
using TypingDefense.Domain.Statistics;
using TypingDefense.Domain.Typing;
using TypingDefense.Simulation.Threats;
using UnityEngine;

namespace TypingDefense.Application.Gameplay
{
    public interface ILauncherService
    {
        void RequestIntercept(ThreatId threatId);
    }

    public sealed class TypingCoordinator : IDisposable
    {
        private readonly ITextInputSource _input;
        private readonly ILanguageRules _languageRules;
        private readonly ITypingTargetSelector _targetSelector;
        private readonly IThreatRegistry _threatRegistry;
        private readonly ILauncherService _launcher;
        private readonly GameplayEventBus _events;
        private readonly ScoreService _scoreService;
        private readonly TrainingStatsCollector _stats;
        private readonly Func<Vector3> _sensorOriginProvider;
        private readonly Func<bool> _isInputEnabled;
        private readonly Func<bool> _registerStrike;

        private TypingTarget _lockedTarget;
        private readonly Dictionary<int, float> _trackStartTimes = new Dictionary<int, float>();
        private readonly Func<float> _elapsedProvider;

        public TypingCoordinator(
            ITextInputSource input,
            ILanguageRules languageRules,
            ITypingTargetSelector targetSelector,
            IThreatRegistry threatRegistry,
            ILauncherService launcher,
            GameplayEventBus events,
            ScoreService scoreService,
            TrainingStatsCollector stats,
            Func<Vector3> sensorOriginProvider,
            Func<float> elapsedProvider,
            Func<bool> isInputEnabled,
            Func<bool> registerStrike = null)
        {
            _input = input;
            _languageRules = languageRules;
            _targetSelector = targetSelector;
            _threatRegistry = threatRegistry;
            _launcher = launcher;
            _events = events;
            _scoreService = scoreService;
            _stats = stats;
            _sensorOriginProvider = sensorOriginProvider;
            _elapsedProvider = elapsedProvider;
            _isInputEnabled = isInputEnabled;
            _registerStrike = registerStrike;
        }

        public void Start()
        {
            _input.TextCommitted += OnTextCommitted;
            _input.BackspacePressed += OnBackspacePressed;
        }

        public void Dispose()
        {
            _input.TextCommitted -= OnTextCommitted;
            _input.BackspacePressed -= OnBackspacePressed;
            _lockedTarget = null;
            _trackStartTimes.Clear();
        }

        public void NotifyThreatTracked(ThreatId id)
        {
            if (!_trackStartTimes.ContainsKey(id.Value))
                _trackStartTimes[id.Value] = _elapsedProvider();
        }

        public void ClearLock()
        {
            _lockedTarget = null;
        }

        public void ResetSession()
        {
            _lockedTarget = null;
            _trackStartTimes.Clear();
        }

        private void OnTextCommitted(string rawInput)
        {
            if (!_isInputEnabled())
                return;

            var normalized = _languageRules.Normalize(rawInput);
            var elements = _languageRules.SplitIntoTextElements(normalized);
            for (var i = 0; i < elements.Count; i++)
                ProcessTextElement(elements[i]);
        }

        private void ProcessTextElement(string textElement)
        {
            if (!_languageRules.IsAllowed(textElement))
                return;

            var candidates = TypingTargetFactory.BuildFromRegistry(
                _threatRegistry,
                _sensorOriginProvider());

            // Refresh locked target reference from live registry.
            if (_lockedTarget != null)
            {
                if (!_threatRegistry.TryGet(_lockedTarget.Id, out var lockedThreat) ||
                    lockedThreat.LifecycleState != ThreatLifecycleState.Active ||
                    lockedThreat.Challenge == null ||
                    lockedThreat.Challenge.IsCompleted)
                {
                    _lockedTarget = null;
                }
                else
                {
                    _lockedTarget = new TypingTarget(
                        lockedThreat.Id,
                        lockedThreat.Challenge,
                        lockedThreat.TimeToImpact,
                        lockedThreat.SpawnedAtElapsed,
                        Vector3.Distance(_sensorOriginProvider(), lockedThreat.Position));
                }
            }

            var target = _targetSelector.Select(textElement, _lockedTarget, candidates);
            if (target == null)
            {
                // Only strike when there are live targets and this letter matches none of them.
                RegisterWrongLetter(textElement, countAsStrike: candidates.Count > 0);
                return;
            }

            var result = target.Challenge.Submit(textElement);
            switch (result.Outcome)
            {
                case ChallengeInputOutcome.Incorrect:
                    _lockedTarget = null;
                    RegisterWrongLetter(textElement, countAsStrike: true);
                    break;

                case ChallengeInputOutcome.CorrectProgress:
                    if (_threatRegistry.TryGet(target.Id, out var progressThreat))
                        progressThreat.SetTypingLocked();
                    _lockedTarget = target;
                    _events.PublishTypingProgress(target.Id, result.Progress, result.Length);
                    break;

                case ChallengeInputOutcome.Completed:
                    CompleteTarget(target);
                    break;
            }
        }

        private void RegisterWrongLetter(string textElement, bool countAsStrike)
        {
            _scoreService.RegisterMistake();
            _stats.RegisterIncorrect(textElement);
            _events.PublishMistake(textElement);
            _events.PublishScoreChanged(_scoreService.Score, _scoreService.Combo);

            if (!countAsStrike)
                return;

            _events.PublishDefenseHitFeedback();
            if (_registerStrike != null && _registerStrike())
                return;
        }

        private void CompleteTarget(TypingTarget target)
        {
            if (!_threatRegistry.TryGet(target.Id, out var threat))
                return;

            if (!threat.TryReserveForInterception())
                return;

            var reaction = 0f;
            if (_trackStartTimes.TryGetValue(target.Id.Value, out var start))
                reaction = Mathf.Max(0f, _elapsedProvider() - start);

            _stats.RegisterCorrect(threat.LeasedTokenText, reaction);
            _lockedTarget = null;
            _events.PublishTypingProgress(target.Id, target.Challenge.Progress, target.Challenge.Length);
            _events.PublishThreatEngaged(target.Id);
            _launcher.RequestIntercept(target.Id);
        }

        private void OnBackspacePressed()
        {
            // MVP: ignore backspace for single-letter mode.
        }
    }
}
