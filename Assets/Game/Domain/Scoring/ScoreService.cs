namespace TypingDefense.Domain.Scoring
{
    public readonly struct ScoreResult
    {
        public int PointsAwarded { get; }
        public int TotalScore { get; }
        public int Combo { get; }

        public ScoreResult(int pointsAwarded, int totalScore, int combo)
        {
            PointsAwarded = pointsAwarded;
            TotalScore = totalScore;
            Combo = combo;
        }
    }

    public sealed class ScoringProfileData
    {
        public int BaseThreatScore { get; set; } = 100;
        public float TokenLengthMultiplierPerChar { get; set; } = 0.25f;
        public float RemainingTimeMaxMultiplier { get; set; } = 1.5f;
        public float ComboStepMultiplier { get; set; } = 0.1f;
        public int MaxComboMultiplierSteps { get; set; } = 10;
        public int MistakeComboReset { get; set; } = 0;
    }

    public sealed class ScoreService
    {
        private readonly ScoringProfileData _profile;
        private int _score;
        private int _combo;

        public ScoreService(ScoringProfileData profile)
        {
            _profile = profile ?? new ScoringProfileData();
        }

        public int Score => _score;
        public int Combo => _combo;

        public void Reset()
        {
            _score = 0;
            _combo = 0;
        }

        public ScoreResult AwardInterception(int tokenLength, float remainingTimeNormalized)
        {
            _combo++;

            var lengthMul = 1f + System.Math.Max(0, tokenLength - 1) * _profile.TokenLengthMultiplierPerChar;
            var timeMul = 1f + System.Math.Clamp(remainingTimeNormalized, 0f, 1f) * (_profile.RemainingTimeMaxMultiplier - 1f);
            var comboSteps = System.Math.Min(_combo, _profile.MaxComboMultiplierSteps);
            var comboMul = 1f + comboSteps * _profile.ComboStepMultiplier;

            var points = (int)System.Math.Round(_profile.BaseThreatScore * lengthMul * timeMul * comboMul);
            _score += points;
            return new ScoreResult(points, _score, _combo);
        }

        public void RegisterMistake()
        {
            _combo = _profile.MistakeComboReset;
        }
    }
}
