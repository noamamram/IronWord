using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TypingDefense.Bootstrap;
using TypingDefense.Content.Definitions;
using TypingDefense.Content.GameModes;
using TypingDefense.Content.Languages;
using TypingDefense.Content.Threats;
using TypingDefense.Presentation.Gameplay;

namespace TypingDefense.Bootstrap.Editor
{
    public static class IronWordSetupMenu
    {
        private const string ContentRoot = "Assets/Game/Content";
        private const string PrefabRoot = ContentRoot + "/Prefabs";
        private const string ArtThreatPath = "Assets/3D/Missile/atmaca_3626t1904v.prefab";
        private const string ArtSpecialThreatPath = "Assets/3D/Cruise Missile/StormShadow.prefab";
        private const string ArtInterceptorPath = "Assets/3D/Irondome Missile/irondome missile.prefab";
        private const string ArtDomePath = "Assets/3D/ironDome/ironDome.prefab";

        [MenuItem("IronWord/Setup/Wire Art Prefabs")]
        public static void WireArtPrefabs()
        {
            WireArtPrefabsInternal(logToConsole: true);
        }

        /// <summary>
        /// Batch entry: Unity.exe -batchmode -projectPath ... -executeMethod TypingDefense.Bootstrap.Editor.IronWordSetupMenu.WireArtPrefabsBatch
        /// </summary>
        public static void WireArtPrefabsBatch()
        {
            try
            {
                WireArtPrefabsInternal(logToConsole: true);
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        private static void WireArtPrefabsInternal(bool logToConsole)
        {
            EnsureFolder(ContentRoot);
            EnsureFolder(ContentRoot + "/Languages");
            EnsureFolder(ContentRoot + "/Threats");
            EnsureFolder(ContentRoot + "/GameModes");
            EnsureFolder(ContentRoot + "/Definitions");
            EnsureFolder(PrefabRoot);

            var artThreat = AssetDatabase.LoadAssetAtPath<GameObject>(ArtThreatPath);
            var artSpecialThreat = AssetDatabase.LoadAssetAtPath<GameObject>(ArtSpecialThreatPath);
            var artInterceptor = AssetDatabase.LoadAssetAtPath<GameObject>(ArtInterceptorPath);

            if (artThreat == null)
            {
                Debug.LogError("Missing threat art prefab at " + ArtThreatPath);
                return;
            }

            if (artInterceptor == null)
            {
                Debug.LogError("Missing interceptor art prefab at " + ArtInterceptorPath);
                return;
            }

            var threatGameplayPrefab = CreateGameplayPrefab(
                PrefabRoot + "/Threat_Atmaca.prefab",
                "Threat_Atmaca",
                artThreat,
                visualScale: new Vector3(100f, 100f, 100f),
                addThreatView: true,
                addInterceptorView: false);

            GameObject specialGameplayPrefab = null;
            if (artSpecialThreat != null)
            {
                specialGameplayPrefab = CreateGameplayPrefab(
                    PrefabRoot + "/Threat_StormShadow.prefab",
                    "Threat_StormShadow",
                    artSpecialThreat,
                    visualScale: new Vector3(80f, 80f, 80f),
                    addThreatView: true,
                    addInterceptorView: false);
            }
            else
            {
                Debug.LogWarning("Missing StormShadow art at " + ArtSpecialThreatPath);
            }

            var interceptorGameplayPrefab = CreateGameplayPrefab(
                PrefabRoot + "/Interceptor_IronDome.prefab",
                "Interceptor_IronDome",
                artInterceptor,
                visualScale: new Vector3(1.25f, 1.25f, 1.25f),
                addThreatView: false,
                addInterceptorView: true);

            var english = LoadOrCreateLanguage();
            var hebrew = LoadOrCreateHebrewLanguage();
            var mode = LoadOrCreateMode();
            var threatDef = LoadOrCreateThreatDefinition(threatGameplayPrefab);
            var specialDef = specialGameplayPrefab != null
                ? LoadOrCreateStormShadowDefinition(specialGameplayPrefab)
                : null;
            var interceptorDef = LoadOrCreateInterceptorDefinition(interceptorGameplayPrefab);
            var catalog = LoadOrCreateCatalog(english, hebrew, mode, threatDef, specialDef, interceptorDef);

            EditorUtility.SetDirty(threatDef);
            if (specialDef != null)
                EditorUtility.SetDirty(specialDef);
            EditorUtility.SetDirty(interceptorDef);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AssignCatalogToOpenInstallers(catalog);
            AssignExplosionPrefabToOpenInstallers();

            if (logToConsole)
            {
                Debug.Log(
                    "IronWord art wired.\n" +
                    "Threat: " + AssetDatabase.GetAssetPath(threatGameplayPrefab) + "\n" +
                    "Special: " + (specialGameplayPrefab != null ? AssetDatabase.GetAssetPath(specialGameplayPrefab) : "(none)") + "\n" +
                    "Interceptor: " + AssetDatabase.GetAssetPath(interceptorGameplayPrefab) + "\n" +
                    "Catalog: " + AssetDatabase.GetAssetPath(catalog));
            }
        }

        private static void AssignExplosionPrefabToOpenInstallers()
        {
            var explosion = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/EffectExamples/FireExplosionEffects/Prefabs/BigExplosionEffect.prefab");
            var launchExplosion = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/EffectExamples/FireExplosionEffects/Prefabs/SmallExplosionEffect.prefab");
            if (explosion == null && launchExplosion == null)
                return;

            var installers = Object.FindObjectsByType<GameInstaller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < installers.Length; i++)
            {
                var so = new SerializedObject(installers[i]);
                if (explosion != null)
                    so.FindProperty("_explosionPrefab").objectReferenceValue = explosion;
                if (launchExplosion != null)
                    so.FindProperty("_launchExplosionPrefab").objectReferenceValue = launchExplosion;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(installers[i]);
            }
        }

        private static void AssignCatalogToOpenInstallers(GameplayContentCatalog catalog)
        {
            var installers = Object.FindObjectsByType<GameInstaller>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < installers.Length; i++)
            {
                var so = new SerializedObject(installers[i]);
                so.FindProperty("_catalog").objectReferenceValue = catalog;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(installers[i]);
            }

            if (installers.Length > 0)
                EditorSceneManager.MarkAllScenesDirty();
        }

