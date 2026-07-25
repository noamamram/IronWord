namespace TypingDefense.Domain.Sessions
{
    public enum SessionPhase
    {
        None,
        Menu,
        Countdown,
        Playing,
        Paused,
        Results,
        GameOver
    }

    public enum ThreatLifecycleState
    {
        Pooled,
        Active,
        Engaged,
        Intercepted,
        Impacted,
        Recycled
    }

    public enum SensorTrackState
    {
        Undetected,
        Tracked,
        Lost
    }

    public enum TypingTrackState
    {
        Unassigned,
        Available,
        Locked,
        Completed
    }

    public sealed class SessionConfig
    {
        public int MaxStrikes { get; set; } = 3;
        public float CountdownSeconds { get; set; } = 3f;
        public float? TimedDurationSeconds { get; set; }
        public bool EndOnMaxStrikes { get; set; } = true;
    }

    public sealed class SessionState
    {
        public SessionPhase Phase { get; private set; } = SessionPhase.Menu;
        public float ElapsedPlaySeconds { get; private set; }
        public float CountdownRemaining { get; private set; }
        public int Strikes { get; private set; }
        public string TrainingLanguageId { get; private set; } = "en";
        /// <summary>True after max strikes — keep playing/time, but stop new spawns and typing.</summary>
        public bool DefenseFailed { get; private set; }
        public bool IsSimulationRunning => Phase == SessionPhase.Playing;
        public bool CanSpawnThreats => Phase == SessionPhase.Playing && !DefenseFailed;
        public bool CanAcceptTyping => Phase == SessionPhase.Playing && !DefenseFailed;

        public void ConfigureLanguage(string languageId)
        {
            TrainingLanguageId = languageId ?? "en";
        }

        public void EnterMenu()
        {
            Phase = SessionPhase.Menu;
            ElapsedPlaySeconds = 0f;
            Strikes = 0;
            CountdownRemaining = 0f;
            DefenseFailed = false;
        }

        public void BeginCountdown(float seconds)
        {
            Phase = SessionPhase.Countdown;
            CountdownRemaining = seconds;
            ElapsedPlaySeconds = 0f;
            Strikes = 0;
            DefenseFailed = false;
        }

        public void BeginPlaying()
        {
            Phase = SessionPhase.Playing;
            CountdownRemaining = 0f;
        }

        public void Pause()
        {
            if (Phase == SessionPhase.Playing)
                Phase = SessionPhase.Paused;
        }

        public void Resume()
        {
            if (Phase == SessionPhase.Paused)
                Phase = SessionPhase.Playing;
        }

        public void TickCountdown(float deltaSeconds)
        {
            if (Phase != SessionPhase.Countdown)
                return;

            CountdownRemaining -= deltaSeconds;
            if (CountdownRemaining <= 0f)
            {
                CountdownRemaining = 0f;
                BeginPlaying();
            }
        }

        public void TickPlay(float deltaSeconds)
        {
            if (Phase != SessionPhase.Playing)
                return;

            ElapsedPlaySeconds += deltaSeconds;
        }

        /// <summary>
        /// Registers a strike. On max strikes sets DefenseFailed but keeps Playing
        /// so the clock and residual missiles continue (spawning stops separately).
        /// </summary>
        public bool RegisterStrike(int maxStrikes)
        {
            Strikes++;
            if (Strikes >= maxStrikes)
            {
                DefenseFailed = true;
                return true;
            }

            return false;
        }

        public void EndWithResults()
        {
            Phase = SessionPhase.Results;
        }
    }
}
