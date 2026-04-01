using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Tools;
using UnityEditor;
using UnityEngine;

namespace Tools
{
    public static class PrefabGenerator
    {
        private const string OutputFolder = "Assets/Resources/Generated";

        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            Generate();
        }

        [MenuItem("Tools/GeneratePrefabs")]
        public static void Generate()
        {
            EnsureFolder(OutputFolder);

            var definitionTypes = TypeCache.GetTypesWithAttribute<PrefabDefinitionAttribute>();
            if (definitionTypes.Count == 0) return;

            var generatedPrefabs = new List<(string asmdefName, string prefabName, string prefabPath)>();

            foreach (var type in definitionTypes)
            {
                try
                {
                    var result = GeneratePrefab(type);
                    if (result.HasValue)
                    {
                        generatedPrefabs.Add(result.Value);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PrefabGenerator] Failed to generate prefab from {type.Name}: {ex}");
                }
            }

            if (generatedPrefabs.Count > 0)
            {
                PrefabsClassGenerator.Generate(generatedPrefabs);
                AssetDatabase.Refresh();
                Debug.Log($"[PrefabGenerator] Generated {generatedPrefabs.Count} prefab(s).");
            }
        }

        private static (string asmdefName, string prefabName, string prefabPath)? GeneratePrefab(Type type)
        {
            var defineMethod = type.GetMethod("Define", BindingFlags.Public | BindingFlags.Static);
            if (defineMethod == null)
            {
                Debug.LogError(
                    $"[PrefabGenerator] {type.Name} has [PrefabDefinition] but no public static Define(PrefabBuilder) method."
                );
                return null;
            }

            var parameters = defineMethod.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(PrefabBuilder))
            {
                Debug.LogError(
                    $"[PrefabGenerator] {type.Name}.Define() must accept exactly one PrefabBuilder parameter."
                );
                return null;
            }

            var builder = new PrefabBuilder();
            defineMethod.Invoke(null, new object[] { builder });

            var prefabPath = $"{OutputFolder}/{builder.PrefabName}.prefab";
            CheckManualModification(prefabPath);

            builder.Build(prefabPath);

            return (string.Empty, builder.PrefabName, $"Generated/{builder.PrefabName}");
        }

        private static void CheckManualModification(string prefabPath)
        {
            if (!File.Exists(prefabPath)) return;

            var metaPath = prefabPath + ".meta";
            if (!File.Exists(metaPath)) return;

            var prefabTime = File.GetLastWriteTimeUtc(prefabPath);
            var metaTime = File.GetLastWriteTimeUtc(metaPath);

            if (prefabTime > metaTime.AddSeconds(5))
            {
                Debug.LogWarning(
                    $"[PrefabGenerator] '{prefabPath}' appears to have been modified manually. It will be overwritten."
                );
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}