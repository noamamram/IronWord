using System.Collections.Generic;
using TypingDefense.Domain.Common;
using TypingDefense.Domain.Sessions;
using UnityEngine;

namespace TypingDefense.Simulation.Threats
{
    public interface IThreatRegistry
    {
        IReadOnlyList<ThreatRuntime> ActiveThreats { get; }
        ThreatRuntime Get(ThreatId id);
        bool TryGet(ThreatId id, out ThreatRuntime threat);
        void Register(ThreatRuntime threat);
        void Unregister(ThreatId id);
        void Clear();
    }

    public sealed class ThreatRegistry : IThreatRegistry
    {
        private readonly List<ThreatRuntime> _active = new List<ThreatRuntime>(64);
        private readonly Dictionary<int, ThreatRuntime> _byId = new Dictionary<int, ThreatRuntime>(64);

        public IReadOnlyList<ThreatRuntime> ActiveThreats => _active;

        public ThreatRuntime Get(ThreatId id)
        {
            return _byId.TryGetValue(id.Value, out var threat) ? threat : null;
        }

        public bool TryGet(ThreatId id, out ThreatRuntime threat)
        {
            return _byId.TryGetValue(id.Value, out threat);
        }

        public void Register(ThreatRuntime threat)
        {
            if (threat == null)
                return;

            if (_byId.ContainsKey(threat.Id.Value))
                return;

            _byId[threat.Id.Value] = threat;
            _active.Add(threat);
        }

        public void Unregister(ThreatId id)
        {
            if (!_byId.TryGetValue(id.Value, out var threat))
                return;

            _byId.Remove(id.Value);
            _active.Remove(threat);
        }

        public void Clear()
        {
            _active.Clear();
            _byId.Clear();
        }
    }

    public sealed class ThreatSimulation
    {
        private readonly IThreatRegistry _registry;
        private readonly List<ThreatId> _impactBuffer = new List<ThreatId>(16);

        public ThreatSimulation(IThreatRegistry registry)
        {
            _registry = registry;
        }

        public IReadOnlyList<ThreatId> Tick(float deltaSeconds)
        {
            _impactBuffer.Clear();
            var threats = _registry.ActiveThreats;
            for (var i = 0; i < threats.Count; i++)
            {
                var threat = threats[i];
                if (threat.LifecycleState != ThreatLifecycleState.Active &&
                    threat.LifecycleState != ThreatLifecycleState.Engaged)
                    continue;

                threat.Tick(deltaSeconds);
                if (threat.HasReachedImpact())
                    _impactBuffer.Add(threat.Id);
            }

            return _impactBuffer;
        }
    }

    public sealed class SensorSystem
    {
        private readonly IThreatRegistry _registry;
        private Vector3 _origin;
        private Vector3 _forward;
        private float _maxRange;
        private float _maxAngleDegrees;

        public SensorSystem(IThreatRegistry registry)
        {
            _registry = registry;
            _maxRange = 500f;
            _maxAngleDegrees = 70f;
            _forward = Vector3.forward;
        }

        public void Configure(Vector3 origin, Vector3 forward, float maxRange, float maxAngleDegrees)
        {
            _origin = origin;
            _forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            _maxRange = maxRange;
            _maxAngleDegrees = maxAngleDegrees;
        }

        public void Scan()
        {
            var threats = _registry.ActiveThreats;
            var maxRangeSq = _maxRange * _maxRange;

            for (var i = 0; i < threats.Count; i++)
            {
                var threat = threats[i];
                if (threat.LifecycleState != ThreatLifecycleState.Active &&
                    threat.LifecycleState != ThreatLifecycleState.Engaged)
                {
                    threat.SetTracked(false);
                    continue;
                }

                var toThreat = threat.Position - _origin;
                var distanceSq = toThreat.sqrMagnitude;
                if (distanceSq > maxRangeSq || distanceSq < 0.01f)
                {
                    threat.SetTracked(false);
                    continue;
                }

                var angle = Vector3.Angle(_forward, toThreat);
                threat.SetTracked(angle <= _maxAngleDegrees);
            }
        }
    }
}
