using TypingDefense.Application.Events;
using TypingDefense.Domain.Sessions;
using UnityEngine;
using UnityEngine.UI;

namespace TypingDefense.Presentation.HUD
{
    public sealed class GameplayHudView : MonoBehaviour
    {
        [SerializeField] private Text _scoreText;
        [SerializeField] private Text _comboText;
        [SerializeField] private Text _strikesText;
        [SerializeField] private Text _timerText;
        [SerializeField] private Text _phaseText;
        [SerializeField] private Text _promptText;

        private GameplayEventBus _events;
        private SessionState _session;
        private int _score;
        private int _combo;
        private int _strikes;
        private int _maxStrikes = 3;

        public void Initialize(GameplayEventBus events, SessionState session, int maxStrikes = 3)
        {
            _events = events;
            _session = session;
            _maxStrikes = Mathf.Max(1, maxStrikes);
            _events.ScoreChanged += OnScoreChanged;
            _events.StrikeCountChanged += OnStrikesChanged;
            _events.PhaseChanged += OnPhaseChanged;
            RefreshAll();
        }

        public void Shutdown()
        {
            if (_events == null)
                return;

            _events.ScoreChanged -= OnScoreChanged;
            _events.StrikeCountChanged -= OnStrikesChanged;
            _events.PhaseChanged -= OnPhaseChanged;
        }

        private void Update()
        {
            if (_session == null)
                return;

            if (_timerText != null)
            {
                if (_session.Phase == SessionPhase.Countdown)
                    _timerText.text = Mathf.CeilToInt(_session.CountdownRemaining).ToString();
                else if (_session.Phase == SessionPhase.Playing || _session.Phase == SessionPhase.Paused)
                    _timerText.text = FormatTime(_session.ElapsedPlaySeconds);
            }

            if (_session.DefenseFailed)
            {
                if (_phaseText != null)
                    _phaseText.text = "FAILED";
                if (_promptText != null)
                    _promptText.text = "DEFENSE FAILED — SPAWNING STOPPED";
            }
        }

        private void OnScoreChanged(int score, int combo)
        {
            _score = score;
            _combo = combo;
            if (_scoreText != null)
                _scoreText.text = $"SCORE  {_score}";
            if (_comboText != null)
                _comboText.text = _combo > 1 ? $"COMBO x{_combo}" : string.Empty;
        }

        private void OnStrikesChanged(int strikes)
        {
            _strikes = strikes;
            RefreshStrikesLabel();
        }

        private void RefreshStrikesLabel()
        {
            if (_strikesText == null)
                return;

            var remaining = Mathf.Max(0, _maxStrikes - _strikes);
            // Filled = remaining lives, empty = spent strikes.
            var icons = new char[_maxStrikes];
            for (var i = 0; i < _maxStrikes; i++)
                icons[i] = i < remaining ? '●' : '○';

            _strikesText.text = $"LIVES  {new string(icons)}  ({remaining}/{_maxStrikes})";
            _strikesText.color = remaining <= 1
                ? new Color(1f, 0.35f, 0.3f)
                : Color.white;
        }

        private void OnPhaseChanged(SessionPhase phase)
        {
            if (_phaseText != null)
                _phaseText.text = phase.ToString().ToUpperInvariant();

            if (_promptText != null)
            {
                _promptText.text = phase switch
                {
                    SessionPhase.Menu => "CHOOSE A LANGUAGE TO BEGIN",
                    SessionPhase.Countdown => "GET READY",
                    SessionPhase.Playing => "TYPE THE LETTER ON THE TARGET",
                    SessionPhase.Paused => "PAUSED",
                    SessionPhase.GameOver => "DEFENSE FAILED",
                    SessionPhase.Results => "SESSION COMPLETE",
                    _ => string.Empty
                };
            }

            if (phase == SessionPhase.Countdown || phase == SessionPhase.Menu)
            {
                _strikes = _session != null ? _session.Strikes : 0;
                RefreshStrikesLabel();
            }
        }

        private void RefreshAll()
        {
            OnScoreChanged(_score, _combo);
            _strikes = _session != null ? _session.Strikes : _strikes;
            RefreshStrikesLabel();
            if (_session != null)
                OnPhaseChanged(_session.Phase);
        }

        private static string FormatTime(float seconds)
        {
            var m = Mathf.FloorToInt(seconds / 60f);
            var s = Mathf.FloorToInt(seconds % 60f);
            return $"{m:00}:{s:00}";
        }

        public void ConfigureRuntime(Text score, Text combo, Text strikes, Text timer, Text phase, Text prompt)
        {
            _scoreText = score;
            _comboText = combo;
            _strikesText = strikes;
            _timerText = timer;
            _phaseText = phase;
            _promptText = prompt;
        }
    }
}
