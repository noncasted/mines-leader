using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Tools
{
    public static class SceneGenerator
    {
        private static readonly string[] SearchFolders =
        {
            "Assets/Common",
            "Assets/GamePlay",
            "Assets/Global",
            "Assets/Internal",
            "Assets/Flow",
            "Assets/Menu",
            "Assets/Meta",
            "Assets/Startup",
            "Assets/Tools"
        };

        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            Generate();
        }

        [MenuItem("Tools/GenerateScenes")]
        public static void Generate()
        {
            var folders = new List<string>();

            foreach (var folder in SearchFolders)
            {
                if (AssetDatabase.IsValidFolder(folder))
                    folders.Add(folder);
            }

            if (folders.Count == 0)
                return;

            var guids = AssetDatabase.FindAssets("t:SceneAsset", folders.ToArray());

            if (guids.Length == 0)
                return;

            var scenes = new List<(string sceneName, string sceneGuid)>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = Path.GetFileNameWithoutExtension(path);
                var sceneName = SanitizeSceneName(fileName);
                scenes.Add((sceneName, guid));
            }

            if (scenes.Count > 0)
            {
                ScenesClassGenerator.Generate(scenes);
                Debug.Log($"[SceneGenerator] Found {scenes.Count} scene(s).");
            }
        }

        private static string SanitizeSceneName(string fileName)
        {
            var sb = new StringBuilder(fileName.Length);
            var capitalizeNext = true;

            foreach (var c in fileName)
            {
                if (c == '_' || c == '-' || c == ' ')
                {
                    capitalizeNext = true;
                    continue;
                }

                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(capitalizeNext ? char.ToUpper(c) : c);
                    capitalizeNext = false;
                }
            }

            return sb.ToString();
        }
    }
}