        [MenuItem("IronWord/Setup/Create Default Content Assets")]
        public static void CreateDefaultContent()
        {
            WireArtPrefabs();
        }

        [MenuItem("IronWord/Setup/Create Vertical Slice Scene")]
        public static void CreateVerticalSliceScene()
        {
            WireArtPrefabs();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var installerGo = new GameObject("GameInstaller");
            var installer = installerGo.AddComponent<GameInstaller>();

            var catalog = AssetDatabase.LoadAssetAtPath<GameplayContentCatalog>(
                ContentRoot + "/Definitions/GameplayContentCatalog.asset");
            var so = new SerializedObject(installer);
            so.FindProperty("_catalog").objectReferenceValue = catalog;

            var mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.position = new Vector3(0f, 8f, -18f);
                mainCam.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
                so.FindProperty("_worldCamera").objectReferenceValue = mainCam;
            }

            var domePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtDomePath);
            if (domePrefab != null)
            {
                var dome = (GameObject)PrefabUtility.InstantiatePrefab(domePrefab);
                dome.name = "IronDome";
                dome.transform.position = new Vector3(0f, 0f, 0f);
                so.FindProperty("_launcherTransform").objectReferenceValue = dome.transform;

                var launchRoot = new GameObject("LaunchPoints").transform;
                launchRoot.SetParent(dome.transform, false);
                launchRoot.localPosition = Vector3.zero;

                const int launchCount = 5;
                const float radius = 3.5f;
                const float height = 4.5f;
                var launchProp = so.FindProperty("_launchPointTransforms");
                launchProp.arraySize = launchCount;
                for (var i = 0; i < launchCount; i++)
                {
                    var point = new GameObject("LaunchPoint_" + (i + 1)).transform;
                    point.SetParent(launchRoot, false);
                    var angle = (360f / launchCount) * i * Mathf.Deg2Rad;
                    point.localPosition = new Vector3(
                        Mathf.Sin(angle) * radius,
                        height,
                        Mathf.Cos(angle) * radius);
                    var localAim = new Vector3(Mathf.Sin(angle), 0.85f, Mathf.Cos(angle)).normalized;
                    point.localRotation = Quaternion.LookRotation(localAim, Vector3.up);
                    launchProp.GetArrayElementAtIndex(i).objectReferenceValue = point;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder("Assets/Scenes");
            var path = "Assets/Scenes/IronWord_VerticalSlice.unity";
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(path, true)
            };

            Debug.Log("Created scene: " + path + " — Open it and Press Play.");
        }

