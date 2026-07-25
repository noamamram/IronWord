using System.Collections.Generic;
using TypingDefense.Domain.Common;
using UnityEngine;

namespace TypingDefense.Simulation.Interceptors
{
    public enum InterceptorState
    {
        Pooled,
        InFlight,
        Hit,
        Expired
    }

    public sealed class InterceptorRuntime
    {
        public InterceptorId Id { get; }
        public ThreatId TargetId { get; private set; }
        public InterceptorState State { get; private set; }
        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float CruiseSpeed { get; private set; }
        public float InitialSpeed { get; private set; }
        public float AccelerationSeconds { get; private set; }
        public float HitRadius { get; private set; }
        public float MaxLifetime { get; private set; }
        public float Age { get; private set; }
        public float CurrentSpeed { get; private set; }
        public float BoostDurationSeconds { get; private set; }
        public float MaxTurnRateDegrees { get; private set; }
        public float TurnEngageSeconds { get; private set; }
        public float TurnAccelerationDegrees { get; private set; }

        private Vector3 _boostDirection;
        private Vector3 _flightDirection;
        private float _currentTurnRateDegrees;

        public InterceptorRuntime(InterceptorId id)
        {
            Id = id;
            State = InterceptorState.Pooled;
        }

        public void Launch(
            ThreatId targetId,
            Vector3 startPosition,
            Vector3 boostDirection,
            float cruiseSpeed,
            float initialSpeed,
            float accelerationSeconds,
            float hitRadius,
            float maxLifetime,
            float boostDurationSeconds = 0.28f,
            float maxTurnRateDegrees = 105f,
            float turnEngageSeconds = 0.65f,
            float turnAccelerationDegrees = 140f)
        {
            TargetId = targetId;
            Position = startPosition;
            CruiseSpeed = Mathf.Max(0.1f, cruiseSpeed);
            InitialSpeed = Mathf.Clamp(initialSpeed, 0.1f, CruiseSpeed);
            AccelerationSeconds = Mathf.Max(0.05f, accelerationSeconds);
            HitRadius = Mathf.Max(0.1f, hitRadius);
            MaxLifetime = Mathf.Max(0.1f, maxLifetime);
            BoostDurationSeconds = Mathf.Max(0.05f, boostDurationSeconds);
            MaxTurnRateDegrees = Mathf.Max(20f, maxTurnRateDegrees);
            TurnEngageSeconds = Mathf.Max(0.05f, turnEngageSeconds);
            TurnAccelerationDegrees = Mathf.Max(10f, turnAccelerationDegrees);

            _boostDirection = boostDirection.sqrMagnitude > 0.0001f
                ? boostDirection.normalized
                : Vector3.up;
            _flightDirection = _boostDirection;
            _currentTurnRateDegrees = 0f;

            Age = 0f;
            CurrentSpeed = InitialSpeed;
            State = InterceptorState.InFlight;
            Velocity = _flightDirection * CurrentSpeed;
        }

        public void Tick(Vector3 targetPosition, float deltaSeconds)
        {
            if (State != InterceptorState.InFlight)
                return;

            Age += deltaSeconds;
            CurrentSpeed = EvaluateSpeed(Age);

            var toTarget = targetPosition - Position;
            var distance = toTarget.magnitude;
            if (distance <= HitRadius)
            {
                State = InterceptorState.Hit;
                Position = targetPosition;
                Velocity = _flightDirection * CurrentSpeed;
                return;
            }

            var desiredDirection = ResolveDesiredDirection(toTarget, distance);
            _flightDirection = SteerToward(desiredDirection, deltaSeconds);

            var step = CurrentSpeed * deltaSeconds;
            // Only snap-hit once mostly aimed at the target — avoids sharp late corrections.
            var aligned = Vector3.Dot(_flightDirection, desiredDirection) > 0.92f;
            if (step >= distance && Age >= BoostDurationSeconds && aligned)
            {
                Position = targetPosition;
                State = InterceptorState.Hit;
                Velocity = _flightDirection * CurrentSpeed;
                return;
            }

            Position += _flightDirection * step;
            Velocity = _flightDirection * CurrentSpeed;

            if (Age >= MaxLifetime)
                State = InterceptorState.Expired;
        }

        private Vector3 ResolveDesiredDirection(Vector3 toTarget, float distance)
        {
            if (Age < BoostDurationSeconds)
                return _boostDirection;

            if (distance <= 0.0001f)
                return _flightDirection.sqrMagnitude > 0.0001f ? _flightDirection : Vector3.up;

            var toTargetDir = toTarget / distance;

            // Soft handoff: blend muzzle direction into target chase for a curved arc.
            var sinceBoost = Age - BoostDurationSeconds;
            if (sinceBoost < TurnEngageSeconds)
            {
                var t = Mathf.Clamp01(sinceBoost / TurnEngageSeconds);
                var eased = t * t * (3f - 2f * t);
                return Vector3.Slerp(_boostDirection, toTargetDir, eased).normalized;
            }

            return toTargetDir;
        }

