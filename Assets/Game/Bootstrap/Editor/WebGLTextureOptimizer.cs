using UnityEditor;
using UnityEngine;

namespace TypingDefense.Bootstrap.Editor
{
    /// <summary>
    /// Shrinks the Web build by adding WebGL-only texture import overrides.
    /// Source files are never modified, and the overrides can be reverted.
    /// </summary>
    public static class WebGLTextureOptimizer
    {
        private const string WebGlPlatform = "WebGL";
        private static readonly string[] SearchFolders = { "Assets" };

        [MenuItem("IronWord/WebGL/Optimize Textures (1024, Crunch)")]
        public static void OptimizeBalanced()
        {
            Apply(1024, crunch: true);
        }

        [MenuItem("IronWord/WebGL/Optimize Textures (512, Crunch)")]
        public static void OptimizeAggressive()
        {
            Apply(512, crunch: true);
        }

        [MenuItem("IronWord/WebGL/Revert Texture Overrides")]
        public static void Revert()
        {
            Apply(0, crunch: false, revert: true);
        }

        private static void Apply(int maxSize, bool crunch, bool revert = false)
        {
            var guids = AssetDatabase.FindAssets("t:Texture", SearchFolders);
            var changed = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (var i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Optimizing textures for WebGL",
                            path,
                            (float)i / Mathf.Max(1, guids.Length)))
                    {
                        break;
                    }

                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                        continue;

                    var settings = importer.GetPlatformTextureSettings(WebGlPlatform);

                    if (revert)
                    {
                        if (!settings.overridden)
                            continue;

                        settings.overridden = false;
                    }
                    else
                    {
                        settings.overridden = true;
                        settings.maxTextureSize = maxSize;
                        settings.format = TextureImporterFormat.Automatic;
                        settings.textureCompression = TextureImporterCompression.Compressed;
                        settings.crunchedCompression = crunch;
                        settings.compressionQuality = 50;
                    }

                    importer.SetPlatformTextureSettings(settings);
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            Debug.Log(revert
                ? $"[WebGL] Reverted texture overrides on {changed} textures."
                : $"[WebGL] Applied max size {maxSize} (crunch: {crunch}) to {changed} textures.");
        }
    }
}
