using TypingDefense.Domain.Common;
using UnityEngine;

namespace TypingDefense.Presentation.Gameplay
{
    public sealed class InterceptorView : MonoBehaviour
    {
        [Tooltip("Corrects model axis so nose matches Unity +Z before flight facing is applied.")]
        [SerializeField] private Vector3 _meshForwardEulerOffset;

        public InterceptorId BoundId { get; private set; }
        private bool _bound;
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _lastForward = Vector3.forward;

        public Vector3 MeshForwardEulerOffset
        {
            get => _meshForwardEulerOffset;
            set => _meshForwardEulerOffset = value;
        }

        public void Bind(InterceptorId id)
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

        public void Sync(Vector3 position, Vector3 velocity)
        {
            if (!_bound || this == null)
                return;

            transform.position = position;

            if (velocity.sqrMagnitude > 0.0001f)
                _lastForward = velocity.normalized;

            transform.rotation = FlightFacing.FromVelocity(
                velocity,
                _meshForwardEulerOffset,
                _lastForward);
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
