using System;

namespace Internal
{
    // Global / GamePlay / Menu держат runtime-ассеты в подпапке Assets/
    // (Scenes, Prefabs, Generated, Options). Остальные сборки пишут Generated рядом с asmdef.
    public static class ModuleAssetLayout
    {
        public static readonly string[] Roots =
        {
            "Assets/Common/Global",
            "Assets/GamePlay",
            "Assets/Menu"
        };

        public static bool IsModuleRoot(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                return false;

            foreach (var root in Roots)
            {
                if (string.Equals(directory, root, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static bool TryGetRoot(string assetPath, out string root)
        {
            root = null;

            if (string.IsNullOrEmpty(assetPath))
                return false;

            var normalized = assetPath.Replace('\\', '/');

            foreach (var candidate in Roots)
            {
                if (normalized.Equals(candidate, StringComparison.Ordinal) ||
                    normalized.StartsWith(candidate + "/", StringComparison.Ordinal))
                {
                    root = candidate;
                    return true;
                }
            }

            return false;
        }

        public static string GetGeneratedFolder(string moduleRoot)
        {
            return $"{moduleRoot}/Assets/Generated";
        }

        public static string GetOptionsFolder(string moduleRoot)
        {
            return $"{moduleRoot}/Assets/Options";
        }
    }
}