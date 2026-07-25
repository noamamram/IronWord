using TypingDefense.Application.Events;
using TypingDefense.Domain.Common;
using TypingDefense.Domain.Sessions;
using UnityEngine;

namespace TypingDefense.Presentation.Cameras
{
    /// <summary>
    /// Military camera anchored to the Iron Dome in world space (ignores FBX local axes),
    /// with subtle sway and impact shake.
    /// </summary>
    public sealed class CameraPresenter : MonoBehaviour
    {
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private Camera _camera;

        [Header("Follow Iron Dome")]
        [SerializeField] private Transform _followTarget;
        [Tooltip("If false (default), offsets are world-space from the dome position — stable with FBX rotations.")]
        [SerializeField] private bool _useLocalOffsets;
        [Tooltip("Camera position relative to dome (world space by default).")]
        [SerializeField] private Vector3 _followOffset = new Vector3(0f, 8f, -18f);
        [Tooltip("Look-at point relative to dome (world space by default).")]
        [SerializeField] private Vector3 _lookAtOffset = new Vector3(0f, 3f, 45f);
        [SerializeField] private float _positionFollowSpeed = 10f;
        [SerializeField] private float _rotationFollowSpeed = 12f;

        [Header("Idle Sway")]
        [SerializeField] private float _swayAmplitude = 0.05f;
        [SerializeField] private float _swayFrequency = 0.12f;

        [Header("Launch Shake")]
        [Tooltip("Subtle screen kick when an interceptor leaves the battery.")]
        [SerializeField] private float _launchImpulse = 0.38f;

        [Header("Intercept Shake")]
        [Tooltip("Light kick when an interceptor successfully destroys a threat.")]
        [SerializeField] private float _interceptImpulse = 0.28f;

        [Header("Impact Shake")]
        [SerializeField] private float _impactTrauma = 0.85f;
        [SerializeField] private float _maxTrauma = 1f;
        [SerializeField] private float _traumaDecay = 1.35f;
        [SerializeField] private float _maxPositionalShake = 0.55f;
        [SerializeField] private float _maxAngularShake = 7.5f;
        [SerializeField] private float _shakeFrequency = 28f;
        [Tooltip("Leave 0 to keep FOV stable. Small values (2-4) add a subtle zoom punch on impact.")]
        [SerializeField] private float _fovKickDegrees = 0f;
        [SerializeField] private float _fovRecoverSpeed = 10f;
        [SerializeField] private float _baseFov = 100f;

        private const float MinFov = 30f;
        private const float MaxFov = 120f;

        private Vector3 _fallbackRestPosition;
        private Quaternion _fallbackRestRotation;
        private float _trauma;
        private float _noiseSeed;
        private GameplayEventBus _events;
        private float _time;
        private bool _subscribed;
        private Vector3 _smoothedPosition;
        private Quaternion _smoothedRotation;
        private bool _hasSmoothState;

        private void Awake()
        {
            // Older scenes serialized a near-invisible launch kick (trauma is squared).
            if (_launchImpulse < 0.2f)
                _launchImpulse = 0.38f;

            EnsureValidBaseFov();
            ApplyBaseFovImmediate();
        }

        private void OnValidate()
        {
            EnsureValidBaseFov();
        }

        private void EnsureValidBaseFov()
        {
            if (_baseFov < MinFov || _baseFov > MaxFov || float.IsNaN(_baseFov) || float.IsInfinity(_baseFov))
                _baseFov = 100f;
        }

        private void ApplyBaseFovImmediate()
        {
            EnsureValidBaseFov();
            if (_camera == null && _cameraTransform != null)
                _camera = _cameraTransform.GetComponent<Camera>();
            if (_camera == null)
                _camera = GetComponent<Camera>();
            if (_camera != null)
                _camera.fieldOfView = _baseFov;
        }

        public void SetFollowTarget(Transform followTarget)
        {
            _followTarget = followTarget;
            _hasSmoothState = false;
            if (this != null && isActiveAndEnabled)
                SnapToDesiredPose();
        }

