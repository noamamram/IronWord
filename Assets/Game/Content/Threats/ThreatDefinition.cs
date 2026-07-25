using TypingDefense.Domain.Common;
using UnityEngine;

namespace TypingDefense.Content.Threats
{
    [CreateAssetMenu(
        fileName = "ThreatDefinition",
        menuName = "IronWord/Threats/Threat Definition")]
    public sealed class ThreatDefinition : ScriptableObject
    {
        [SerializeField] private string _id = "basic_missile";
        [SerializeField] private GameObject _prefab;
        [SerializeField] private float _baseFlightDuration = 12f;
        [SerializeField] private float _minFlightDuration = 6f;
        [SerializeField] private float _arcHeight = 40f;
        [SerializeField] private int _baseScore = 100;
        [SerializeField] private float _spawnCost = 1f;
        [SerializeField] private float _selectionWeight = 1f;
        [Tooltip("Minimum typed elements required (1 = single letter).")]
        [SerializeField] private int _minTokenLength = 1;
        [Tooltip("Maximum typed elements required.")]
        [SerializeField] private int _maxTokenLength = 1;
        [Tooltip("Elapsed play seconds before this threat type can spawn.")]
        [SerializeField] private float _unlockAfterSeconds = 0f;
        [Tooltip("Extra selection weight gained per minute after unlock.")]
        [SerializeField] private float _weightGrowthPerMinute = 0f;
        [SerializeField] private Vector3 _visualScale = Vector3.one;
        [Tooltip("Euler correction so the mesh nose aligns with Unity +Z.")]
        [SerializeField] private Vector3 _meshForwardEulerOffset;

        public DefinitionId DefinitionId => new DefinitionId(_id);
        public string Id => _id;
        public GameObject Prefab => _prefab;
        public float BaseFlightDuration => _baseFlightDuration;
        public float MinFlightDuration => _minFlightDuration;
        public float ArcHeight => _arcHeight;
        public int BaseScore => _baseScore;
        public float SpawnCost => _spawnCost;
        public float SelectionWeight => _selectionWeight;
        public int MinTokenLength => Mathf.Max(1, _minTokenLength);
        public int MaxTokenLength => Mathf.Max(MinTokenLength, _maxTokenLength);
        public float UnlockAfterSeconds => Mathf.Max(0f, _unlockAfterSeconds);
        public float WeightGrowthPerMinute => Mathf.Max(0f, _weightGrowthPerMinute);
        public Vector3 VisualScale => _visualScale;
        public Vector3 MeshForwardEulerOffset => _meshForwardEulerOffset;

        public float EvaluateWeight(float elapsedSeconds)
        {
            if (elapsedSeconds < UnlockAfterSeconds)
                return 0f;

            var minutesAfterUnlock = (elapsedSeconds - UnlockAfterSeconds) / 60f;
            return SelectionWeight + minutesAfterUnlock * WeightGrowthPerMinute;
        }
    }
}
