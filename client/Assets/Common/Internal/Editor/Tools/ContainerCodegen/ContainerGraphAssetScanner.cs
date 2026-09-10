using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Internal {
    internal static class ContainerGraphAssetScanner {
        private const string SceneServicesFactoryGuid = "acd7e370b6b3474e8b51f677a9edd625";

        private static readonly Regex FileIdHeader = new Regex(@"^--- !u!114 &(-?\d+)", RegexOptions.Compiled);
        private static readonly Regex ScriptGuid = new Regex(@"guid: ([a-f0-9]{32})", RegexOptions.Compiled);
        private static readonly Regex FileIdRef = new Regex(@"fileID: (-?\d+)", RegexOptions.Compiled);

        public static List<ContainerGraphAsset> Scan(string assetsRoot) {
            var results = new List<ContainerGraphAsset>();
            if (string.IsNullOrEmpty(assetsRoot) || Directory.Exists(assetsRoot) == false)
                return results;

            var projectRoot = Path.GetDirectoryName(assetsRoot);
            var scriptTypes = new ScriptTypes();
            var guidToPrefab = BuildPrefabMap(assetsRoot);
            var prefabCache = new Dictionary<string, Dictionary<string, YamlBlock>>(StringComparer.OrdinalIgnoreCase);
            ScanFiles(results, assetsRoot, projectRoot, scriptTypes, guidToPrefab, prefabCache, "*.prefab", true);
            ScanFiles(results, assetsRoot, projectRoot, scriptTypes, guidToPrefab, prefabCache, "*.unity", false);
            results.Sort((a, b) => {
                var path = string.CompareOrdinal(a.AssetPath, b.AssetPath);
                return path != 0 ? path : string.CompareOrdinal(a.HolderType, b.HolderType);
            });
            return results;
        }

        private static void ScanFiles(
            List<ContainerGraphAsset> results,
            string assetsRoot,
            string projectRoot,
            ScriptTypes scriptTypes,
            Dictionary<string, string> guidToPrefab,
            Dictionary<string, Dictionary<string, YamlBlock>> prefabCache,
            string pattern,
            bool prefabs) {
            string[] files;
            try {
                files = Directory.GetFiles(assetsRoot, pattern, SearchOption.AllDirectories);
            }
            catch (Exception exception) {
                Debug.LogError("[ContainerGraph] Failed to scan " + pattern + ": " + exception);
                return;
            }

            foreach (var file in files) {
                if (file.IndexOf($"{Path.DirectorySeparatorChar}Plugins{Path.DirectorySeparatorChar}", StringComparison.Ordinal) >= 0)
                    continue;

                ParseFile(results, file, projectRoot, scriptTypes, guidToPrefab, prefabCache, prefabs);
            }
        }

        private static void ParseFile(
            List<ContainerGraphAsset> results,
            string fullPath,
            string projectRoot,
            ScriptTypes scriptTypes,
            Dictionary<string, string> guidToPrefab,
            Dictionary<string, Dictionary<string, YamlBlock>> prefabCache,
            bool prefab) {
            string[] lines;
            try {
                lines = File.ReadAllLines(fullPath);
            }
            catch (Exception exception) {
                Debug.LogError("[ContainerGraph] Failed to read " + fullPath + ": " + exception);
                return;
            }

            var blocks = ParseBlocks(lines);
            var byId = IndexBlocks(blocks);
            var path = CatalogPaths.ToAssetPath(projectRoot, fullPath);

            foreach (var block in blocks) {
                if (prefab) {
                    if (block.AutoDetected.Count == 0 && block.Register.Count == 0)
                        continue;
                    if (IsEntityHolder(block) == false)
                        continue;

                    var types = Resolve(byId, block.AutoDetected, scriptTypes, guidToPrefab, prefabCache, false);
                    AppendUnique(types, Resolve(byId, block.Register, scriptTypes, guidToPrefab, prefabCache, false));
                    AddAsset(results, path, TypeName(block, scriptTypes), types);
                    continue;
                }

                if (IsSceneFactory(block)) {
                    AddAsset(
                        results,
                        path,
                        TypeName(block, scriptTypes),
                        Resolve(byId, block.Services, scriptTypes, guidToPrefab, prefabCache, true));
                    continue;
                }

                if (IsEntityHolder(block) == false)
                    continue;

                var entityTypes = Resolve(byId, block.AutoDetected, scriptTypes, guidToPrefab, prefabCache, false);
                AppendUnique(entityTypes, Resolve(byId, block.Register, scriptTypes, guidToPrefab, prefabCache, false));
                AddAsset(results, path, TypeName(block, scriptTypes), entityTypes);
            }
        }

        private static bool IsEntityHolder(YamlBlock block) {
            if (block.AutoDetected.Count > 0 || block.Register.Count > 0)
                return true;

            var identifier = block.ClassIdentifier;
            return identifier.IndexOf("ScopeEntityView", StringComparison.Ordinal) >= 0 ||
                   identifier.IndexOf("ScopeEntity", StringComparison.Ordinal) >= 0;
        }

        private static bool IsSceneFactory(YamlBlock block) {
            if (block.ClassIdentifier.IndexOf("SceneServicesFactory", StringComparison.Ordinal) >= 0)
                return true;
            return string.Equals(block.ScriptGuid, SceneServicesFactoryGuid, StringComparison.OrdinalIgnoreCase);
        }

        private static string TypeName(YamlBlock block, ScriptTypes scriptTypes) {
            if (string.IsNullOrEmpty(block.ScriptGuid) == false &&
                scriptTypes.TryGet(block.ScriptGuid, out var mapped))
                return mapped;

            var identifier = block.ClassIdentifier;
            var separator = identifier.IndexOf("::", StringComparison.Ordinal);
            if (separator >= 0 && separator + 2 < identifier.Length)
                return identifier.Substring(separator + 2);

            return identifier ?? "";
        }

        private static List<string> Resolve(
            Dictionary<string, YamlBlock> byId,
            List<string> fileIds,
            ScriptTypes scriptTypes,
            Dictionary<string, string> guidToPrefab,
            Dictionary<string, Dictionary<string, YamlBlock>> prefabCache,
            bool sort) {
            var types = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in fileIds) {
                if (byId.TryGetValue(id, out var block) == false)
                    continue;

                var type = ResolveBlockType(block, scriptTypes, guidToPrefab, prefabCache);
                if (string.IsNullOrEmpty(type) || seen.Add(type) == false)
                    continue;

                types.Add(type);
            }

            if (sort)
                types.Sort(StringComparer.Ordinal);
            return types;
        }

        private static string ResolveBlockType(
            YamlBlock block,
            ScriptTypes scriptTypes,
            Dictionary<string, string> guidToPrefab,
            Dictionary<string, Dictionary<string, YamlBlock>> prefabCache) {
            var type = TypeName(block, scriptTypes);
            if (string.IsNullOrEmpty(type) == false)
                return type;
            if (string.IsNullOrEmpty(block.CorrespondingGuid) || string.IsNullOrEmpty(block.CorrespondingFileId))
                return "";
            if (guidToPrefab.TryGetValue(block.CorrespondingGuid, out var prefabPath) == false)
                return "";

            var prefabBlocks = PrefabBlocks(prefabPath, prefabCache);
            if (prefabBlocks.TryGetValue(block.CorrespondingFileId, out var source) == false)
                return "";
            return TypeName(source, scriptTypes);
        }

        private static void AddAsset(List<ContainerGraphAsset> results, string path, string holder, List<string> types) {
            if (string.IsNullOrEmpty(holder) && types.Count == 0)
                return;

            for (var i = 0; i < results.Count; i++) {
                var existing = results[i];
                if (existing.AssetPath != path || existing.HolderType != holder)
                    continue;

                var combined = new List<string>(existing.ComponentTypes);
                var seen = new HashSet<string>(combined, StringComparer.Ordinal);
                for (var t = 0; t < types.Count; t++) {
                    if (seen.Add(types[t]))
                        combined.Add(types[t]);
                }

                results[i] = new ContainerGraphAsset(path, holder, combined.ToArray());
                return;
            }

            results.Add(new ContainerGraphAsset(path, holder, types.ToArray()));
        }

        private static void AppendUnique(List<string> target, List<string> extra) {
            var seen = new HashSet<string>(target, StringComparer.Ordinal);
            for (var i = 0; i < extra.Count; i++) {
                if (seen.Add(extra[i]))
                    target.Add(extra[i]);
            }
        }

        private static List<YamlBlock> ParseBlocks(string[] lines) {
            var blocks = new List<YamlBlock>();
            YamlBlock current = null;
            string listField = null;

            foreach (var raw in lines) {
                var line = raw.TrimEnd('\r');
                var header = FileIdHeader.Match(line);
                if (header.Success) {
                    if (current != null)
                        blocks.Add(current);

                    current = new YamlBlock { FileId = header.Groups[1].Value };
                    listField = null;
                    continue;
                }

                if (current == null)
                    continue;

                var trimmed = line.Trim();
                if (trimmed.StartsWith("m_EditorClassIdentifier:", StringComparison.Ordinal)) {
                    current.ClassIdentifier = trimmed.Substring("m_EditorClassIdentifier:".Length).Trim();
                    listField = null;
                    continue;
                }

                var guidMatch = ScriptGuid.Match(trimmed);
                if (trimmed.StartsWith("m_Script:", StringComparison.Ordinal) && guidMatch.Success)
                    current.ScriptGuid = guidMatch.Groups[1].Value;

                if (trimmed.StartsWith("m_CorrespondingSourceObject:", StringComparison.Ordinal)) {
                    var correspondingGuid = ScriptGuid.Match(trimmed);
                    if (correspondingGuid.Success)
                        current.CorrespondingGuid = correspondingGuid.Groups[1].Value;
                    var correspondingId = FileIdRef.Match(trimmed);
                    if (correspondingId.Success)
                        current.CorrespondingFileId = correspondingId.Groups[1].Value;
                }

                if (trimmed == "_autoDetected:" || trimmed.StartsWith("_autoDetected:", StringComparison.Ordinal)) {
                    listField = "auto";
                    ParseInlineList(current.AutoDetected, trimmed);
                    continue;
                }

                if (trimmed == "_register:" || trimmed.StartsWith("_register:", StringComparison.Ordinal)) {
                    listField = "register";
                    ParseInlineList(current.Register, trimmed);
                    continue;
                }

                if (trimmed == "_services:" || trimmed.StartsWith("_services:", StringComparison.Ordinal)) {
                    listField = "services";
                    ParseInlineList(current.Services, trimmed);
                    continue;
                }

                if (listField != null && trimmed.StartsWith("-", StringComparison.Ordinal)) {
                    var fileId = FileIdRef.Match(trimmed);
                    if (fileId.Success)
                        ListFor(current, listField).Add(fileId.Groups[1].Value);
                    continue;
                }

                if (trimmed.StartsWith("_", StringComparison.Ordinal) || trimmed.StartsWith("m_", StringComparison.Ordinal))
                    listField = null;
            }

            if (current != null)
                blocks.Add(current);

            return blocks;
        }

        private static void ParseInlineList(List<string> target, string trimmed) {
            var index = trimmed.IndexOf('[');
            if (index < 0)
                return;

            var end = trimmed.IndexOf(']', index + 1);
            if (end < 0)
                return;

            var inner = trimmed.Substring(index + 1, end - index - 1);
            if (string.IsNullOrWhiteSpace(inner))
                return;

            foreach (Match match in FileIdRef.Matches(inner))
                target.Add(match.Groups[1].Value);
        }

        private static Dictionary<string, string> BuildPrefabMap(string assetsRoot) {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] metas;
            try {
                metas = Directory.GetFiles(assetsRoot, "*.prefab.meta", SearchOption.AllDirectories);
            }
            catch (Exception exception) {
                Debug.LogError("[ContainerGraph] Failed to index prefab GUIDs: " + exception);
                return map;
            }

            var guidLine = new Regex(@"^guid: ([a-f0-9]{32})", RegexOptions.Compiled);
            foreach (var meta in metas) {
                string guid = null;
                try {
                    foreach (var line in File.ReadLines(meta)) {
                        var match = guidLine.Match(line.Trim());
                        if (match.Success == false)
                            continue;
                        guid = match.Groups[1].Value;
                        break;
                    }
                }
                catch {
                    continue;
                }

                if (string.IsNullOrEmpty(guid))
                    continue;

                var prefab = meta.Substring(0, meta.Length - ".meta".Length);
                if (File.Exists(prefab) == false)
                    continue;

                map[guid] = prefab;
            }

            return map;
        }

        private static Dictionary<string, YamlBlock> IndexBlocks(List<YamlBlock> blocks) {
            var byId = new Dictionary<string, YamlBlock>(StringComparer.Ordinal);
            foreach (var block in blocks) {
                if (string.IsNullOrEmpty(block.FileId) == false)
                    byId[block.FileId] = block;
            }

            return byId;
        }

        private static Dictionary<string, YamlBlock> PrefabBlocks(
            string prefabPath,
            Dictionary<string, Dictionary<string, YamlBlock>> cache) {
            if (cache.TryGetValue(prefabPath, out var existing))
                return existing;

            var map = new Dictionary<string, YamlBlock>(StringComparer.Ordinal);
            try {
                var blocks = ParseBlocks(File.ReadAllLines(prefabPath));
                map = IndexBlocks(blocks);
            }
            catch (Exception exception) {
                Debug.LogError("[ContainerGraph] Failed to read prefab " + prefabPath + ": " + exception);
            }

            cache[prefabPath] = map;
            return map;
        }

        private static List<string> ListFor(YamlBlock block, string field) {
            if (field == "auto")
                return block.AutoDetected;
            if (field == "register")
                return block.Register;
            return block.Services;
        }

        private sealed class YamlBlock {
            public string FileId = "";
            public string ClassIdentifier = "";
            public string ScriptGuid = "";
            public string CorrespondingGuid = "";
            public string CorrespondingFileId = "";
            public List<string> AutoDetected = new List<string>();
            public List<string> Register = new List<string>();
            public List<string> Services = new List<string>();
        }

        // GUID скрипта → полное имя класса. Unity привязывает скрипт к классу с именем файла, а не к
        // первому классу в нём, поэтому истина — MonoScript.GetClass(). Разбор текста — только для
        // скрипта, которого ещё нет в скомпилированных сборках.
        private sealed class ScriptTypes {
            private static readonly Regex NamespaceDeclaration = new Regex(@"\bnamespace\s+([A-Za-z0-9_.]+)", RegexOptions.Compiled);

            private readonly Dictionary<string, string> _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public bool TryGet(string guid, out string type) {
                if (_cache.TryGetValue(guid, out type) == false) {
                    type = Find(guid);
                    _cache[guid] = type;
                }

                return string.IsNullOrEmpty(type) == false;
            }

            private static string Find(string guid) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) == false)
                    return "";

                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                var compiled = script != null ? script.GetClass() : null;
                if (compiled != null)
                    return compiled.FullName;

                return FromSource(path);
            }

            private static string FromSource(string path) {
                string source;
                try {
                    source = File.ReadAllText(path);
                }
                catch {
                    return "";
                }

                var name = Path.GetFileNameWithoutExtension(path);
                var declaration = Regex.Match(source, @"\bclass\s+" + Regex.Escape(name) + @"\b");
                if (declaration.Success == false)
                    return "";

                var ns = "";
                foreach (Match match in NamespaceDeclaration.Matches(source)) {
                    if (match.Index > declaration.Index)
                        break;
                    ns = match.Groups[1].Value;
                }

                return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
            }
        }
    }
}
