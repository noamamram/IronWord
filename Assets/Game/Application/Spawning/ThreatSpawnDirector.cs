using System;
using System.Collections.Generic;
using System.Text;
using TypingDefense.Application.Events;
using TypingDefense.Domain.Common;
using TypingDefense.Domain.Languages;
using TypingDefense.Domain.Typing;
using TypingDefense.Simulation.Threats;
using TypingDefense.Simulation.Trajectories;
using UnityEngine;

namespace TypingDefense.Application.Spawning
{
    public interface ISpawnPointProvider
    {
        bool TryGetSpawnPoint(out Vector3 spawnPoint);
    }

    public interface IImpactPointProvider
    {
        bool TryGetImpactPoint(out Vector3 impactPoint);
    }

    public sealed class ListPointProvider : ISpawnPointProvider, IImpactPointProvider
    {
        private readonly IReadOnlyList<Vector3> _points;
        private readonly System.Random _random;

        public ListPointProvider(IReadOnlyList<Vector3> points, int seed)
        {
            _points = points;
            _random = new System.Random(seed);
        }

        public bool TryGetSpawnPoint(out Vector3 spawnPoint) => TryGet(out spawnPoint);
        public bool TryGetImpactPoint(out Vector3 impactPoint) => TryGet(out impactPoint);

        private bool TryGet(out Vector3 point)
        {
            if (_points == null || _points.Count == 0)
            {
                point = Vector3.zero;
                return false;
            }

            point = _points[_random.Next(_points.Count)];
            return true;
        }
    }

    /// <summary>
    /// Samples continuous random points inside an axis-aligned box for natural spread.
    /// </summary>
    public sealed class RandomAreaPointProvider : ISpawnPointProvider, IImpactPointProvider
    {
        private readonly Vector3 _min;
        private readonly Vector3 _max;
        private readonly System.Random _random;

        public RandomAreaPointProvider(Vector3 min, Vector3 max, int seed)
        {
            _min = new Vector3(
                Mathf.Min(min.x, max.x),
                Mathf.Min(min.y, max.y),
                Mathf.Min(min.z, max.z));
            _max = new Vector3(
                Mathf.Max(min.x, max.x),
                Mathf.Max(min.y, max.y),
                Mathf.Max(min.z, max.z));
            _random = new System.Random(seed);
        }

        public bool TryGetSpawnPoint(out Vector3 spawnPoint)
        {
            spawnPoint = Sample();
            return true;
        }

        public bool TryGetImpactPoint(out Vector3 impactPoint)
        {
            impactPoint = Sample();
            return true;
        }

        private Vector3 Sample()
        {
            return new Vector3(
                Lerp(_min.x, _max.x),
                Lerp(_min.y, _max.y),
                Lerp(_min.z, _max.z));
        }

        private float Lerp(float a, float b)
        {
            return a + (float)_random.NextDouble() * (b - a);
        }
    }

    /// <summary>
    /// Samples sky spawn points in a forward fan relative to the camera —
    /// even left/right spread in view space (avoids world-axis left bias).
    /// </summary>
    public sealed class CameraRelativeSkySpawnProvider : ISpawnPointProvider
    {
        private readonly Func<Transform> _cameraProvider;
        private readonly float _minDistance;
        private readonly float _maxDistance;
        private readonly float _minHeight;
        private readonly float _maxHeight;
        private readonly float _horizontalFovFraction;
        private readonly System.Random _random;

        public CameraRelativeSkySpawnProvider(
            Func<Transform> cameraProvider,
            float minDistance,
            float maxDistance,
            float minHeight,
            float maxHeight,
            float horizontalFovFraction,
            int seed)
        {
            _cameraProvider = cameraProvider;
            _minDistance = Mathf.Max(10f, Mathf.Min(minDistance, maxDistance));
            _maxDistance = Mathf.Max(_minDistance + 1f, Mathf.Max(minDistance, maxDistance));
            _minHeight = Mathf.Min(minHeight, maxHeight);
            _maxHeight = Mathf.Max(minHeight, maxHeight);
            _horizontalFovFraction = Mathf.Clamp01(horizontalFovFraction);
            _random = new System.Random(seed);
        }

