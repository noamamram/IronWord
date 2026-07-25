using System;
using System.Collections.Generic;
using TypingDefense.Application.Events;
using TypingDefense.Domain.Common;
using TypingDefense.Simulation.Interceptors;
using TypingDefense.Simulation.Threats;
using UnityEngine;

namespace TypingDefense.Application.Gameplay
{
    public readonly struct InterceptRequest
    {
        public ThreatId ThreatId { get; }

        public InterceptRequest(ThreatId threatId)
        {
            ThreatId = threatId;
        }
    }

    public readonly struct LaunchMuzzle
    {
        public Vector3 Position { get; }
        public Vector3 Direction { get; }

        public LaunchMuzzle(Vector3 position, Vector3 direction)
        {
            Position = position;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
        }
    }

    public sealed class LauncherService : ILauncherService
    {
        private readonly Queue<InterceptRequest> _queue = new Queue<InterceptRequest>();
        private readonly IThreatRegistry _threatRegistry;
        private readonly InterceptorSimulation _interceptorSimulation;
        private readonly GameplayEventBus _events;
        private readonly Func<InterceptorRuntime> _interceptorFactory;
        private readonly Func<LaunchMuzzle> _launchMuzzleProvider;
        private readonly float _cruiseSpeed;
        private readonly float _initialSpeed;
        private readonly float _accelerationSeconds;
        private readonly float _hitRadius;
        private readonly float _maxLifetime;
        private readonly float _launchInterval;
        private readonly float _boostDurationSeconds;
        private readonly float _maxTurnRateDegrees;
        private readonly float _turnEngageSeconds;
        private readonly float _turnAccelerationDegrees;
        private float _cooldown;

        public LauncherService(
            IThreatRegistry threatRegistry,
            InterceptorSimulation interceptorSimulation,
            GameplayEventBus events,
            Func<InterceptorRuntime> interceptorFactory,
            Func<LaunchMuzzle> launchMuzzleProvider,
            float cruiseSpeed = 95f,
            float initialSpeed = 18f,
            float accelerationSeconds = 0.85f,
            float hitRadius = 2.5f,
            float maxLifetime = 8f,
            float launchInterval = 0.08f,
            float boostDurationSeconds = 0.28f,
            float maxTurnRateDegrees = 105f,
            float turnEngageSeconds = 0.65f,
            float turnAccelerationDegrees = 140f)
        {
            _threatRegistry = threatRegistry;
            _interceptorSimulation = interceptorSimulation;
            _events = events;
            _interceptorFactory = interceptorFactory;
            _launchMuzzleProvider = launchMuzzleProvider;
            _cruiseSpeed = cruiseSpeed;
            _initialSpeed = initialSpeed;
            _accelerationSeconds = accelerationSeconds;
            _hitRadius = hitRadius;
            _maxLifetime = maxLifetime;
            _launchInterval = launchInterval;
            _boostDurationSeconds = boostDurationSeconds;
            _maxTurnRateDegrees = maxTurnRateDegrees;
            _turnEngageSeconds = turnEngageSeconds;
            _turnAccelerationDegrees = turnAccelerationDegrees;
        }

        public void RequestIntercept(ThreatId threatId)
        {
            _queue.Enqueue(new InterceptRequest(threatId));
        }

        public void Tick(float deltaSeconds)
        {
            _cooldown -= deltaSeconds;
            while (_cooldown <= 0f && _queue.Count > 0)
            {
                var request = _queue.Dequeue();
                if (!_threatRegistry.TryGet(request.ThreatId, out var threat))
                    continue;

                if (threat.LifecycleState != Domain.Sessions.ThreatLifecycleState.Engaged)
                    continue;

                var muzzle = _launchMuzzleProvider != null
                    ? _launchMuzzleProvider()
                    : new LaunchMuzzle(Vector3.up * 4f, Vector3.up);

                var interceptor = _interceptorFactory();
                interceptor.Launch(
                    request.ThreatId,
                    muzzle.Position,
                    muzzle.Direction,
                    _cruiseSpeed,
                    _initialSpeed,
                    _accelerationSeconds,
                    _hitRadius,
                    _maxLifetime,
                    _boostDurationSeconds,
                    _maxTurnRateDegrees,
                    _turnEngageSeconds,
                    _turnAccelerationDegrees);
                _interceptorSimulation.Register(interceptor);
                _events.PublishInterceptorLaunched(request.ThreatId, interceptor.Id, muzzle.Position);
                _cooldown = _launchInterval;
            }
        }

        public void Clear()
        {
            _queue.Clear();
            _cooldown = 0f;
        }
    }
}
