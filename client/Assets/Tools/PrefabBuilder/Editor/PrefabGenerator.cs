using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

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
            var failedTypes = new List<Type>();

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
                catch (Exception)
                {
                    failedTypes.Add(type);
                }
            }

            // Retry failed prefabs — they may depend on prefabs generated in the first pass
            foreach (var type in failedTypes)
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
                CleanupStalePrefabs(generatedPrefabs);
                PrefabsClassGenerator.Generate(generatedPrefabs);
                AssetDatabase.Refresh();
                Debug.Log($"[PrefabGenerator] Generated {generatedPrefabs.Count} prefab(s).");
            }
        }

        private static void CleanupStalePrefabs(
            List<(string asmdefName, string prefabName, string prefabPath)> generatedPrefabs)
        {
            var generatedNames = new HashSet<string>();
            foreach (var (_, prefabName, _) in generatedPrefabs)
            {
                generatedNames.Add(prefabName + ".prefab");
            }

            var existingFiles = Directory.GetFiles(OutputFolder, "*.prefab");
            foreach (var filePath in existingFiles)
            {
                var fileName = Path.GetFileName(filePath);
                if (generatedNames.Contains(fileName)) continue;

                AssetDatabase.DeleteAsset(OutputFolder + "/" + fileName);
                Debug.Log($"[PrefabGenerator] Deleted stale prefab: {fileName}");
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

            try
            {
                defineMethod.Invoke(null, new object[] { builder });

                var prefabPath = $"{OutputFolder}/{builder.PrefabName}.prefab";
                CheckManualModification(prefabPath);

                builder.Build(prefabPath);

                return (string.Empty, builder.PrefabName, builder.PrefabName);
            }
            catch
            {
                // Clean up leaked GameObject if Build() was never called
                if (builder.GameObject != null)
                    Object.DestroyImmediate(builder.GameObject);
                throw;
            }
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