        private static GameObject CreateGameplayPrefab(
            string assetPath,
            string rootName,
            GameObject artPrefab,
            Vector3 visualScale,
            bool addThreatView,
            bool addInterceptorView)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(assetPath);

            var root = new GameObject(rootName);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = visualScale;

            var art = (GameObject)PrefabUtility.InstantiatePrefab(artPrefab);
            art.name = "Art";
            art.transform.SetParent(root.transform, false);
            art.transform.localPosition = Vector3.zero;
            art.transform.localRotation = Quaternion.identity;
            art.transform.localScale = Vector3.one;

            if (addThreatView)
            {
                var threatView = root.AddComponent<ThreatView>();
                threatView.MeshForwardEulerOffset = new Vector3(0f, 180f, 0f);
            }

            if (addInterceptorView)
            {
                var interceptorView = root.AddComponent<InterceptorView>();
                interceptorView.MeshForwardEulerOffset = new Vector3(-90f, 0f, 0f);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            if (Selection.activeGameObject == root ||
                (Selection.activeGameObject != null &&
                 Selection.activeGameObject.transform.IsChildOf(root.transform)))
            {
                Selection.activeObject = prefab;
            }

            Object.DestroyImmediate(root);
            return prefab;
        }

        private static TrainingLanguageProfile LoadOrCreateLanguage()
        {
            const string path = ContentRoot + "/Languages/English.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TrainingLanguageProfile>(path);
            if (existing != null)
                return existing;

            var english = ScriptableObject.CreateInstance<TrainingLanguageProfile>();
            english.ApplyEnglishDefaults();
            AssetDatabase.CreateAsset(english, path);
            return english;
        }

        private static TrainingLanguageProfile LoadOrCreateHebrewLanguage()
        {
            const string path = ContentRoot + "/Languages/Hebrew.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TrainingLanguageProfile>(path);
            if (existing != null)
                return existing;

            var hebrew = ScriptableObject.CreateInstance<TrainingLanguageProfile>();
            hebrew.ApplyHebrewDefaults();
            AssetDatabase.CreateAsset(hebrew, path);
            return hebrew;
        }

        private static GameModeDefinition LoadOrCreateMode()
        {
            const string path = ContentRoot + "/GameModes/LetterDefense.asset";
            var existing = AssetDatabase.LoadAssetAtPath<GameModeDefinition>(path);
            if (existing != null)
                return existing;

            var mode = ScriptableObject.CreateInstance<GameModeDefinition>();
            AssetDatabase.CreateAsset(mode, path);
            return mode;
        }