        public void SetFollowFraming(Vector3 followOffset, Vector3 lookAtOffset, bool useLocalOffsets = false)
        {
            _followOffset = followOffset;
            _lookAtOffset = lookAtOffset;
            _useLocalOffsets = useLocalOffsets;
            _hasSmoothState = false;
            if (this != null && isActiveAndEnabled)
                SnapToDesiredPose();
        }

        public void SetBaseFov(float fov)
        {
            _baseFov = fov;
            EnsureValidBaseFov();
            ApplyBaseFovImmediate();
        }

        public void Initialize(GameplayEventBus events)
        {
            if (this == null)
                return;

            Shutdown();

            if (_cameraTransform == null)
                _cameraTransform = transform;
            if (_camera == null && _cameraTransform != null)
                _camera = _cameraTransform.GetComponent<Camera>();

            if (_cameraTransform != null)
            {
                _fallbackRestPosition = _cameraTransform.position;
                _fallbackRestRotation = _cameraTransform.rotation;
            }

            // Always lock to a known gameplay FOV so kicks/editor state can't leave it broken.
            ApplyBaseFovImmediate();

            _noiseSeed = Random.value * 100f;
            _trauma = 0f;
            _events = events;

            SnapToDesiredPose();

            if (_events == null)
                return;

            _events.InterceptorLaunched += OnLaunch;
            _events.ThreatIntercepted += OnInterceptSuccess;
            _events.ThreatImpacted += OnImpact;
            _events.DefenseHitFeedback += OnDefenseHit;
            _events.PhaseChanged += OnPhaseChanged;
            _subscribed = true;
        }

        public void Shutdown()
        {
            if (this == null)
                return;

            if (_subscribed && _events != null)
            {
                _events.InterceptorLaunched -= OnLaunch;
                _events.ThreatIntercepted -= OnInterceptSuccess;
                _events.ThreatImpacted -= OnImpact;
                _events.DefenseHitFeedback -= OnDefenseHit;
                _events.PhaseChanged -= OnPhaseChanged;
            }

            _subscribed = false;
            _events = null;
            _trauma = 0f;

            ApplyBaseFovImmediate();
        }

        private void OnDestroy()
        {
            if (_subscribed && _events != null)
            {
                _events.InterceptorLaunched -= OnLaunch;
                _events.ThreatIntercepted -= OnInterceptSuccess;
                _events.ThreatImpacted -= OnImpact;
                _events.DefenseHitFeedback -= OnDefenseHit;
                _events.PhaseChanged -= OnPhaseChanged;
            }

            _subscribed = false;
            _events = null;
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null)
                return;

            _time += Time.deltaTime;
            _trauma = Mathf.Clamp01(_trauma - _traumaDecay * Time.deltaTime);

            EvaluateDesiredPose(out var desiredPosition, out var desiredRotation);

            if (!_hasSmoothState)
            {
                _smoothedPosition = desiredPosition;
                _smoothedRotation = desiredRotation;
                _hasSmoothState = true;
            }
            else
            {
                var posT = 1f - Mathf.Exp(-_positionFollowSpeed * Time.deltaTime);
                var rotT = 1f - Mathf.Exp(-_rotationFollowSpeed * Time.deltaTime);
                _smoothedPosition = Vector3.Lerp(_smoothedPosition, desiredPosition, posT);
                _smoothedRotation = Quaternion.Slerp(_smoothedRotation, desiredRotation, rotT);
            }

            ApplyPoseWithShake(_smoothedPosition, _smoothedRotation);
        }

        private void SnapToDesiredPose()
        {
            if (_cameraTransform == null)
            {
                _cameraTransform = transform;
                _camera = _cameraTransform != null ? _cameraTransform.GetComponent<Camera>() : null;
            }

            if (_cameraTransform == null)
                return;

            EvaluateDesiredPose(out var desiredPosition, out var desiredRotation);
            _smoothedPosition = desiredPosition;
            _smoothedRotation = desiredRotation;
            _hasSmoothState = true;
            ApplyPoseWithShake(desiredPosition, desiredRotation);
        }

