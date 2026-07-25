using System.Collections.Generic;
using TypingDefense.Application.Events;
using TypingDefense.Domain.Common;
using TypingDefense.Domain.Sessions;
using TypingDefense.Simulation.Interceptors;
using TypingDefense.Simulation.Threats;
using UnityEngine;

namespace TypingDefense.Presentation.Gameplay
{
    public readonly struct ThreatVisualSpec
    {
        public string DefinitionId { get; }
        public GameObject Prefab { get; }
        public Vector3 Scale { get; }
        public Vector3 MeshForwardEulerOffset { get; }

        public ThreatVisualSpec(string definitionId, GameObject prefab, Vector3 scale, Vector3 meshForwardEulerOffset)
        {
            DefinitionId = definitionId ?? string.Empty;
            Prefab = prefab;
            Scale = scale.sqrMagnitude < 0.0001f ? Vector3.one : scale;
            MeshForwardEulerOffset = meshForwardEulerOffset;
        }
    }

    public sealed class GameplayPresenter : MonoBehaviour
    {
        [SerializeField] private Transform _threatRoot;
        [SerializeField] private Transform _interceptorRoot;
        [SerializeField] private GameObject _threatPrefab;
        [SerializeField] private GameObject _interceptorPrefab;
        [SerializeField] private Vector3 _threatVisualScale = Vector3.one;
        [SerializeField] private Vector3 _interceptorVisualScale = Vector3.one;
        [SerializeField] private Vector3 _threatMeshForwardEulerOffset;
        [SerializeField] private Vector3 _interceptorMeshForwardEulerOffset;

        private IThreatRegistry _threatRegistry;
        private InterceptorSimulation _interceptorSimulation;
        private GameplayEventBus _events;

        private readonly Dictionary<string, ThreatVisualSpec> _threatVisuals =
            new Dictionary<string, ThreatVisualSpec>();
        private readonly Dictionary<string, Stack<ThreatView>> _threatPoolsByDefinition =
            new Dictionary<string, Stack<ThreatView>>();
        private readonly Dictionary<int, string> _threatViewDefinitionIds = new Dictionary<int, string>();

        private readonly Dictionary<int, ThreatView> _threatViews = new Dictionary<int, ThreatView>();
        private readonly Dictionary<int, InterceptorView> _interceptorViews = new Dictionary<int, InterceptorView>();
        private readonly Stack<InterceptorView> _interceptorPool = new Stack<InterceptorView>();

        public void Configure(Transform threatRoot, Transform interceptorRoot, GameObject threatPrefab = null, GameObject interceptorPrefab = null)
        {
            _threatRoot = threatRoot;
            _interceptorRoot = interceptorRoot;
            _threatPrefab = threatPrefab;
            _interceptorPrefab = interceptorPrefab;
        }

        public void SetVisualPrefabs(GameObject threatPrefab, GameObject interceptorPrefab)
        {
            _threatPrefab = threatPrefab;
            _interceptorPrefab = interceptorPrefab;
        }

        public void SetThreatVisuals(IEnumerable<ThreatVisualSpec> visuals)
        {
            _threatVisuals.Clear();
            if (visuals == null)
                return;

            foreach (var visual in visuals)
            {
                if (string.IsNullOrEmpty(visual.DefinitionId))
                    continue;
                _threatVisuals[visual.DefinitionId] = visual;
            }
        }

        public void SetVisualScales(Vector3 threatScale, Vector3 interceptorScale)
        {
            _threatVisualScale = threatScale.sqrMagnitude < 0.0001f ? Vector3.one : threatScale;
            _interceptorVisualScale = interceptorScale.sqrMagnitude < 0.0001f ? Vector3.one : interceptorScale;
        }

        public void SetMeshForwardOffsets(Vector3 threatEulerOffset, Vector3 interceptorEulerOffset)
        {
            _threatMeshForwardEulerOffset = threatEulerOffset;
            _interceptorMeshForwardEulerOffset = interceptorEulerOffset;
        }

