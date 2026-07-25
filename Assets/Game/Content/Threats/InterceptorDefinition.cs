using TypingDefense.Domain.Common;
using UnityEngine;

namespace TypingDefense.Content.Threats
{
    [CreateAssetMenu(
        fileName = "InterceptorDefinition",
        menuName = "IronWord/Threats/Interceptor Definition")]
    public sealed class InterceptorDefinition : ScriptableObject
    {
        [SerializeField] private string _id = "iron_dome_interceptor";
        [SerializeField] private GameObject _prefab;
        [SerializeField] private float _cruiseSpeed = 95f;
        [SerializeField] private float _initialSpeed = 18f;
        [SerializeField] private float _accelerationSeconds = 0.85f;
        [SerializeField] private float _hitRadius = 3f;
        [SerializeField] private float _maxLifetime = 8f;
        [SerializeField] private float _launchInterval = 0.08f;
        [Tooltip("Seconds of muzzle-direction throw before homing begins.")]
        [SerializeField] private float _boostDurationSeconds = 0.28f;
        [Tooltip("Peak turn rate after engage ramp (deg/sec). Lower = wider circular arc.")]
        [SerializeField] private float _maxTurnRateDegrees = 105f;
        [Tooltip("How long after boost the turn authority / aim blends in.")]
        [SerializeField] private float _turnEngageSeconds = 0.65f;
        [Tooltip("How quickly turn rate builds toward Max Turn Rate (deg/sec²).")]
        [SerializeField] private float _turnAccelerationDegrees = 140f;
        [SerializeField] private Vector3 _visualScale = Vector3.one;
        [Tooltip("Euler correction so the mesh nose aligns with Unity +Z.")]
        [SerializeField] private Vector3 _meshForwardEulerOffset = new Vector3(-90f, 0f, 0f);

        public DefinitionId DefinitionId => new DefinitionId(_id);
        public GameObject Prefab => _prefab;
        public float CruiseSpeed => _cruiseSpeed;
        public float Speed => _cruiseSpeed;
        public float InitialSpeed => _initialSpeed;
        public float AccelerationSeconds => _accelerationSeconds;
        public float HitRadius => _hitRadius;
        public float MaxLifetime => _maxLifetime;
        public float LaunchInterval => _launchInterval;
        public float BoostDurationSeconds => _boostDurationSeconds;
        public float MaxTurnRateDegrees => _maxTurnRateDegrees;
        public float TurnEngageSeconds => _turnEngageSeconds;
        public float TurnAccelerationDegrees => _turnAccelerationDegrees;
        public Vector3 VisualScale => _visualScale;
        public Vector3 MeshForwardEulerOffset => _meshForwardEulerOffset;
    }
}