        private Vector3 SteerToward(Vector3 desiredDirection, float deltaSeconds)
        {
            if (desiredDirection.sqrMagnitude < 0.0001f)
                return _flightDirection;

            desiredDirection.Normalize();
            if (_flightDirection.sqrMagnitude < 0.0001f)
                return desiredDirection;

            // During boost: no turning. After boost: ramp turn authority for a rounded arc.
            float targetTurnRate;
            if (Age < BoostDurationSeconds)
            {
                targetTurnRate = 0f;
            }
            else
            {
                var sinceBoost = Age - BoostDurationSeconds;
                var authority = Mathf.Clamp01(sinceBoost / TurnEngageSeconds);
                authority = authority * authority * (3f - 2f * authority);
                targetTurnRate = MaxTurnRateDegrees * authority;
            }

            _currentTurnRateDegrees = Mathf.MoveTowards(
                _currentTurnRateDegrees,
                targetTurnRate,
                TurnAccelerationDegrees * Mathf.Max(0f, deltaSeconds));

            var angle = Vector3.Angle(_flightDirection, desiredDirection);
            if (angle <= 0.01f)
                return desiredDirection;

            var maxRadians = _currentTurnRateDegrees * Mathf.Deg2Rad * Mathf.Max(0f, deltaSeconds);
            if (maxRadians <= 0.00001f)
                return _flightDirection;

            var t = Mathf.Clamp01(maxRadians / (angle * Mathf.Deg2Rad));
            return Vector3.Slerp(_flightDirection, desiredDirection, t).normalized;
        }

        private float EvaluateSpeed(float age)
        {
            var t = Mathf.Clamp01(age / AccelerationSeconds);
            var eased = t * t * (3f - 2f * t);
            return Mathf.Lerp(InitialSpeed, CruiseSpeed, eased);
        }

        public void ReturnToPool()
        {
            State = InterceptorState.Pooled;
            TargetId = default;
            Position = Vector3.zero;
            Velocity = Vector3.zero;
            Age = 0f;
            CurrentSpeed = 0f;
            BoostDurationSeconds = 0f;
            MaxTurnRateDegrees = 0f;
            TurnEngageSeconds = 0f;
            TurnAccelerationDegrees = 0f;
            _currentTurnRateDegrees = 0f;
            _boostDirection = Vector3.up;
            _flightDirection = Vector3.up;
        }
    }

    public sealed class InterceptorSimulation
    {
        private readonly List<InterceptorRuntime> _active = new List<InterceptorRuntime>(32);
        private readonly List<InterceptorRuntime> _hitBuffer = new List<InterceptorRuntime>(8);
        private readonly List<InterceptorRuntime> _expiredBuffer = new List<InterceptorRuntime>(8);

        public IReadOnlyList<InterceptorRuntime> Active => _active;
        public IReadOnlyList<InterceptorRuntime> LastHits => _hitBuffer;
        public IReadOnlyList<InterceptorRuntime> LastExpired => _expiredBuffer;

        public void Register(InterceptorRuntime interceptor)
        {
            if (interceptor == null || _active.Contains(interceptor))
                return;
            _active.Add(interceptor);
        }

        public bool TryGet(InterceptorId id, out InterceptorRuntime interceptor)
        {
            for (var i = 0; i < _active.Count; i++)
            {
                if (_active[i].Id.Equals(id))
                {
                    interceptor = _active[i];
                    return true;
                }
            }

            interceptor = null;
            return false;
        }

        public void Tick(System.Func<ThreatId, Vector3?> targetPositionProvider, float deltaSeconds)
        {
            _hitBuffer.Clear();
            _expiredBuffer.Clear();

            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var interceptor = _active[i];
                var targetPos = targetPositionProvider(interceptor.TargetId);
                if (!targetPos.HasValue)
                {
                    interceptor.ReturnToPool();
                    _active.RemoveAt(i);
                    _expiredBuffer.Add(interceptor);
                    continue;
                }

                interceptor.Tick(targetPos.Value, deltaSeconds);

                if (interceptor.State == InterceptorState.Hit)
                {
                    _hitBuffer.Add(interceptor);
                    _active.RemoveAt(i);
                }
                else if (interceptor.State == InterceptorState.Expired)
                {
                    _expiredBuffer.Add(interceptor);
                    _active.RemoveAt(i);
                }
            }
        }

        public void Clear(System.Action<InterceptorRuntime> release = null)
        {
            for (var i = 0; i < _active.Count; i++)
            {
                if (release != null)
                    release(_active[i]);
                else
                    _active[i].ReturnToPool();
            }

            _active.Clear();
            _hitBuffer.Clear();
            _expiredBuffer.Clear();
        }
    }
}
