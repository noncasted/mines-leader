using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
            var guidToType = BuildGuidMap(assetsRoot);
            ScanFiles(results, assetsRoot, projectRoot, guidToType, "*.prefab", true);
            ScanFiles(results, assetsRoot, projectRoot, guidToType, "*.unity", false);
            results.Sort((a, b) => string.CompareOrdinal(a.AssetPath, b.AssetPath));
            return results;
        }

        private static void ScanFiles(
            List<ContainerGraphAsset> results,
            string assetsRoot,
            string projectRoot,
            Dictionary<string, string> guidToType,
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

                var asset = Parse(file, projectRoot, guidToType, prefabs);
                if (asset.HasValue)
                    results.Add(asset.Value);
            }
        }

        private static ContainerGraphAsset? Parse(
            string fullPath,
            string projectRoot,
            Dictionary<string, string> guidToType,
            bool prefab) {
            string[] lines;
            try {
                lines = File.ReadAllLines(fullPath);
            }
            catch (Exception exception) {
                Debug.LogError("[ContainerGraph] Failed to read " + fullPath + ": " + exception);
                return null;
            }

            var blocks = ParseBlocks(lines);
            var byId = new Dictionary<string, YamlBlock>(StringComparer.Ordinal);
            foreach (var block in blocks) {
                if (string.IsNullOrEmpty(block.FileId) == false)
                    byId[block.FileId] = block;
            }

            var path = CatalogPaths.ToAssetPath(projectRoot, fullPath);
            ContainerGraphAsset? found = null;

            foreach (var block in blocks) {
                if (prefab) {
                    if (block.AutoDetected.Count == 0 && block.Register.Count == 0)
                        continue;
                    if (IsEntityHolder(block) == false)
                        continue;

                    var types = Resolve(byId, block.AutoDetected, guidToType);
                    types.AddRange(Resolve(byId, block.Register, guidToType));
                    found = Merge(found, path, TypeName(block, guidToType), types);
                    continue;
                }

                if (IsSceneFactory(block) == false)
                    continue;

                found = Merge(found, path, TypeName(block, guidToType), Resolve(byId, block.Services, guidToType));
            }

            return found;
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

        private static string TypeName(YamlBlock block, Dictionary<string, string> guidToType) {
            var identifier = block.ClassIdentifier;
            var separator = identifier.IndexOf("::", StringComparison.Ordinal);
            if (separator >= 0 && separator + 2 < identifier.Length)
                return identifier.Substring(separator + 2);

            if (string.IsNullOrEmpty(identifier) == false)
                return identifier;

            if (string.IsNullOrEmpty(block.ScriptGuid) == false &&
                guidToType.TryGetValue(block.ScriptGuid, out var mapped))
                return mapped;

            return "";
        }

        private static List<string> Resolve(
            Dictionary<string, YamlBlock> byId,
            List<string> fileIds,
            Dictionary<string, string> guidToType) {
            var types = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in fileIds) {
                if (byId.TryGetValue(id, out var block) == false)
                    continue;

                var type = TypeName(block, guidToType);
                if (string.IsNullOrEmpty(type) || seen.Add(type) == false)
                    continue;

                types.Add(type);
            }

            types.Sort(StringComparer.Ordinal);
            return types;
        }

        private static ContainerGraphAsset Merge(ContainerGraphAsset? existing, string path, string holder, List<string> types) {
            if (existing == null)
                return new ContainerGraphAsset(path, holder, types.ToArray());

            var combined = new List<string>(existing.Value.ComponentTypes);
            var seen = new HashSet<string>(combined, StringComparer.Ordinal);
            foreach (var type in types) {
                if (seen.Add(type))
                    combined.Add(type);
            }

            combined.Sort(StringComparer.Ordinal);
            var holderType = string.IsNullOrEmpty(existing.Value.HolderType) ? holder : existing.Value.HolderType;
            return new ContainerGraphAsset(path, holderType, combined.ToArray());
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

        private static Dictionary<string, string> BuildGuidMap(string assetsRoot) {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] metas;
            try {
                metas = Directory.GetFiles(assetsRoot, "*.cs.meta", SearchOption.AllDirectories);
            }
            catch (Exception exception) {
                Debug.LogError("[ContainerGraph] Failed to index script GUIDs: " + exception);
                return map;
            }

            var guidLine = new Regex(@"^guid: ([a-f0-9]{32})", RegexOptions.Compiled);
            var namespaceLine = new Regex(@"namespace\s+([A-Za-z0-9_.]+)", RegexOptions.Compiled);
            var classLine = new Regex(@"\bclass\s+([A-Za-z0-9_]+)", RegexOptions.Compiled);

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

                var script = meta.Substring(0, meta.Length - ".meta".Length);
                if (File.Exists(script) == false)
                    continue;

                try {
                    string ns = "";
                    string type = "";
                    foreach (var line in File.ReadLines(script)) {
                        if (string.IsNullOrEmpty(ns)) {
                            var nsMatch = namespaceLine.Match(line);
                            if (nsMatch.Success)
                                ns = nsMatch.Groups[1].Value;
                        }

                        if (string.IsNullOrEmpty(type)) {
                            var classMatch = classLine.Match(line);
                            if (classMatch.Success)
                                type = classMatch.Groups[1].Value;
                        }

                        if (string.IsNullOrEmpty(ns) == false && string.IsNullOrEmpty(type) == false)
                            break;
                    }

                    if (string.IsNullOrEmpty(type))
                        continue;

                    map[guid] = string.IsNullOrEmpty(ns) ? type : ns + "." + type;
                }
                catch {
                }
            }

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
            public List<string> AutoDetected = new List<string>();
            public List<string> Register = new List<string>();
            public List<string> Services = new List<string>();
        }
    }
}