        public void Initialize(
            IThreatRegistry threatRegistry,
            InterceptorSimulation interceptorSimulation,
            GameplayEventBus events)
        {
            _threatRegistry = threatRegistry;
            _interceptorSimulation = interceptorSimulation;
            _events = events;

            _events.ThreatSpawned += OnThreatSpawned;
            _events.ThreatIntercepted += OnThreatRemoved;
            _events.ThreatImpacted += OnThreatRemoved;
            _events.InterceptorLaunched += OnInterceptorLaunched;
        }

        public void Shutdown()
        {
            if (_events != null)
            {
                _events.ThreatSpawned -= OnThreatSpawned;
                _events.ThreatIntercepted -= OnThreatRemoved;
                _events.ThreatImpacted -= OnThreatRemoved;
                _events.InterceptorLaunched -= OnInterceptorLaunched;
            }

            foreach (var pair in _threatViews)
                ReleaseThreatView(pair.Key, pair.Value);
            _threatViews.Clear();
            _threatViewDefinitionIds.Clear();

            foreach (var pair in _interceptorViews)
                ReleaseInterceptorView(pair.Value);
            _interceptorViews.Clear();

            _threatPoolsByDefinition.Clear();
            _interceptorPool.Clear();

            _threatRegistry = null;
            _interceptorSimulation = null;
            _events = null;
        }

        private void LateUpdate()
        {
            if (_threatRegistry == null)
                return;

            var threats = _threatRegistry.ActiveThreats;
            var liveThreatIds = new HashSet<int>();
            for (var i = 0; i < threats.Count; i++)
            {
                var threat = threats[i];
                liveThreatIds.Add(threat.Id.Value);
                if (_threatViews.TryGetValue(threat.Id.Value, out var view) && view != null)
                    view.Sync(threat);
            }

            var staleThreatIds = new List<int>();
            foreach (var pair in _threatViews)
            {
                if (!liveThreatIds.Contains(pair.Key))
                    staleThreatIds.Add(pair.Key);
            }

            for (var i = 0; i < staleThreatIds.Count; i++)
            {
                var id = staleThreatIds[i];
                ReleaseThreatView(id, _threatViews[id]);
                _threatViews.Remove(id);
            }

            if (_interceptorSimulation == null)
                return;

            var interceptors = _interceptorSimulation.Active;
            var liveIds = new HashSet<int>();
            for (var i = 0; i < interceptors.Count; i++)
            {
                var interceptor = interceptors[i];
                liveIds.Add(interceptor.Id.Value);
                if (!_interceptorViews.TryGetValue(interceptor.Id.Value, out var view) || view == null)
                {
                    view = AcquireInterceptorView();
                    view.Bind(interceptor.Id);
                    _interceptorViews[interceptor.Id.Value] = view;
                }

                view.Sync(interceptor.Position, interceptor.Velocity);
            }

            var removeBuffer = new List<int>();
            foreach (var pair in _interceptorViews)
            {
                if (!liveIds.Contains(pair.Key))
                    removeBuffer.Add(pair.Key);
            }

            for (var i = 0; i < removeBuffer.Count; i++)
            {
                ReleaseInterceptorView(_interceptorViews[removeBuffer[i]]);
                _interceptorViews.Remove(removeBuffer[i]);
            }
        }

        private void OnThreatSpawned(ThreatId id)
        {
            if (_threatViews.ContainsKey(id.Value))
                return;

            var definitionId = "basic_missile";
            ThreatRuntime threat = null;
            if (_threatRegistry != null && _threatRegistry.TryGet(id, out threat))
                definitionId = threat.DefinitionId.Value;

            var view = AcquireThreatView(definitionId);
            view.Bind(id);
            view.name = "Threat_" + id.Value + "_" + definitionId;
            _threatViews[id.Value] = view;
            _threatViewDefinitionIds[id.Value] = definitionId;

            if (threat != null)
                view.Sync(threat);
        }

        private void OnThreatRemoved(ThreatId id, Vector3 _)
        {
            if (!_threatViews.TryGetValue(id.Value, out var view))
                return;

            ReleaseThreatView(id.Value, view);
            _threatViews.Remove(id.Value);
        }

