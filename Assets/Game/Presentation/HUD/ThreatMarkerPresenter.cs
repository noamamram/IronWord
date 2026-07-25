using System.Collections.Generic;
using TypingDefense.Domain.Common;
using TypingDefense.Domain.Sessions;
using TypingDefense.Simulation.Threats;
using UnityEngine;
using UnityEngine.UI;

namespace TypingDefense.Presentation.HUD
{
    [System.Serializable]
    public sealed class ThreatMarkerStyle
    {
        public Vector2 FrameSize = new Vector2(72f, 72f);
        public float FrameThickness = 3f;
        public Vector2 InfoPanelSize = new Vector2(70f, 72f);
        public float InfoPanelGap = 8f;
        public int LetterFontSize = 28;
        public int DistanceFontSize = 14;
        public Color AvailableColor = new Color(0.2f, 0.95f, 0.35f, 0.95f);
        public Color LockedColor = new Color(1f, 0.85f, 0.2f, 0.95f);
        public Color EngagedColor = new Color(0.25f, 0.55f, 1f, 0.95f);
        public Color CriticalColor = new Color(1f, 0.25f, 0.2f, 0.95f);
        public Color LetterColor = Color.white;
        public Color DistanceColor = new Color(0.85f, 1f, 0.85f, 1f);
        public Sprite FrameSprite;
    }

    public sealed class ThreatMarkerView : MonoBehaviour
    {
        [SerializeField] private RectTransform _frame;
        [SerializeField] private Image _frameImage;
        [SerializeField] private Image[] _frameEdges;
        [SerializeField] private Text _letterText;
        [SerializeField] private Text _distanceText;
        [SerializeField] private RectTransform _infoPanel;

        private ThreatId _boundId;
        private bool _bound;
        private ThreatMarkerStyle _style;

        public ThreatId BoundId => _boundId;
        public bool IsBound => _bound;

        public void ConfigureRuntime(
            RectTransform frame,
            Image frameImage,
            Image[] frameEdges,
            Text letterText,
            Text distanceText,
            RectTransform infoPanel,
            ThreatMarkerStyle style)
        {
            _frame = frame;
            _frameImage = frameImage;
            _frameEdges = frameEdges;
            _letterText = letterText;
            _distanceText = distanceText;
            _infoPanel = infoPanel;
            _style = style;
        }

        public void Bind(ThreatId id)
        {
            _boundId = id;
            _bound = true;
            gameObject.SetActive(true);
        }

        public void Sync(ThreatRuntime threat, Camera worldCamera, RectTransform canvasRect, Vector3 sensorOrigin)
        {
            if (!_bound || threat == null || worldCamera == null)
                return;

            var screenPoint = worldCamera.WorldToScreenPoint(threat.Position);
            if (screenPoint.z < 0f)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPoint,
                    null,
                    out var localPoint))
            {
                ((RectTransform)transform).anchoredPosition = localPoint;
            }

            var frameColor = GetStateColor(threat);

            if (_letterText != null)
            {
                _letterText.text = threat.Challenge != null
                    ? FormatChallengeLabel(threat.Challenge)
                    : string.Empty;
                _letterText.color = frameColor;
            }

            if (_distanceText != null)
            {
                var distance = Vector3.Distance(sensorOrigin, threat.Position);
                _distanceText.text = $"{distance:0} m";
                _distanceText.color = frameColor;
            }