        private static ThreatDefinition LoadOrCreateThreatDefinition(GameObject gameplayPrefab)
        {
            const string path = ContentRoot + "/Threats/BasicMissile.asset";
            var threat = AssetDatabase.LoadAssetAtPath<ThreatDefinition>(path);
            if (threat == null)
            {
                threat = ScriptableObject.CreateInstance<ThreatDefinition>();
                AssetDatabase.CreateAsset(threat, path);
            }

            var so = new SerializedObject(threat);
            so.FindProperty("_id").stringValue = "atmaca";
            so.FindProperty("_prefab").objectReferenceValue = gameplayPrefab;
            so.FindProperty("_baseFlightDuration").floatValue = 12f;
            so.FindProperty("_minFlightDuration").floatValue = 6f;
            so.FindProperty("_arcHeight").floatValue = 40f;
            so.FindProperty("_baseScore").intValue = 100;
            so.FindProperty("_spawnCost").floatValue = 1f;
            so.FindProperty("_selectionWeight").floatValue = 1f;
            so.FindProperty("_minTokenLength").intValue = 1;
            so.FindProperty("_maxTokenLength").intValue = 1;
            so.FindProperty("_unlockAfterSeconds").floatValue = 0f;
            so.FindProperty("_weightGrowthPerMinute").floatValue = 0f;
            so.FindProperty("_visualScale").vector3Value = new Vector3(100f, 100f, 100f);
            so.FindProperty("_meshForwardEulerOffset").vector3Value = new Vector3(0f, 90f, -90f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return threat;
        }

        private static ThreatDefinition LoadOrCreateStormShadowDefinition(GameObject gameplayPrefab)
        {
            const string path = ContentRoot + "/Threats/StormShadow.asset";
            var threat = AssetDatabase.LoadAssetAtPath<ThreatDefinition>(path);
            if (threat == null)
            {
                threat = ScriptableObject.CreateInstance<ThreatDefinition>();
                AssetDatabase.CreateAsset(threat, path);
            }

            var so = new SerializedObject(threat);
            so.FindProperty("_id").stringValue = "storm_shadow";
            so.FindProperty("_prefab").objectReferenceValue = gameplayPrefab;
            so.FindProperty("_baseFlightDuration").floatValue = 16f;
            so.FindProperty("_minFlightDuration").floatValue = 10f;
            so.FindProperty("_arcHeight").floatValue = 28f;
            so.FindProperty("_baseScore").intValue = 250;
            so.FindProperty("_spawnCost").floatValue = 2.2f;
            so.FindProperty("_selectionWeight").floatValue = 0.35f;
            so.FindProperty("_minTokenLength").intValue = 2;
            so.FindProperty("_maxTokenLength").intValue = 4;
            so.FindProperty("_unlockAfterSeconds").floatValue = 0f;
            so.FindProperty("_weightGrowthPerMinute").floatValue = 0.4f;
            so.FindProperty("_visualScale").vector3Value = new Vector3(80f, 80f, 80f);
            so.FindProperty("_meshForwardEulerOffset").vector3Value = new Vector3(0f, 180f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return threat;
        }

        private static InterceptorDefinition LoadOrCreateInterceptorDefinition(GameObject gameplayPrefab)
        {
            const string path = ContentRoot + "/Threats/IronDomeInterceptor.asset";
            var interceptor = AssetDatabase.LoadAssetAtPath<InterceptorDefinition>(path);
            if (interceptor == null)
            {
                interceptor = ScriptableObject.CreateInstance<InterceptorDefinition>();
                AssetDatabase.CreateAsset(interceptor, path);
            }

            var so = new SerializedObject(interceptor);
            so.FindProperty("_id").stringValue = "iron_dome_interceptor";
            so.FindProperty("_prefab").objectReferenceValue = gameplayPrefab;
            so.FindProperty("_cruiseSpeed").floatValue = 110f;
            so.FindProperty("_initialSpeed").floatValue = 16f;
            so.FindProperty("_accelerationSeconds").floatValue = 0.9f;
            so.FindProperty("_hitRadius").floatValue = 3f;
            so.FindProperty("_maxLifetime").floatValue = 8f;
            so.FindProperty("_launchInterval").floatValue = 0.08f;
            so.FindProperty("_boostDurationSeconds").floatValue = 0.28f;
            so.FindProperty("_maxTurnRateDegrees").floatValue = 105f;
            so.FindProperty("_turnEngageSeconds").floatValue = 0.65f;
            so.FindProperty("_turnAccelerationDegrees").floatValue = 140f;
            so.FindProperty("_visualScale").vector3Value = new Vector3(1.25f, 1.25f, 1.25f);
            so.FindProperty("_meshForwardEulerOffset").vector3Value = new Vector3(-90f, 0f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return interceptor;
        }

        private static GameplayContentCatalog LoadOrCreateCatalog(
            TrainingLanguageProfile english,
            TrainingLanguageProfile hebrew,
            GameModeDefinition mode,
            ThreatDefinition threat,
            ThreatDefinition specialThreat,
            InterceptorDefinition interceptor)
        {
            const string path = ContentRoot + "/Definitions/GameplayContentCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<GameplayContentCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<GameplayContentCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            var so = new SerializedObject(catalog);
            so.FindProperty("_english").objectReferenceValue = english;
            so.FindProperty("_hebrew").objectReferenceValue = hebrew;
            so.FindProperty("_defaultMode").objectReferenceValue = mode;
            so.FindProperty("_basicThreat").objectReferenceValue = threat;
            so.FindProperty("_specialThreat").objectReferenceValue = specialThreat;
            so.FindProperty("_basicInterceptor").objectReferenceValue = interceptor;
            so.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