        public bool TryGetSpawnPoint(out Vector3 spawnPoint)
        {
            var cam = _cameraProvider?.Invoke();
            if (cam == null)
            {
                spawnPoint = new Vector3(0f, (_minHeight + _maxHeight) * 0.5f, (_minDistance + _maxDistance) * 0.5f);
                return true;
            }

            var forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            var camera = cam.GetComponent<Camera>();
            var halfFov = camera != null ? camera.fieldOfView * 0.5f * _horizontalFovFraction : 40f;

            var yaw = ((float)_random.NextDouble() * 2f - 1f) * halfFov;
            var distance = Mathf.Lerp(_minDistance, _maxDistance, (float)_random.NextDouble());
            var height = Mathf.Lerp(_minHeight, _maxHeight, (float)_random.NextDouble());

            var flatDir = (Quaternion.AngleAxis(yaw, Vector3.up) * forward).normalized;
            var groundAnchor = new Vector3(cam.position.x, 0f, cam.position.z) + flatDir * distance;
            spawnPoint = new Vector3(groundAnchor.x, height, groundAnchor.z);
            return true;
        }
    }

    /// <summary>
    /// Ground impact samples in a sector ahead of the camera / protected area.
    /// </summary>
    public sealed class CameraRelativeImpactProvider : IImpactPointProvider
    {
        private readonly Func<Transform> _cameraProvider;
        private readonly Func<Vector3> _areaOriginProvider;
        private readonly float _minDistance;
        private readonly float _maxDistance;
        private readonly float _halfWidth;
        private readonly float _groundY;
        private readonly System.Random _random;

        public CameraRelativeImpactProvider(
            Func<Transform> cameraProvider,
            Func<Vector3> areaOriginProvider,
            float minDistance,
            float maxDistance,
            float halfWidth,
            float groundY,
            int seed)
        {
            _cameraProvider = cameraProvider;
            _areaOriginProvider = areaOriginProvider;
            _minDistance = Mathf.Max(2f, Mathf.Min(minDistance, maxDistance));
            _maxDistance = Mathf.Max(_minDistance + 1f, Mathf.Max(minDistance, maxDistance));
            _halfWidth = Mathf.Max(1f, halfWidth);
            _groundY = groundY;
            _random = new System.Random(seed);
        }

        public bool TryGetImpactPoint(out Vector3 impactPoint)
        {
            var cam = _cameraProvider?.Invoke();
            var origin = _areaOriginProvider != null ? _areaOriginProvider() : Vector3.zero;

            var forward = cam != null
                ? Vector3.ProjectOnPlane(cam.forward, Vector3.up)
                : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var distance = Mathf.Lerp(_minDistance, _maxDistance, (float)_random.NextDouble());
            var lateralScale = distance / _maxDistance;
            var lateral = ((float)_random.NextDouble() * 2f - 1f) * _halfWidth * Mathf.Lerp(0.55f, 1f, lateralScale);

            var point = origin + forward * distance + right * lateral;
            impactPoint = new Vector3(point.x, _groundY, point.z);
            return true;
        }
    }

    public sealed class ThreatSpawnDirector
    {
        private readonly IThreatRegistry _registry;
        private readonly ISpawnPointProvider _spawnPoints;
        private readonly IImpactPointProvider _impactPoints;
        private readonly ITrajectoryFactory _trajectoryFactory;
        private readonly TokenAllocator _tokenAllocator;
        private readonly ILanguageRules _languageRules;
        private readonly IDifficultyPolicy _difficultyPolicy;
        private readonly GameplayEventBus _events;
        private readonly Func<ThreatRuntime> _threatFactory;
        private readonly Func<float> _elapsedProvider;
        private readonly Func<PerformanceSnapshot> _performanceProvider;
        private readonly IReadOnlyList<ThreatSpawnArchetype> _threatArchetypes;
        private readonly System.Random _random;
        private readonly Func<bool> _canSpawn;

        private float _spawnCredits;

        public ThreatSpawnDirector(
            IThreatRegistry registry,
            ISpawnPointProvider spawnPoints,
            IImpactPointProvider impactPoints,
            ITrajectoryFactory trajectoryFactory,
            TokenAllocator tokenAllocator,
            ILanguageRules languageRules,
            IDifficultyPolicy difficultyPolicy,
            GameplayEventBus events,
            Func<ThreatRuntime> threatFactory,
            Func<float> elapsedProvider,
            Func<PerformanceSnapshot> performanceProvider,
            IReadOnlyList<ThreatSpawnArchetype> threatArchetypes,
            int seed,
            Func<bool> canSpawn = null)
        {
            _registry = registry;
            _spawnPoints = spawnPoints;
            _impactPoints = impactPoints;
            _trajectoryFactory = trajectoryFactory;
            _tokenAllocator = tokenAllocator;
            _languageRules = languageRules;
            _difficultyPolicy = difficultyPolicy;
            _events = events;
            _threatFactory = threatFactory;
            _elapsedProvider = elapsedProvider;
            _performanceProvider = performanceProvider;
            _threatArchetypes = threatArchetypes ?? Array.Empty<ThreatSpawnArchetype>();
            _random = new System.Random(seed);
            _canSpawn = canSpawn;
        }