            ApplyFrameColor(frameColor);
        }

        private static string FormatChallengeLabel(TypingDefense.Domain.Typing.ITypingChallenge challenge)
        {
            var text = challenge?.RemainingText ?? string.Empty;
            if (string.IsNullOrEmpty(text) || !ContainsHebrew(text))
                return text;

            var chars = text.ToCharArray();
            System.Array.Reverse(chars);
            return new string(chars);
        }

        private static bool ContainsHebrew(string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c >= '\u0590' && c <= '\u05FF')
                    return true;
            }

            return false;
        }

        public void Unbind()
        {
            _bound = false;
            _boundId = default;
            gameObject.SetActive(false);
        }

        private void ApplyFrameColor(Color color)
        {
            if (_frameImage != null)
                _frameImage.color = color;

            if (_frameEdges == null)
                return;

            for (var i = 0; i < _frameEdges.Length; i++)
            {
                if (_frameEdges[i] != null)
                    _frameEdges[i].color = color;
            }
        }

        private Color GetStateColor(ThreatRuntime threat)
        {
            var style = _style ?? new ThreatMarkerStyle();
            if (threat.LifecycleState == ThreatLifecycleState.Engaged)
                return style.EngagedColor;
            if (threat.TimeToImpact < 3f)
                return style.CriticalColor;
            if (threat.TypingState == TypingTrackState.Locked)
                return style.LockedColor;
            return style.AvailableColor;
        }
    }

    public sealed class ThreatMarkerPresenter : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _markerRoot;
        [SerializeField] private ThreatMarkerView _markerPrefab;
        [SerializeField] private Camera _worldCamera;
        [SerializeField] private ThreatMarkerStyle _style = new ThreatMarkerStyle();

        private IThreatRegistry _registry;
        private Vector3 _sensorOrigin;
        private Sprite _runtimeFrameSprite;
        private readonly Dictionary<int, ThreatMarkerView> _markers = new Dictionary<int, ThreatMarkerView>();
        private readonly Stack<ThreatMarkerView> _pool = new Stack<ThreatMarkerView>();

        public ThreatMarkerStyle Style => _style;

        public void Configure(Canvas canvas, RectTransform markerRoot, Camera worldCamera, ThreatMarkerView markerPrefab = null)
        {
            _canvas = canvas;
            _markerRoot = markerRoot;
            _worldCamera = worldCamera;
            _markerPrefab = markerPrefab;
        }

        public void Initialize(IThreatRegistry registry, Camera worldCamera, Vector3 sensorOrigin)
        {
            _registry = registry;
            _worldCamera = worldCamera;
            _sensorOrigin = sensorOrigin;
        }

        public void SetSensorOrigin(Vector3 origin) => _sensorOrigin = origin;

        private void OnDestroy()
        {
            if (_runtimeFrameSprite != null)
            {
                if (_runtimeFrameSprite.texture != null)
                    Destroy(_runtimeFrameSprite.texture);
                Destroy(_runtimeFrameSprite);
                _runtimeFrameSprite = null;
            }
        }

        private void LateUpdate()
        {
            if (_registry == null || _worldCamera == null || _markerRoot == null)
                return;

            var live = new HashSet<int>();
            var threats = _registry.ActiveThreats;
            for (var i = 0; i < threats.Count; i++)
            {
                var threat = threats[i];
                if (threat.TrackState != SensorTrackState.Tracked)
                    continue;
                if (threat.LifecycleState != ThreatLifecycleState.Active &&
                    threat.LifecycleState != ThreatLifecycleState.Engaged)
                    continue;

                live.Add(threat.Id.Value);
                if (!_markers.TryGetValue(threat.Id.Value, out var marker))
                {
                    marker = Acquire();
                    marker.Bind(threat.Id);
                    _markers[threat.Id.Value] = marker;
                }

                marker.Sync(threat, _worldCamera, _markerRoot, _sensorOrigin);
            }

            var remove = new List<int>();
            foreach (var pair in _markers)
            {
                if (!live.Contains(pair.Key))
                    remove.Add(pair.Key);
            }

            for (var i = 0; i < remove.Count; i++)
            {
                Release(_markers[remove[i]]);
                _markers.Remove(remove[i]);
            }
        }

        private ThreatMarkerView Acquire()
        {
            if (_pool.Count > 0)
                return _pool.Pop();

            if (_markerPrefab != null)
                return Instantiate(_markerPrefab, _markerRoot);

            return CreateFallbackMarker();
        }

        private void Release(ThreatMarkerView view)
        {
            view.Unbind();
            _pool.Push(view);
        }

        private ThreatMarkerView CreateFallbackMarker()
        {
            var style = _style ?? new ThreatMarkerStyle();
            var frameSprite = style.FrameSprite != null ? style.FrameSprite : GetOrCreateHollowFrameSprite();

            var root = new GameObject("ThreatMarker", typeof(RectTransform));
            root.transform.SetParent(_markerRoot, false);
            var rootRect = root.GetComponent<RectTransform>();
            var totalWidth = style.FrameSize.x + style.InfoPanelGap + style.InfoPanelSize.x;
            var totalHeight = Mathf.Max(style.FrameSize.y, style.InfoPanelSize.y);
            rootRect.sizeDelta = new Vector2(totalWidth, totalHeight);
            rootRect.pivot = new Vector2(style.FrameSize.x * 0.5f / totalWidth, 0.5f);

            // Hollow frame centered on the missile (pivot keeps frame over world point).
            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(root.transform, false);
            var frameRect = frameGo.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0f, 0.5f);
            frameRect.anchorMax = new Vector2(0f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.anchoredPosition = new Vector2(style.FrameSize.x * 0.5f, 0f);
            frameRect.sizeDelta = style.FrameSize;
            var frameImage = frameGo.GetComponent<Image>();
            frameImage.sprite = frameSprite;
            frameImage.type = Image.Type.Sliced;
            frameImage.color = style.AvailableColor;
            frameImage.raycastTarget = false;
            frameImage.preserveAspect = true;

            // Info panel to the right of the frame.
            var infoGo = new GameObject("Info", typeof(RectTransform));
            infoGo.transform.SetParent(root.transform, false);
            var infoRect = infoGo.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0f, 0.5f);
            infoRect.anchorMax = new Vector2(0f, 0.5f);
            infoRect.pivot = new Vector2(0f, 0.5f);
            infoRect.anchoredPosition = new Vector2(style.FrameSize.x + style.InfoPanelGap, 0f);
            infoRect.sizeDelta = style.InfoPanelSize;

            var letterGo = new GameObject("Letter", typeof(RectTransform), typeof(Text));
            letterGo.transform.SetParent(infoGo.transform, false);
            var letterRect = letterGo.GetComponent<RectTransform>();
            letterRect.anchorMin = new Vector2(0f, 0.45f);
            letterRect.anchorMax = new Vector2(1f, 1f);
            letterRect.offsetMin = Vector2.zero;
            letterRect.offsetMax = Vector2.zero;
            var letterText = letterGo.GetComponent<Text>();
            letterText.alignment = TextAnchor.LowerLeft;
            letterText.fontSize = style.LetterFontSize;
            letterText.fontStyle = FontStyle.Bold;
            letterText.color = style.AvailableColor;
            letterText.font = UI.UiFonts.Resolve();
            letterText.raycastTarget = false;
            var letterOutline = letterGo.AddComponent<Outline>();
            letterOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            letterOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var distGo = new GameObject("Distance", typeof(RectTransform), typeof(Text));
            distGo.transform.SetParent(infoGo.transform, false);
            var distRect = distGo.GetComponent<RectTransform>();
            distRect.anchorMin = new Vector2(0f, 0f);
            distRect.anchorMax = new Vector2(1f, 0.45f);
            distRect.offsetMin = Vector2.zero;
            distRect.offsetMax = Vector2.zero;
            var distText = distGo.GetComponent<Text>();
            distText.alignment = TextAnchor.UpperLeft;
            distText.fontSize = style.DistanceFontSize;
            distText.color = style.AvailableColor;
            distText.font = UI.UiFonts.Resolve();
            distText.raycastTarget = false;
            var distOutline = distGo.AddComponent<Outline>();
            distOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            distOutline.effectDistance = new Vector2(1.2f, -1.2f);

            var view = root.AddComponent<ThreatMarkerView>();
            view.ConfigureRuntime(frameRect, frameImage, null, letterText, distText, infoRect, style);
            root.SetActive(false);
            return view;
        }

        private Sprite GetOrCreateHollowFrameSprite()
        {
            if (_runtimeFrameSprite != null)
                return _runtimeFrameSprite;

            const int size = 64;
            const int border = 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ThreatMarkerFrame"
            };

            var clear = new Color(0f, 0f, 0f, 0f);
            var solid = Color.white;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var onBorder =
                        x < border || x >= size - border ||
                        y < border || y >= size - border;
                    texture.SetPixel(x, y, onBorder ? solid : clear);
                }
            }

            texture.Apply(false, false);
            _runtimeFrameSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            _runtimeFrameSprite.name = "ThreatMarkerFrameSprite";
            return _runtimeFrameSprite;
        }
    }
}
