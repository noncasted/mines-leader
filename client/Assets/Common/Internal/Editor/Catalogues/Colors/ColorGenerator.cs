using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    public static class ColorGenerator
    {
        public const string GeneratedFolder = "Assets/Common/Internal/Runtime/Catalogues/Colors/Generated";

        private const string GeneratedFileName = "Colors.cs";
        private const string LogTag = "ColorGenerator";

        [MenuItem("Tools/GenerateColors")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling)
                return;

            if (EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Generate;
                return;
            }

            var guids = AssetDatabase.FindAssets("t:ColorCatalog");

            if (guids.Length == 0)
                throw new InvalidOperationException("[ColorGenerator] ColorCatalog asset is missing");

            if (guids.Length != 1)
                throw new InvalidOperationException(
                    $"[ColorGenerator] Expected exactly one ColorCatalog, found {guids.Length}");

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var catalog = AssetDatabase.LoadAssetAtPath<ColorCatalog>(path);

            if (catalog == null)
                throw new InvalidOperationException($"[ColorGenerator] Failed to load ColorCatalog at {path}");

            Generate(catalog);
        }

        public static void Generate(ColorCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            var groups = BuildGroups(catalog);
            CatalogPaths.EnsureFolder(GeneratedFolder);
            GeneratedFile.WriteIfChanged(LogTag, $"{GeneratedFolder}/{GeneratedFileName}", BuildColorsClass(groups));
            Debug.Log($"[ColorGenerator] Generated {groups.Count} color group(s).");
        }

        private static List<ColorGroupDefinition> BuildGroups(ColorCatalog catalog)
        {
            var groups = new List<ColorGroupDefinition>();
            var usedGroupNames = new HashSet<string>(StringComparer.Ordinal);
            var sourceGroups = catalog.Groups;

            if (sourceGroups == null)
                return groups;

            for (var i = 0; i < sourceGroups.Count; i++)
            {
                var source = sourceGroups[i];

                if (source == null)
                {
                    Debug.LogError($"[ColorGenerator] Group at index {i} is null");
                    continue;
                }

                var groupName = ToIdentifier(source.Name);

                if (string.IsNullOrEmpty(groupName))
                {
                    Debug.LogError($"[ColorGenerator] Group name is empty at index {i}");
                    continue;
                }

                if (usedGroupNames.Add(groupName) == false)
                {
                    Debug.LogError(
                            $"[ColorGenerator] Duplicate group identifier '{groupName}'. Skipping group at index {i}."
                        );
                    continue;
                }

                var group = new ColorGroupDefinition
                {
                    Name = groupName,
                    ClassName = groupName + "Colors"
                };

                if (source.Entries != null)
                {
                    var usedEntryNames = new HashSet<string>(StringComparer.Ordinal);

                    for (var entryIndex = 0; entryIndex < source.Entries.Count; entryIndex++)
                    {
                        var entry = source.Entries[entryIndex];

                        if (entry == null)
                        {
                            Debug.LogError(
                                    $"[ColorGenerator] Entry at index {entryIndex} in group '{groupName}' is null"
                                );
                            continue;
                        }

                        var entryName = ToIdentifier(entry.Name);

                        if (string.IsNullOrEmpty(entryName))
                        {
                            Debug.LogError(
                                    $"[ColorGenerator] Color name is empty at index {entryIndex} in group '{groupName}'"
                                );
                            continue;
                        }

                        if (usedEntryNames.Add(entryName) == false)
                        {
                            Debug.LogError(
                                    $"[ColorGenerator] Duplicate color identifier '{entryName}' in group '{groupName}'. Skipping."
                                );
                            continue;
                        }

                        group.Entries.Add(new ColorEntryDefinition
                        {
                            Name = entryName,
                            Value = entry.Value
                        });
                    }
                }

                if (group.Entries.Count == 0)
                    continue;

                group.Entries.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
                groups.Add(group);
            }

            groups.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            return groups;
        }

        private static string BuildColorsClass(IReadOnlyList<ColorGroupDefinition> groups)
        {
            var builder = new StringBuilder();
            builder.AppendLine("// Auto-generated by ColorGenerator. Do not edit manually.");
            builder.AppendLine("using Unity.Scripting.LifecycleManagement;");

            if (groups.Count > 0)
            {
                builder.AppendLine("using UnityEngine;");
            }

            builder.AppendLine();

            builder.AppendLine("namespace Internal {");
            builder.AppendLine("    [NoAutoStaticsCleanup]");
            builder.AppendLine("    public static class Colors {");

            foreach (var group in groups)
                builder.AppendLine($"        public static readonly {group.ClassName} {group.Name} = new();");

            builder.AppendLine("    }");

            foreach (var group in groups)
            {
                builder.AppendLine();
                builder.AppendLine($"    public sealed class {group.ClassName} {{");

                foreach (var entry in group.Entries)
                    builder.AppendLine($"        public readonly Color {entry.Name} = {FormatColor(entry.Value)};");

                builder.AppendLine("    }");
            }

            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string FormatColor(Color color)
        {
            return
                $"new Color({FormatFloat(color.r)}, {FormatFloat(color.g)}, {FormatFloat(color.b)}, {FormatFloat(color.a)})";
        }

        private static string FormatFloat(float value)
        {
            if (value == 0f)
                return "0f";

            if (value == 1f)
                return "1f";

            return value.ToString("R", CultureInfo.InvariantCulture) + "f";
        }

        private static string ToIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var builder = new StringBuilder(name.Length);
            var startWord = true;

            foreach (var character in name)
            {
                if (char.IsLetterOrDigit(character) == false)
                {
                    startWord = true;
                    continue;
                }

                if (startWord)
                {
                    builder.Append(char.ToUpperInvariant(character));
                    startWord = false;
                    continue;
                }

                builder.Append(character);
            }

            if (builder.Length == 0)
                return string.Empty;

            if (char.IsDigit(builder[0]))
                return "C" + builder;

            return builder.ToString();
        }

        private sealed class ColorGroupDefinition
        {
            public string Name;
            public string ClassName;
            public List<ColorEntryDefinition> Entries = new();
        }

        private sealed class ColorEntryDefinition
        {
            public string Name;
            public Color Value;
        }
    }
}