        public void Reset()
        {
            _spawnCredits = 0f;
            _tokenAllocator.Clear();
        }

        public void Tick(float deltaSeconds)
        {
            if (_canSpawn != null && !_canSpawn())
                return;

            var elapsed = _elapsedProvider();
            var difficulty = _difficultyPolicy.Evaluate(elapsed, _performanceProvider());
            _spawnCredits += difficulty.SpawnRatePerSecond * deltaSeconds;

            // Use cheapest unlocked threat cost as the credit threshold.
            var minCost = ResolveMinSpawnCost(elapsed);
            while (_spawnCredits >= minCost)
            {
                if (!CanSpawn(difficulty))
                    break;

                if (!TrySpawn(elapsed, difficulty))
                    break;
            }
        }

        private float ResolveMinSpawnCost(float elapsed)
        {
            var min = float.MaxValue;
            for (var i = 0; i < _threatArchetypes.Count; i++)
            {
                var def = _threatArchetypes[i];
                if (def == null || def.EvaluateWeight(elapsed) <= 0f)
                    continue;
                min = Mathf.Min(min, def.SpawnCost);
            }

            return min < float.MaxValue ? min : 1f;
        }

        private bool CanSpawn(DifficultySnapshot difficulty)
        {
            var activeCount = 0;
            var threats = _registry.ActiveThreats;
            for (var i = 0; i < threats.Count; i++)
            {
                var state = threats[i].LifecycleState;
                if (state == Domain.Sessions.ThreatLifecycleState.Active ||
                    state == Domain.Sessions.ThreatLifecycleState.Engaged)
                    activeCount++;
            }

            return activeCount < difficulty.MaxConcurrentThreats;
        }

        private bool TrySpawn(float elapsed, DifficultySnapshot difficulty)
        {
            if (!TryPickArchetype(elapsed, out var archetype))
                return false;
            if (!_spawnPoints.TryGetSpawnPoint(out var spawn))
                return false;
            if (!_impactPoints.TryGetImpactPoint(out var impact))
                return false;

            if (!archetype.TryRollTokenLength(_random, difficulty.MaxTokenLength, out var tokenLength))
                return false;

            if (!_tokenAllocator.TryAllocateSequence(tokenLength, out var elements))
                return false;

            var comparer = _languageRules as ILanguageComparer
                           ?? throw new InvalidOperationException("Language rules must implement ILanguageComparer.");

            ITypingChallenge challenge;
            string leasedText;
            if (elements.Count == 1)
            {
                leasedText = elements[0];
                challenge = new SingleElementChallenge(leasedText, comparer);
            }
            else
            {
                challenge = new SequenceChallenge(elements, comparer);
                leasedText = JoinElements(elements);
            }

            var duration = Mathf.Max(
                archetype.MinFlightDuration,
                archetype.BaseFlightDuration / Mathf.Max(0.1f, difficulty.SpeedMultiplier));

            var trajectory = _trajectoryFactory.CreateArc(spawn, impact, archetype.ArcHeight);
            var threat = _threatFactory();

            threat.Activate(
                archetype.DefinitionId,
                trajectory,
                duration,
                challenge,
                leasedText,
                elapsed,
                archetype.BaseScore);

            _registry.Register(threat);
            _events.PublishThreatSpawned(threat.Id);
            _spawnCredits -= archetype.SpawnCost;
            return true;
        }

        private bool TryPickArchetype(float elapsed, out ThreatSpawnArchetype archetype)
        {
            archetype = null;
            var totalWeight = 0f;
            for (var i = 0; i < _threatArchetypes.Count; i++)
            {
                var def = _threatArchetypes[i];
                if (def == null)
                    continue;
                totalWeight += def.EvaluateWeight(elapsed);
            }

            if (totalWeight <= 0f)
                return false;

            var roll = (float)_random.NextDouble() * totalWeight;
            var cursor = 0f;
            for (var i = 0; i < _threatArchetypes.Count; i++)
            {
                var def = _threatArchetypes[i];
                if (def == null)
                    continue;

                var weight = def.EvaluateWeight(elapsed);
                if (weight <= 0f)
                    continue;

                cursor += weight;
                if (roll <= cursor)
                {
                    archetype = def;
                    return true;
                }
            }

            for (var i = _threatArchetypes.Count - 1; i >= 0; i--)
            {
                var def = _threatArchetypes[i];
                if (def != null && def.EvaluateWeight(elapsed) > 0f)
                {
                    archetype = def;
                    return true;
                }
            }

            return false;
        }

        private static string JoinElements(IReadOnlyList<string> elements)
        {
            var sb = new StringBuilder(elements.Count);
            for (var i = 0; i < elements.Count; i++)
                sb.Append(elements[i]);
            return sb.ToString();
        }
    }
}
