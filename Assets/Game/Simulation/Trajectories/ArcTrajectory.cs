using UnityEngine;

namespace TypingDefense.Simulation.Trajectories
{
    public interface ITrajectory
    {
        Vector3 EvaluatePosition(float normalizedTime);
        Vector3 EvaluateVelocity(float normalizedTime);
    }

    public sealed class ArcTrajectory : ITrajectory
    {
        private readonly Vector3 _start;
        private readonly Vector3 _end;
        private readonly Vector3 _up;
        private readonly float _height;

        public ArcTrajectory(Vector3 start, Vector3 end, Vector3 up, float height)
        {
            _start = start;
            _end = end;
            _up = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up;
            _height = height;
        }

        public Vector3 Start => _start;
        public Vector3 End => _end;

        public Vector3 EvaluatePosition(float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            Vector3 linear = Vector3.LerpUnclamped(_start, _end, t);
            Vector3 arc = _up * (4f * _height * t * (1f - t));
            return linear + arc;
        }

        public Vector3 EvaluateVelocity(float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            return (_end - _start) + _up * (4f * _height * (1f - 2f * t));
        }
    }

    public interface ITrajectoryFactory
    {
        ITrajectory CreateArc(Vector3 start, Vector3 end, float height);
    }

    public sealed class DefaultTrajectoryFactory : ITrajectoryFactory
    {
        public ITrajectory CreateArc(Vector3 start, Vector3 end, float height)
        {
            return new ArcTrajectory(start, end, Vector3.up, height);
        }
    }
}