        private void ApplyPoseWithShake(Vector3 basePosition, Quaternion baseRotation)
        {
            var shakeStrength = _trauma * _trauma;
            var shakePos = Vector3.zero;
            var shakeRot = Vector3.zero;

            if (shakeStrength > 0.0001f)
            {
                var t = _time * _shakeFrequency;
                shakePos = new Vector3(
                    (Mathf.PerlinNoise(_noiseSeed, t) * 2f - 1f) * _maxPositionalShake,
                    (Mathf.PerlinNoise(_noiseSeed + 11f, t) * 2f - 1f) * _maxPositionalShake,
                    (Mathf.PerlinNoise(_noiseSeed + 23f, t) * 2f - 1f) * _maxPositionalShake * 0.35f) * shakeStrength;

                shakeRot = new Vector3(
                    (Mathf.PerlinNoise(_noiseSeed + 31f, t) * 2f - 1f) * _maxAngularShake,
                    (Mathf.PerlinNoise(_noiseSeed + 47f, t) * 2f - 1f) * _maxAngularShake,
                    (Mathf.PerlinNoise(_noiseSeed + 59f, t) * 2f - 1f) * _maxAngularShake * 0.65f) * shakeStrength;
            }

            var sway = new Vector3(
                Mathf.Sin(_time * _swayFrequency) * _swayAmplitude,
                Mathf.Cos(_time * _swayFrequency * 0.7f) * _swayAmplitude * 0.5f,
                0f);

            var orientedOffset = baseRotation * (sway + shakePos);
            _cameraTransform.position = basePosition + orientedOffset;
            _cameraTransform.rotation = baseRotation * Quaternion.Euler(shakeRot);

            if (_camera != null)
            {
                EnsureValidBaseFov();
                if (_fovKickDegrees <= 0.001f)
                {
                    _camera.fieldOfView = _baseFov;
                }
                else
                {
                    var targetFov = Mathf.Clamp(_baseFov + _fovKickDegrees * shakeStrength, MinFov, MaxFov);
                    var next = Mathf.Lerp(_camera.fieldOfView, targetFov, Time.deltaTime * _fovRecoverSpeed);
                    _camera.fieldOfView = Mathf.Clamp(next, MinFov, MaxFov);
                }
            }
        }

        private void EvaluateDesiredPose(out Vector3 position, out Quaternion rotation)
        {
            if (_followTarget != null)
            {
                if (_useLocalOffsets)
                {
                    position = _followTarget.TransformPoint(_followOffset);
                    var lookAtLocal = _followTarget.TransformPoint(_lookAtOffset);
                    var forwardLocal = lookAtLocal - position;
                    if (forwardLocal.sqrMagnitude < 0.0001f)
                        forwardLocal = _followTarget.forward;
                    rotation = Quaternion.LookRotation(forwardLocal.normalized, Vector3.up);
                }
                else
                {
                    // World-space framing: reliable regardless of FBX import rotation.
                    position = _followTarget.position + _followOffset;
                    var lookAt = _followTarget.position + _lookAtOffset;
                    var forward = lookAt - position;
                    if (forward.sqrMagnitude < 0.0001f)
                        forward = Vector3.forward;
                    rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                }

                return;
            }

            position = _fallbackRestPosition;
            rotation = _fallbackRestRotation;
        }

        private void OnLaunch(ThreatId threatId, InterceptorId interceptorId, Vector3 _)
        {
            // Soft kick — noticeable but much lighter than ground-impact trauma.
            AddTrauma(Mathf.Max(0.05f, _launchImpulse));
            _noiseSeed = Random.value * 100f;
        }

        private void OnInterceptSuccess(ThreatId threatId, Vector3 _)
        {
            AddTrauma(Mathf.Max(0.05f, _interceptImpulse));
            _noiseSeed = Random.value * 100f;
        }

        private void OnImpact(ThreatId threatId, Vector3 _)
        {
            AddTrauma(_impactTrauma);
            _noiseSeed = Random.value * 100f;
        }

        private void OnDefenseHit()
        {
            AddTrauma(_impactTrauma);
            _noiseSeed = Random.value * 100f;
        }

        private void OnPhaseChanged(SessionPhase phase)
        {
            if (phase == SessionPhase.GameOver)
                AddTrauma(1f);
        }

        private void AddTrauma(float amount)
        {
            _trauma = Mathf.Min(_maxTrauma, _trauma + amount);
        }
    }
}
