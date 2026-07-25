using TypingDefense.Application.Events;
using TypingDefense.Domain.Common;
using UnityEngine;
using UnityEngine.UI;

namespace TypingDefense.Presentation.HUD
{
    /// <summary>
    /// Full-screen impact frame that flashes in and fades out on ground hits.
    /// Assign your red border sprite on Frame Sprite.
    /// </summary>
    public sealed class ImpactScreenFlashPresenter : MonoBehaviour
    {
        [SerializeField] private Image _frameImage;
        [SerializeField] private Sprite _frameSprite;
        [SerializeField] private Color _tint = new Color(1f, 0.12f, 0.1f, 1f);
        [SerializeField] [Range(0f, 1f)] private float _peakAlpha = 0.92f;
        [SerializeField] private float _fadeInSeconds = 0.05f;
        [SerializeField] private float _holdSeconds = 0.08f;
        [SerializeField] private float _fadeOutSeconds = 0.55f;

        private GameplayEventBus _events;
        private float _alpha;
        private float _phaseTime;
        private FlashPhase _phase = FlashPhase.Idle;

        private enum FlashPhase
        {
            Idle,
            FadeIn,
            Hold,
            FadeOut
        }

        public void Configure(Image frameImage, Sprite frameSprite = null)
        {
            _frameImage = frameImage;
            if (frameSprite != null)
                _frameSprite = frameSprite;
            ApplySprite();
            SetVisibleAlpha(0f);
        }

        public void SetFrameSprite(Sprite sprite)
        {
            _frameSprite = sprite;
            ApplySprite();
        }

        public void Initialize(GameplayEventBus events)
        {
            Shutdown();
            _events = events;
            if (_events == null)
                return;

            ApplySprite();
            SetVisibleAlpha(0f);
            _events.ThreatImpacted += OnImpacted;
            _events.DefenseHitFeedback += OnDefenseHit;
        }

        public void Shutdown()
        {
            if (this == null)
                return;

            if (_events != null)
            {
                _events.ThreatImpacted -= OnImpacted;
                _events.DefenseHitFeedback -= OnDefenseHit;
            }
            _events = null;
            _phase = FlashPhase.Idle;
            SetVisibleAlpha(0f);
        }

        private void OnDestroy()
        {
            if (_events != null)
            {
                _events.ThreatImpacted -= OnImpacted;
                _events.DefenseHitFeedback -= OnDefenseHit;
            }
            _events = null;
        }

        private void Update()
        {
            if (_phase == FlashPhase.Idle || _frameImage == null)
                return;

            _phaseTime += Time.unscaledDeltaTime;

            switch (_phase)
            {
                case FlashPhase.FadeIn:
                {
                    var t = _fadeInSeconds <= 0.0001f ? 1f : Mathf.Clamp01(_phaseTime / _fadeInSeconds);
                    SetVisibleAlpha(Mathf.Lerp(0f, _peakAlpha, t));
                    if (t >= 1f)
                        EnterPhase(FlashPhase.Hold);
                    break;
                }
                case FlashPhase.Hold:
                {
                    SetVisibleAlpha(_peakAlpha);
                    if (_phaseTime >= _holdSeconds)
                        EnterPhase(FlashPhase.FadeOut);
                    break;
                }
                case FlashPhase.FadeOut:
                {
                    var t = _fadeOutSeconds <= 0.0001f ? 1f : Mathf.Clamp01(_phaseTime / _fadeOutSeconds);
                    SetVisibleAlpha(Mathf.Lerp(_peakAlpha, 0f, t));
                    if (t >= 1f)
                    {
                        _phase = FlashPhase.Idle;
                        SetVisibleAlpha(0f);
                    }
                    break;
                }
            }
        }

        private void OnImpacted(ThreatId id, Vector3 _)
        {
            TriggerFlash();
        }

        private void OnDefenseHit()
        {
            TriggerFlash();
        }

        private void TriggerFlash()
        {
            if (_frameImage != null)
                _frameImage.transform.SetAsLastSibling();
            EnterPhase(FlashPhase.FadeIn);
        }

        private void EnterPhase(FlashPhase phase)
        {
            _phase = phase;
            _phaseTime = 0f;
        }

        private void ApplySprite()
        {
            if (_frameImage == null)
                return;

            if (_frameSprite != null)
            {
                _frameImage.sprite = _frameSprite;
                _frameImage.type = Image.Type.Simple;
                _frameImage.preserveAspect = false;
            }

            _frameImage.raycastTarget = false;
        }

        private void SetVisibleAlpha(float alpha)
        {
            _alpha = Mathf.Clamp01(alpha);
            if (_frameImage == null)
                return;

            var color = _tint;
            color.a = _alpha;
            _frameImage.color = color;
            _frameImage.enabled = _alpha > 0.001f;
        }
    }
}
