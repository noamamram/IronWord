namespace TypingDefense.Infrastructure.UnityTime
{
    public interface IGameClock
    {
        float DeltaTime { get; }
        float TimeScale { get; set; }
    }

    public sealed class UnityGameClock : IGameClock
    {
        public float TimeScale { get; set; } = 1f;

        public float DeltaTime => UnityEngine.Time.deltaTime * TimeScale;
    }

    public sealed class ManualGameClock : IGameClock
    {
        public float DeltaTime { get; set; }
        public float TimeScale { get; set; } = 1f;
    }
}