        private void OnInterceptorLaunched(ThreatId threatId, InterceptorId interceptorId, Vector3 _)
        {
            if (_interceptorViews.ContainsKey(interceptorId.Value))
                return;

            var view = AcquireInterceptorView();
            view.Bind(interceptorId);
            view.name = "Interceptor_" + interceptorId.Value;
            _interceptorViews[interceptorId.Value] = view;

            if (_interceptorSimulation != null &&
                _interceptorSimulation.TryGet(interceptorId, out var interceptor))
            {
                view.Sync(interceptor.Position, interceptor.Velocity);
            }
        }

        private ThreatView AcquireThreatView(string definitionId)
        {
            if (!_threatPoolsByDefinition.TryGetValue(definitionId, out var pool))
            {
                pool = new Stack<ThreatView>();
                _threatPoolsByDefinition[definitionId] = pool;
            }

            while (pool.Count > 0)
            {
                var pooled = pool.Pop();
                if (pooled != null)
                    return pooled;
            }

            ThreatVisualSpec spec;
            var hasSpec = _threatVisuals.TryGetValue(definitionId, out spec);
            var prefab = hasSpec && spec.Prefab != null ? spec.Prefab : _threatPrefab;
            var scale = hasSpec ? spec.Scale : _threatVisualScale;
            var meshOffset = hasSpec ? spec.MeshForwardEulerOffset : _threatMeshForwardEulerOffset;

            var go = prefab != null
                ? Instantiate(prefab, _threatRoot)
                : CreateFallbackThreat();
            var view = go.GetComponent<ThreatView>() ?? go.AddComponent<ThreatView>();
            view.MeshForwardEulerOffset = meshOffset;
            view.ApplyBaseScale(scale);
            go.SetActive(false);
            return view;
        }

        private void ReleaseThreatView(int threatIdValue, ThreatView view)
        {
            if (view == null)
                return;

            view.Unbind();
            if (view == null)
                return;

            if (!_threatViewDefinitionIds.TryGetValue(threatIdValue, out var definitionId))
                definitionId = "basic_missile";
            _threatViewDefinitionIds.Remove(threatIdValue);

            if (!_threatPoolsByDefinition.TryGetValue(definitionId, out var pool))
            {
                pool = new Stack<ThreatView>();
                _threatPoolsByDefinition[definitionId] = pool;
            }

            pool.Push(view);
        }

        private InterceptorView AcquireInterceptorView()
        {
            while (_interceptorPool.Count > 0)
            {
                var pooled = _interceptorPool.Pop();
                if (pooled != null)
                    return pooled;
            }

            var go = _interceptorPrefab != null
                ? Instantiate(_interceptorPrefab, _interceptorRoot)
                : CreateFallbackInterceptor();
            var view = go.GetComponent<InterceptorView>() ?? go.AddComponent<InterceptorView>();
            view.MeshForwardEulerOffset = _interceptorMeshForwardEulerOffset;
            view.ApplyBaseScale(_interceptorVisualScale);
            go.SetActive(false);
            return view;
        }

        private void ReleaseInterceptorView(InterceptorView view)
        {
            if (view == null)
                return;

            view.Unbind();
            if (view != null)
                _interceptorPool.Push(view);
        }

        private GameObject CreateFallbackThreat()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Threat";
            go.transform.SetParent(_threatRoot, false);
            go.transform.localScale = new Vector3(0.8f, 1.6f, 0.8f);
            Destroy(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(FindLitShader())
            {
                color = new Color(0.85f, 0.2f, 0.15f)
            };
            go.AddComponent<ThreatView>();
            go.SetActive(false);
            return go;
        }

        private GameObject CreateFallbackInterceptor()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Interceptor";
            go.transform.SetParent(_interceptorRoot, false);
            go.transform.localScale = Vector3.one * 0.45f;
            Destroy(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(FindLitShader())
            {
                color = new Color(0.2f, 0.85f, 0.35f)
            };
            go.AddComponent<InterceptorView>();
            go.SetActive(false);
            return go;
        }

        private static Shader FindLitShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                   ?? Shader.Find("HDRP/Lit")
                   ?? Shader.Find("Standard")
                   ?? Shader.Find("Unlit/Color");
        }
    }
}
