using System;
using System.Collections.Generic;
using TypingDefense.Application.Events;
using TypingDefense.Application.Flow;
using TypingDefense.Application.Gameplay;
using TypingDefense.Application.Spawning;
using TypingDefense.Application.Targeting;
using TypingDefense.Content.Definitions;
using TypingDefense.Content.Languages;
using TypingDefense.Content.Threats;
using TypingDefense.Content.UI;
using TypingDefense.Domain.Common;
using TypingDefense.Domain.Languages;
using TypingDefense.Domain.Scoring;
using TypingDefense.Domain.Sessions;
using TypingDefense.Domain.Statistics;
using TypingDefense.Domain.Typing;
using TypingDefense.Infrastructure.Input;
using TypingDefense.Infrastructure.Pooling;
using TypingDefense.Infrastructure.UnityTime;
using TypingDefense.Presentation.Cameras;
using TypingDefense.Presentation.Gameplay;
using TypingDefense.Presentation.HUD;
using TypingDefense.Presentation.Menus;
using TypingDefense.Presentation.UI;
using TypingDefense.Presentation.VFX;
using TypingDefense.Simulation.Interceptors;
using TypingDefense.Simulation.Threats;
using TypingDefense.Simulation.Trajectories;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TypingDefense.Bootstrap
{
    /// <summary>
    /// Composition Root — the only place that constructs and wires systems.
    /// </summary>
    public sealed class GameInstaller : MonoBehaviour
    {
        [Header("Optional Content")]
        [SerializeField] private GameplayContentCatalog _catalog;
        [SerializeField] private MenuButtonStyle _menuButtonStyle;

        [Header("Scene References (auto-built if empty)")]
        [SerializeField] private Camera _worldCamera;
        [Tooltip("Iron Dome root (camera follow). Launch muzzles use Launch Point Transforms below.")]
        [SerializeField] private Transform _launcherTransform;
        [Tooltip("Interceptor exit points. Boost follows each point's blue (forward) axis. Random pick per launch.")]
        [SerializeField] private Transform[] _launchPointTransforms;
        [SerializeField] private Transform[] _spawnPointTransforms;
        [SerializeField] private Transform[] _impactPointTransforms;
        [SerializeField] private GameplayPresenter _gameplayPresenter;
        [SerializeField] private ThreatMarkerPresenter _markerPresenter;
        [SerializeField] private GameplayHudView _hudView;
        [SerializeField] private MainMenuView _mainMenuView;
        [SerializeField] private ResultsView _resultsView;
        [SerializeField] private CameraPresenter _cameraPresenter;
        [SerializeField] private ExplosionVfxPresenter _explosionVfxPresenter;
        [SerializeField] private GameObject _explosionPrefab;
        [SerializeField] private GameObject _launchExplosionPrefab;
        [SerializeField] private ImpactScreenFlashPresenter _impactFlashPresenter;
        [SerializeField] private Sprite _impactFrameSprite;

        [Header("Sensor")]
        [SerializeField] private float _sensorRange = 450f;
        [SerializeField] private float _sensorAngle = 75f;

        [Header("Camera-Relative Threat Spawn")]
        [SerializeField] private float _spawnMinDistance = 120f;
        [SerializeField] private float _spawnMaxDistance = 230f;
        [SerializeField] private float _spawnMinHeight = 38f;
        [SerializeField] private float _spawnMaxHeight = 88f;
        [Tooltip("Fraction of camera FOV used for left/right spawn fan (1 = full FOV).")]
        [SerializeField] [Range(0.35f, 1f)] private float _spawnHorizontalFovFraction = 0.85f;
        [SerializeField] private float _impactMinDistance = 18f;
        [SerializeField] private float _impactMaxDistance = 78f;
        [SerializeField] private float _impactHalfWidth = 48f;

        [Header("Interceptor Launch Points")]
        [SerializeField] private int _autoLaunchPointCount = 5;
        [SerializeField] private float _autoLaunchPointRadius = 3.5f;
        [SerializeField] private float _autoLaunchPointHeight = 4.5f;

        [Header("Random")]
        [Tooltip("0 = randomize spawn areas every compose. Letter order always reshuffles on each Start/Retry.")]
        [SerializeField] private int _sessionSeed = 0;

        [Header("Explosion Scale")]
        [SerializeField] private float _interceptExplosionScale = 0.9f;
        [SerializeField] private float _impactExplosionScale = 1.35f;
        [SerializeField] private float _launchExplosionScale = 0.55f;

        private UnityTextInputAdapter _input;
        private UnityGameClock _clock;
        private GameSessionController _sessionController;
        private GameplayEventBus _events;
        private SessionState _sessionState;
        private string _pendingLanguageId = "en";
        private ThreatRuntimePool _threatPool;
        private InterceptorRuntimePool _interceptorPool;
        private bool _builtUi;

        private void Awake()
        {
            EnsureSceneScaffold();
            EnsureUi();
        }

        private void Start()
        {
            ComposeAndEnterMenu();
        }

        private void Update()
        {
            _input?.Tick();

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                    _sessionController?.TogglePause();
            }

            if (_sessionController == null || _clock == null)
                return;

            _sessionController.Tick(_clock.DeltaTime);
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private void ComposeAndEnterMenu(bool showMenu = true)
        {
            Teardown();
            // Teardown only unbinds session state — re-acquire scene presenters if needed.
            EnsureSceneScaffold();
            EnsureUi();

            var languageProfile = ResolveLanguageProfile(_pendingLanguageId);
            var rules = languageProfile.CreateRules();
            var tokens = languageProfile.CreateTokenRepository();
            var sessionConfig = _catalog != null && _catalog.DefaultMode != null
                ? _catalog.DefaultMode.CreateSessionConfig()
                : new SessionConfig { MaxStrikes = 3, CountdownSeconds = 3f, EndOnMaxStrikes = true };

            var threatArchetypes = BuildThreatArchetypes();

            _events = new GameplayEventBus();
            _sessionState = new SessionState();
            _clock = new UnityGameClock();
            _input = new UnityTextInputAdapter(
                PhysicalTextMapperFactory.Create(_pendingLanguageId));
            _input.Bind();

            _threatPool = new ThreatRuntimePool();
            _interceptorPool = new InterceptorRuntimePool();

            var registry = new ThreatRegistry();
            var threatSim = new ThreatSimulation(registry);
            var sensor = new SensorSystem(registry);
            var interceptorSim = new InterceptorSimulation();
            var trajectoryFactory = new DefaultTrajectoryFactory();
            var resolvedSeed = _sessionSeed != 0
                ? _sessionSeed
                : Environment.TickCount ^ Guid.NewGuid().GetHashCode();

            var tokenAllocator = new TokenAllocator(tokens, rules, resolvedSeed);
            var scoreService = new ScoreService(new ScoringProfileData());
            var stats = new TrainingStatsCollector();
            var difficulty = new CurveDifficultyPolicy();
            var targetSelector = new PriorityTypingTargetSelector();

            var spawnPoints = CollectPoints(_spawnPointTransforms, null);
            var impactPoints = CollectPoints(_impactPointTransforms, null);

            ISpawnPointProvider spawnProvider;
            IImpactPointProvider impactProvider;

            if (spawnPoints.Count > 0)
            {
                spawnProvider = new ListPointProvider(spawnPoints, resolvedSeed);
            }
            else
            {
                spawnProvider = new CameraRelativeSkySpawnProvider(
                    () => _worldCamera != null ? _worldCamera.transform : null,
                    _spawnMinDistance,
                    _spawnMaxDistance,
                    _spawnMinHeight,
                    _spawnMaxHeight,
                    _spawnHorizontalFovFraction,
                    resolvedSeed);
            }

            if (impactPoints.Count > 0)
            {
                impactProvider = new ListPointProvider(impactPoints, resolvedSeed + 17);
            }
            else
            {
                impactProvider = new CameraRelativeImpactProvider(
                    () => _worldCamera != null ? _worldCamera.transform : null,
                    () =>
                    {
                        var dome = ResolveCameraFollowTarget();
                        return dome != null ? dome.position : Vector3.zero;
                    },
                    _impactMinDistance,
                    _impactMaxDistance,
                    _impactHalfWidth,
                    groundY: 0f,
                    seed: resolvedSeed + 91);
            }

            EnsureLaunchPoints();
            var launchPos = ResolveLaunchMuzzle().Position;

            sensor.Configure(
                _worldCamera != null ? _worldCamera.transform.position : launchPos,
                _worldCamera != null ? _worldCamera.transform.forward : Vector3.forward,
                _sensorRange,
                _sensorAngle);

            ThreatSpawnDirector spawnDirector = null;
            LauncherService launcher = null;

            spawnDirector = new ThreatSpawnDirector(
                registry,
                spawnProvider,
                impactProvider,
                trajectoryFactory,
                tokenAllocator,
                rules,
                difficulty,
                _events,
                () => _threatPool.Get(),
                () => _sessionState.ElapsedPlaySeconds,
                () => PerformanceSnapshot.From(stats),
                threatArchetypes,
                resolvedSeed,
                () => _sessionState.CanSpawnThreats);

            launcher = new LauncherService(
                registry,
                interceptorSim,
                _events,
                () => _interceptorPool.Get(),
                ResolveLaunchMuzzle,
                cruiseSpeed: _catalog?.BasicInterceptor != null ? _catalog.BasicInterceptor.CruiseSpeed : 95f,
                initialSpeed: _catalog?.BasicInterceptor != null ? _catalog.BasicInterceptor.InitialSpeed : 18f,
                accelerationSeconds: _catalog?.BasicInterceptor != null ? _catalog.BasicInterceptor.AccelerationSeconds : 0.85f,
                hitRadius: _catalog?.BasicInterceptor != null ? _catalog.BasicInterceptor.HitRadius : 3f,
                maxLifetime: _catalog?.BasicInterceptor != null ? _catalog.BasicInterceptor.MaxLifetime : 8f,
                launchInterval: _catalog?.BasicInterceptor != null ? _catalog.BasicInterceptor.LaunchInterval : 0.08f,
                boostDurationSeconds: _catalog?.BasicInterceptor != null ? _catalog.BasicInterceptor.BoostDurationSeconds : 0.28f,
                maxTurnRateDegrees: _catalog?.BasicInterceptor != null ? _catalog.BasicInterceptor.MaxTurnRateDegrees : 105f,
                turnEngageSeconds: _catalog?.BasicInterceptor != null ? _catalog.BasicInterceptor.TurnEngageSeconds : 0.65f,
                turnAccelerationDegrees: _catalog?.BasicInterceptor != null ? _catalog.BasicInterceptor.TurnAccelerationDegrees : 140f);

            if (_gameplayPresenter != null)
            {
                _gameplayPresenter.SetVisualPrefabs(
                    _catalog?.BasicThreat != null ? _catalog.BasicThreat.Prefab : null,
                    _catalog?.BasicInterceptor != null ? _catalog.BasicInterceptor.Prefab : null);
                _gameplayPresenter.SetThreatVisuals(BuildThreatVisualSpecs());
                _gameplayPresenter.SetVisualScales(
                    _catalog?.BasicThreat != null ? _catalog.BasicThreat.VisualScale : Vector3.one,
                    _catalog?.BasicInterceptor != null ? _catalog.BasicInterceptor.VisualScale : Vector3.one);
                _gameplayPresenter.SetMeshForwardOffsets(
                    _catalog?.BasicThreat != null ? _catalog.BasicThreat.MeshForwardEulerOffset : Vector3.zero,
                    _catalog?.BasicInterceptor != null ? _catalog.BasicInterceptor.MeshForwardEulerOffset : Vector3.zero);
            }

            var typing = new TypingCoordinator(
                _input,
                rules,
                targetSelector,
                registry,
                launcher,
                _events,
                scoreService,
                stats,
                () => _worldCamera != null ? _worldCamera.transform.position : ResolveLaunchMuzzle().Position,
                () => _sessionState.ElapsedPlaySeconds,
                () => _sessionState.CanAcceptTyping,
                () =>
                {
                    if (_sessionState.Phase != SessionPhase.Playing || _sessionState.DefenseFailed)
                        return false;

                    var gameOver = _sessionState.RegisterStrike(sessionConfig.MaxStrikes);
                    _events.PublishStrikeCountChanged(_sessionState.Strikes);
                    if (gameOver && sessionConfig.EndOnMaxStrikes)
                        _events.PublishSessionEnded();
                    return gameOver;
                });

            typing.Start();

            _sessionController = new GameSessionController(
                _sessionState,
                sessionConfig,
                threatSim,
                sensor,
                interceptorSim,
                spawnDirector,
                launcher,
                typing,
                registry,
                tokenAllocator,
                scoreService,
                stats,
                _events,
                threat => _threatPool.Release(threat),
                interceptor => _interceptorPool.Release(interceptor));

            if (_gameplayPresenter != null)
                _gameplayPresenter.Initialize(registry, interceptorSim, _events);

            if (_markerPresenter != null)
            {
                _markerPresenter.Initialize(
                    registry,
                    _worldCamera,
                    _worldCamera != null ? _worldCamera.transform.position : ResolveLaunchMuzzle().Position);
            }

            if (_hudView != null)
                _hudView.Initialize(_events, _sessionState, sessionConfig.MaxStrikes);

            if (_impactFlashPresenter != null)
                _impactFlashPresenter.Initialize(_events);

            if (_cameraPresenter != null)
            {
                var follow = ResolveCameraFollowTarget();
                _cameraPresenter.SetFollowTarget(follow);
                _cameraPresenter.SetBaseFov(100f);
                if (_worldCamera != null)
                    _worldCamera.fieldOfView = 100f;
                _cameraPresenter.Initialize(_events);
            }

            EnsureExplosionVfx();
            if (_explosionVfxPresenter != null)
            {
                _explosionVfxPresenter.SetScales(
                    _interceptExplosionScale,
                    _impactExplosionScale,
                    _launchExplosionScale);
                _explosionVfxPresenter.Initialize(_events);
            }

            WireMenus();

            _events.PhaseChanged += OnPhaseChanged;
            _events.SessionEnded += OnSessionEnded;

            _sessionController.StartFromMenu(_pendingLanguageId);
            if (showMenu)
            {
                _mainMenuView?.Show();
                _resultsView?.Hide();
                _input.Enabled = false;
            }
        }

        private void WireMenus()
        {
            if (_mainMenuView != null)
            {
                _mainMenuView.StartRequested -= OnStartRequested;
                _mainMenuView.StartRequested += OnStartRequested;
            }

            if (_resultsView != null)
            {
                _resultsView.RetryRequested -= OnRetryRequested;
                _resultsView.MenuRequested -= OnMenuRequested;
                _resultsView.RetryRequested += OnRetryRequested;
                _resultsView.MenuRequested += OnMenuRequested;
            }
        }

        private void OnStartRequested(string languageId)
        {
            _pendingLanguageId = languageId ?? "en";
            ComposeAndEnterMenu(showMenu: false);
            _input?.SetMapper(PhysicalTextMapperFactory.Create(_pendingLanguageId));
            _mainMenuView?.Hide();
            _resultsView?.Hide();
            _sessionController.StartCountdown();
            _input.Enabled = true;
        }

        private void OnRetryRequested()
        {
            _resultsView?.Hide();
            _sessionController.StartCountdown();
            _input.Enabled = true;
        }

        private void OnMenuRequested()
        {
            _resultsView?.Hide();
            _sessionController.StartFromMenu(_pendingLanguageId);
            _mainMenuView?.Show();
            _input.Enabled = false;
        }

        private void OnPhaseChanged(SessionPhase phase)
        {
            _input.Enabled = phase == SessionPhase.Playing &&
                             _sessionState != null &&
                             _sessionState.CanAcceptTyping;

            if (phase == SessionPhase.Menu)
                _mainMenuView?.Show();
            else
                _mainMenuView?.Hide();
        }

        private void OnSessionEnded()
        {
            _input.Enabled = false;
            var summary =
                $"SCORE {_sessionController.Score.Score}\n" +
                $"ACCURACY {(_sessionController.Stats.Accuracy * 100f):0.0}%\n" +
                $"CORRECT {_sessionController.Stats.CorrectInputs}   MISS {_sessionController.Stats.IncorrectInputs}\n" +
                $"AVG REACTION {_sessionController.Stats.AverageReactionSeconds:0.00}s\n" +
                $"MAX STREAK {_sessionController.Stats.MaxStreak}\n" +
                $"STRIKES {_sessionState.Strikes}";

            // Keep Playing so the clock continues; only stop spawning (DefenseFailed).
            _resultsView?.Show(summary);
        }

        private void Teardown()
        {
            if (_events != null)
            {
                _events.PhaseChanged -= OnPhaseChanged;
                _events.SessionEnded -= OnSessionEnded;
            }

            if (_mainMenuView != null)
                _mainMenuView.StartRequested -= OnStartRequested;
            if (_resultsView != null)
            {
                _resultsView.RetryRequested -= OnRetryRequested;
                _resultsView.MenuRequested -= OnMenuRequested;
            }

            SafeShutdown(_gameplayPresenter);
            SafeShutdown(_hudView);
            SafeShutdown(_impactFlashPresenter);
            SafeShutdown(_cameraPresenter);
            SafeShutdown(_explosionVfxPresenter);
            // Dispose session before destroying hierarchy so views can unbind safely.
            _sessionController?.Dispose();
            _sessionController = null;
            _input?.Dispose();
            _events?.ClearAll();

            _input = null;
            _events = null;
            // Keep scene-owned presenter references. Nulling them here skipped
            // GameplayPresenter.Initialize on the next Compose — no missiles/interceptors.
        }

        private static void SafeShutdown(MonoBehaviour behaviour)
        {
            // Unity overloads == null for destroyed objects.
            if (behaviour == null)
                return;

            switch (behaviour)
            {
                case GameplayPresenter gameplay:
                    gameplay.Shutdown();
                    break;
                case GameplayHudView hud:
                    hud.Shutdown();
                    break;
                case ImpactScreenFlashPresenter flash:
                    flash.Shutdown();
                    break;
                case CameraPresenter camera:
                    camera.Shutdown();
                    break;
                case ExplosionVfxPresenter explosion:
                    explosion.Shutdown();
                    break;
            }
        }

        private Transform ResolveCameraFollowTarget()
        {
            var follow = _launcherTransform;
            if (follow != null)
            {
                // If this is a LaunchPoint child, follow the dome root instead.
                if (follow.parent != null &&
                    follow.name.IndexOf("Launch", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    follow = follow.parent;
                }

                return follow;
            }

            // Auto-find common dome object names if Launcher Transform was left empty.
            var byName = GameObject.Find("ironDome");
            if (byName == null)
                byName = GameObject.Find("IronDome");
            if (byName == null)
                byName = GameObject.Find("IronDomeLauncher");

            return byName != null ? byName.transform : null;
        }

        /// <summary>
        /// Picks a random launch muzzle: exact world position + LaunchPoint forward axis.
        /// Orient each LaunchPoint so its blue (forward) axis matches the barrel direction.
        /// </summary>
        private LaunchMuzzle ResolveLaunchMuzzle()
        {
            EnsureLaunchPoints();

            var live = CollectLiveLaunchPoints();
            if (live.Count > 0)
            {
                var pick = live[UnityEngine.Random.Range(0, live.Count)];
                return new LaunchMuzzle(pick.position, ResolveMuzzleDirection(pick));
            }

            if (_launcherTransform != null)
            {
                if (_launcherTransform.name.IndexOf("Launch", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new LaunchMuzzle(_launcherTransform.position, ResolveMuzzleDirection(_launcherTransform));

                var bounds = CollectWorldBounds(_launcherTransform);
                if (bounds.HasValue)
                {
                    var b = bounds.Value;
                    return new LaunchMuzzle(
                        new Vector3(b.center.x, b.max.y + 1.5f, b.center.z),
                        Vector3.up);
                }

                return new LaunchMuzzle(_launcherTransform.position + Vector3.up * 8f, Vector3.up);
            }

            var dome = ResolveCameraFollowTarget();
            if (dome != null)
            {
                var bounds = CollectWorldBounds(dome);
                if (bounds.HasValue)
                {
                    var b = bounds.Value;
                    return new LaunchMuzzle(
                        new Vector3(b.center.x, b.max.y + 1.5f, b.center.z),
                        Vector3.up);
                }

                return new LaunchMuzzle(dome.position + Vector3.up * 8f, Vector3.up);
            }

            return new LaunchMuzzle(new Vector3(0f, 8f, 0f), Vector3.up);
        }

        private static Vector3 ResolveMuzzleDirection(Transform muzzle)
        {
            if (muzzle == null)
                return Vector3.up;

            // Blue axis (forward) of the LaunchPoint is the boost direction.
            var forward = muzzle.forward;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.up;
        }

        private List<Transform> CollectLiveLaunchPoints()
        {
            var list = new List<Transform>();
            if (_launchPointTransforms == null)
                return list;

            for (var i = 0; i < _launchPointTransforms.Length; i++)
            {
                if (_launchPointTransforms[i] != null)
                    list.Add(_launchPointTransforms[i]);
            }

            return list;
        }

        private void EnsureLaunchPoints()
        {
            if (CollectLiveLaunchPoints().Count > 0)
                return;

            var dome = ResolveCameraFollowTarget();
            if (dome == null && _launcherTransform != null)
                dome = _launcherTransform;
            if (dome == null)
                return;

            // Legacy single LaunchPoint child — promote into the array.
            if (_launcherTransform != null &&
                _launcherTransform != dome &&
                _launcherTransform.name.IndexOf("Launch", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _launchPointTransforms = new[] { _launcherTransform };
                return;
            }

            var count = Mathf.Max(1, _autoLaunchPointCount);
            var root = dome.Find("LaunchPoints");
            if (root == null)
            {
                var go = new GameObject("LaunchPoints");
                root = go.transform;
                root.SetParent(dome, false);
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.identity;
            }

            var created = new List<Transform>(count);
            for (var i = 0; i < count; i++)
            {
                var name = "LaunchPoint_" + (i + 1);
                var existing = root.Find(name);
                Transform point;
                if (existing != null)
                {
                    point = existing;
                }
                else
                {
                    point = new GameObject(name).transform;
                    point.SetParent(root, false);
                    var angle = (360f / count) * i;
                    var rad = angle * Mathf.Deg2Rad;
                    var localPos = new Vector3(
                        Mathf.Sin(rad) * _autoLaunchPointRadius,
                        _autoLaunchPointHeight,
                        Mathf.Cos(rad) * _autoLaunchPointRadius);
                    point.localPosition = localPos;
                    // Aim barrel outward and upward from the ring — blue axis = launch direction.
                    var localAim = new Vector3(Mathf.Sin(rad), 0.85f, Mathf.Cos(rad)).normalized;
                    point.localRotation = Quaternion.LookRotation(localAim, Vector3.up);
                }

                created.Add(point);
            }

            _launchPointTransforms = created.ToArray();
        }

        private static Bounds? CollectWorldBounds(Transform root)
        {
            if (root == null)
                return null;

            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
                return null;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private TrainingLanguageProfile ResolveLanguageProfile(string languageId)
        {
            if (_catalog != null)
            {
                var resolved = _catalog.ResolveLanguage(languageId);
                if (resolved != null)
                    return resolved;
            }

            if (string.Equals(languageId, "he", System.StringComparison.OrdinalIgnoreCase))
            {
                var hebrew = ScriptableObject.CreateInstance<TrainingLanguageProfile>();
                hebrew.ApplyHebrewDefaults();
                return hebrew;
            }

            return ResolveEnglishProfile();
        }

        private TrainingLanguageProfile ResolveEnglishProfile()
        {
            if (_catalog != null && _catalog.English != null)
                return _catalog.English;

            var runtime = ScriptableObject.CreateInstance<TrainingLanguageProfile>();
            runtime.ApplyEnglishDefaults();
            return runtime;
        }

        private List<ThreatSpawnArchetype> BuildThreatArchetypes()
        {
            var list = new List<ThreatSpawnArchetype>();
            if (_catalog?.BasicThreat != null)
                list.Add(ToArchetype(_catalog.BasicThreat));
            if (_catalog?.SpecialThreat != null)
                list.Add(ToArchetype(_catalog.SpecialThreat));

            if (list.Count == 0)
            {
                list.Add(new ThreatSpawnArchetype(
                    new DefinitionId("basic_missile"),
                    minTokenLength: 1,
                    maxTokenLength: 1));
            }

            return list;
        }

        private static ThreatSpawnArchetype ToArchetype(ThreatDefinition def)
        {
            return new ThreatSpawnArchetype(
                def.DefinitionId,
                def.BaseFlightDuration,
                def.MinFlightDuration,
                def.ArcHeight,
                def.BaseScore,
                def.SpawnCost,
                def.SelectionWeight,
                def.MinTokenLength,
                def.MaxTokenLength,
                def.UnlockAfterSeconds,
                def.WeightGrowthPerMinute);
        }

        private List<ThreatVisualSpec> BuildThreatVisualSpecs()
        {
            var list = new List<ThreatVisualSpec>();
            if (_catalog?.BasicThreat != null)
            {
                list.Add(new ThreatVisualSpec(
                    _catalog.BasicThreat.Id,
                    _catalog.BasicThreat.Prefab,
                    _catalog.BasicThreat.VisualScale,
                    _catalog.BasicThreat.MeshForwardEulerOffset));
            }

            if (_catalog?.SpecialThreat != null)
            {
                list.Add(new ThreatVisualSpec(
                    _catalog.SpecialThreat.Id,
                    _catalog.SpecialThreat.Prefab,
                    _catalog.SpecialThreat.VisualScale,
                    _catalog.SpecialThreat.MeshForwardEulerOffset));
            }

            return list;
        }

        private static List<Vector3> CollectPoints(Transform[] transforms, List<Vector3> fallback)
        {
            var list = new List<Vector3>();
            if (transforms != null)
            {
                for (var i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i] != null)
                        list.Add(transforms[i].position);
                }
            }

            if (list.Count > 0)
                return list;

            return fallback ?? new List<Vector3>();
        }

        private void EnsureExplosionVfx()
        {
            if (_explosionPrefab == null)
            {
#if UNITY_EDITOR
                _explosionPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/EffectExamples/FireExplosionEffects/Prefabs/BigExplosionEffect.prefab");
#endif
                if (_explosionPrefab == null)
                    Debug.LogWarning("IronWord: assign BigExplosionEffect to GameInstaller.Explosion Prefab.");
            }

            if (_launchExplosionPrefab == null)
            {
#if UNITY_EDITOR
                _launchExplosionPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/EffectExamples/FireExplosionEffects/Prefabs/SmallExplosionEffect.prefab");
#endif
                if (_launchExplosionPrefab == null)
                    Debug.LogWarning("IronWord: assign SmallExplosionEffect to GameInstaller.Launch Explosion Prefab.");
            }

            if (_explosionVfxPresenter == null)
            {
                var go = new GameObject("ExplosionVfxPresenter");
                _explosionVfxPresenter = go.AddComponent<ExplosionVfxPresenter>();
            }

            _explosionVfxPresenter.Configure(_explosionPrefab, _launchExplosionPrefab);
        }

        private void EnsureSceneScaffold()
        {
            if (_worldCamera == null)
            {
                var camGo = new GameObject("GameplayCamera");
                _worldCamera = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
                camGo.transform.position = new Vector3(0f, 8f, -18f);
                camGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
                _worldCamera.fieldOfView = 100f;
                _worldCamera.nearClipPlane = 0.3f;
                _worldCamera.farClipPlane = 500f;
                camGo.AddComponent<AudioListener>();
                _cameraPresenter = camGo.AddComponent<CameraPresenter>();
                _cameraPresenter.SetBaseFov(100f);
            }
            else if (_worldCamera != null)
            {
                _worldCamera.fieldOfView = 100f;
            }

            if (_cameraPresenter == null && _worldCamera != null)
            {
                _cameraPresenter = _worldCamera.GetComponent<CameraPresenter>();
                if (_cameraPresenter == null)
                    _cameraPresenter = _worldCamera.gameObject.AddComponent<CameraPresenter>();
            }

            if (_cameraPresenter != null)
                _cameraPresenter.SetBaseFov(100f);

            if (_launcherTransform == null)
            {
                var launcher = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                launcher.name = "IronDomeLauncher";
                launcher.transform.position = new Vector3(0f, 1.5f, 0f);
                launcher.transform.localScale = new Vector3(4f, 1.5f, 4f);
                Destroy(launcher.GetComponent<Collider>());
                var renderer = launcher.GetComponent<MeshRenderer>();
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                renderer.sharedMaterial = new Material(shader) { color = new Color(0.35f, 0.4f, 0.45f) };
                _launcherTransform = launcher.transform;

                var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dome.name = "DomeShell";
                dome.transform.SetParent(launcher.transform, false);
                dome.transform.localPosition = Vector3.up * 0.8f;
                dome.transform.localScale = new Vector3(1.6f, 0.9f, 1.6f);
                Destroy(dome.GetComponent<Collider>());
                var domeRenderer = dome.GetComponent<MeshRenderer>();
                domeRenderer.sharedMaterial = new Material(shader)
                {
                    color = new Color(0.25f, 0.55f, 0.85f, 0.35f)
                };
            }

            EnsureLaunchPoints();

            if (GameObject.Find("GroundPlane") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "GroundPlane";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(20f, 1f, 20f);
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                ground.GetComponent<MeshRenderer>().sharedMaterial = new Material(shader)
                {
                    color = new Color(0.18f, 0.22f, 0.16f)
                };
            }

            if (_gameplayPresenter == null)
                _gameplayPresenter = FindFirstObjectByType<GameplayPresenter>();

            if (_gameplayPresenter == null)
            {
                var root = new GameObject("GameplayPresenter");
                var threatRoot = new GameObject("Threats").transform;
                threatRoot.SetParent(root.transform, false);
                var interceptorRoot = new GameObject("Interceptors").transform;
                interceptorRoot.SetParent(root.transform, false);
                _gameplayPresenter = root.AddComponent<GameplayPresenter>();
                _gameplayPresenter.Configure(threatRoot, interceptorRoot);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.4f, 0.5f);
            if (FindFirstObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.95f, 0.85f);
                light.intensity = 1.1f;
                lightGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            }
        }

        private void EnsureUi()
        {
            if (_builtUi && _mainMenuView != null && _hudView != null && _resultsView != null && _markerPresenter != null)
            {
                EnsureImpactFlashOnExistingCanvas();
                return;
            }

            var canvasGo = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            // HUD
            var hudGo = CreatePanel(canvasGo.transform, "HUD", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -140f));
            var score = CreateText(hudGo.transform, "Score", new Vector2(40f, -30f), 28, TextAnchor.UpperLeft);
            var combo = CreateText(hudGo.transform, "Combo", new Vector2(40f, -70f), 20, TextAnchor.UpperLeft);
            var strikes = CreateText(hudGo.transform, "Strikes", new Vector2(-40f, -30f), 24, TextAnchor.UpperRight, true);
            var timer = CreateText(hudGo.transform, "Timer", new Vector2(0f, -30f), 32, TextAnchor.UpperCenter);
            var phase = CreateText(hudGo.transform, "Phase", new Vector2(0f, -70f), 18, TextAnchor.UpperCenter);
            var prompt = CreateText(hudGo.transform, "Prompt", new Vector2(0f, -110f), 18, TextAnchor.UpperCenter);
            _hudView = hudGo.AddComponent<GameplayHudView>();
            _hudView.ConfigureRuntime(score, combo, strikes, timer, phase, prompt);

            // Markers root
            var markerRootGo = new GameObject("ThreatMarkers", typeof(RectTransform));
            markerRootGo.transform.SetParent(canvasGo.transform, false);
            var markerRoot = markerRootGo.GetComponent<RectTransform>();
            markerRoot.anchorMin = Vector2.zero;
            markerRoot.anchorMax = Vector2.one;
            markerRoot.offsetMin = Vector2.zero;
            markerRoot.offsetMax = Vector2.zero;
            _markerPresenter = markerRootGo.AddComponent<ThreatMarkerPresenter>();
            _markerPresenter.Configure(canvas, markerRoot, _worldCamera);

            // Main menu
            var menuRoot = CreateFullPanel(canvasGo.transform, "MainMenu", new Color(0.02f, 0.04f, 0.06f, 0.92f));
            var title = CreateText(menuRoot.transform, "Title", new Vector2(0f, 200f), 56, TextAnchor.MiddleCenter);
            title.text = "IRONWORD";
            title.fontStyle = FontStyle.Bold;
            var subtitle = CreateText(menuRoot.transform, "Subtitle", new Vector2(0f, 130f), 18, TextAnchor.MiddleCenter);
            subtitle.text = "TYPE TO DEFEND";
            subtitle.color = new Color(0.75f, 0.82f, 0.88f, 1f);
            var langLabel = CreateText(menuRoot.transform, "LangLabel", new Vector2(0f, 40f), 16, TextAnchor.MiddleCenter);
            langLabel.color = new Color(0.65f, 0.72f, 0.78f, 1f);
            var style = ResolveMenuButtonStyle();
            var enBtn = CreateStyledButton(
                menuRoot.transform,
                "EnglishBtn",
                new Vector2(-140f, -50f),
                style.EnglishLabel,
                style,
                rtl: false);
            var heBtn = CreateStyledButton(
                menuRoot.transform,
                "HebrewBtn",
                new Vector2(140f, -50f),
                style.HebrewLabel,
                style,
                rtl: true);
            var startBtn = CreateStyledButton(
                menuRoot.transform,
                "StartBtn",
                new Vector2(0f, -140f),
                "START",
                style,
                rtl: false);
            startBtn.gameObject.SetActive(false);
            _mainMenuView = menuRoot.AddComponent<MainMenuView>();
            _mainMenuView.ConfigureRuntime(menuRoot, enBtn, heBtn, startBtn, langLabel);

            // Results
            var resultsRoot = CreateFullPanel(canvasGo.transform, "Results", new Color(0.05f, 0.08f, 0.1f, 0.9f));
            resultsRoot.SetActive(false);
            var summary = CreateText(resultsRoot.transform, "Summary", new Vector2(0f, 40f), 22, TextAnchor.MiddleCenter);
            summary.rectTransform.sizeDelta = new Vector2(700f, 280f);
            var retryBtn = CreateStyledButton(
                resultsRoot.transform, "RetryBtn", new Vector2(-120f, -180f), "RETRY", ResolveMenuButtonStyle(), false);
            var menuBtn = CreateStyledButton(
                resultsRoot.transform, "MenuBtn", new Vector2(120f, -180f), "MENU", ResolveMenuButtonStyle(), false);
            _resultsView = resultsRoot.AddComponent<ResultsView>();
            _resultsView.ConfigureRuntime(resultsRoot, summary, retryBtn, menuBtn);

            CreateImpactFlash(canvasGo.transform);
            _builtUi = true;
        }

        private void EnsureImpactFlashOnExistingCanvas()
        {
            if (_impactFlashPresenter != null)
            {
                if (_impactFrameSprite != null)
                    _impactFlashPresenter.SetFrameSprite(_impactFrameSprite);
                return;
            }

            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
                return;

            CreateImpactFlash(canvas.transform);
        }

        private void CreateImpactFlash(Transform canvasTransform)
        {
            if (_impactFlashPresenter != null)
                return;

            var flashGo = new GameObject("ImpactFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            flashGo.transform.SetParent(canvasTransform, false);
            var flashRect = flashGo.GetComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            var flashImage = flashGo.GetComponent<Image>();
            flashImage.raycastTarget = false;
            flashImage.color = new Color(1f, 0.12f, 0.1f, 0f);
            _impactFlashPresenter = flashGo.AddComponent<ImpactScreenFlashPresenter>();
            _impactFlashPresenter.Configure(flashImage, _impactFrameSprite);
            flashGo.transform.SetAsLastSibling();
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return go;
        }

        private static GameObject CreateFullPanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            int fontSize,
            TextAnchor anchor,
            bool right = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(480f, 48f);
            rect.anchoredPosition = anchoredPos;
            if (right)
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
            }
            else if (anchor == TextAnchor.UpperCenter || anchor == TextAnchor.MiddleCenter)
            {
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                if (anchor == TextAnchor.MiddleCenter)
                {
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                }
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
            }

            var text = go.GetComponent<Text>();
            text.font = UiFonts.Resolve();
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = name;
            return text;
        }

        private MenuButtonStyle ResolveMenuButtonStyle()
        {
            if (_menuButtonStyle != null)
                return _menuButtonStyle;

            // Assign DefaultMenuButtonStyle on GameInstaller in the Inspector for full control.
            _menuButtonStyle = ScriptableObject.CreateInstance<MenuButtonStyle>();
            return _menuButtonStyle;
        }

        private static Button CreateStyledButton(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            string label,
            MenuButtonStyle style,
            bool rtl)
        {
            style ??= ScriptableObject.CreateInstance<MenuButtonStyle>();
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = style.Size;
            rect.anchoredPosition = anchoredPos;

            var image = go.GetComponent<Image>();
            image.color = Color.white;

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = style.NormalColor;
            colors.highlightedColor = style.HighlightedColor;
            colors.pressedColor = style.PressedColor;
            colors.selectedColor = style.HighlightedColor;
            colors.disabledColor = style.DisabledColor;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.targetGraphic = image;

            var text = CreateText(go.transform, "Label", Vector2.zero, style.FontSize, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.color = style.LabelColor;
            text.fontStyle = style.FontStyle;
            text.text = rtl ? MenuButtonStyle.FormatRtlForLegacyText(label) : label;

            return button;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            string label,
            Vector2? size = null)
        {
            var style = ScriptableObject.CreateInstance<MenuButtonStyle>();
            return CreateStyledButton(parent, name, anchoredPos, label, style, rtl: false);
        }
    }
}
