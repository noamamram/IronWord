using TypingDefense.Domain.Common;
using TypingDefense.Domain.Sessions;
using TypingDefense.Simulation.Threats;
using UnityEngine;

namespace TypingDefense.Presentation.Gameplay
{
    public sealed class ThreatView : MonoBehaviour
    {
        [Tooltip("Corrects model axis so nose matches Unity +Z before flight facing is applied.")]
        [SerializeField] private Vector3 _meshForwardEulerOffset;

        public ThreatId BoundId { get; private set; }
        private bool _bound;
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _lastForward = Vector3.forward;

        public Vector3 MeshForwardEulerOffset
        {
            get => _meshForwardEulerOffset;
            set => _meshForwardEulerOffset = value;
        }

        public void Bind(ThreatId id)
        {
            if (this == null)
                return;

            BoundId = id;
            _bound = true;
            _baseScale = transform.localScale;
            if (_baseScale.sqrMagnitude < 0.0001f)
                _baseScale = Vector3.one;
            _lastForward = transform.forward.sqrMagnitude > 0.0001f
                ? transform.forward
                : Vector3.forward;
            gameObject.SetActive(true);
        }

        public void ApplyBaseScale(Vector3 scale)
        {
            if (this == null)
                return;

            _baseScale = scale.sqrMagnitude < 0.0001f ? Vector3.one : scale;
            transform.localScale = _baseScale;
        }

        public void Sync(ThreatRuntime threat)
        {
            if (!_bound || threat == null || this == null)
                return;

            transform.position = threat.Position;

            var velocity = threat.Velocity;
            if (velocity.sqrMagnitude > 0.0001f)
                _lastForward = velocity.normalized;

            transform.rotation = FlightFacing.FromVelocity(
                velocity,
                _meshForwardEulerOffset,
                _lastForward);

            var engaged = threat.LifecycleState == ThreatLifecycleState.Engaged;
            transform.localScale = engaged ? _baseScale * 1.15f : _baseScale;
        }

        public void Unbind()
        {
            _bound = false;
            BoundId = default;
            if (this == null)
                return;

            transform.localScale = _baseScale;
            gameObject.SetActive(false);
        }
    }
}
