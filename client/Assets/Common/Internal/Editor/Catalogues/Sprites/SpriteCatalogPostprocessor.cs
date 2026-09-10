using UnityEditor;

namespace Internal
{
    public sealed class SpriteCatalogPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsSource(deletedAssets) ||
                ContainsSource(movedAssets) ||
                ContainsSource(movedFromAssetPaths) ||
                ContainsIncludedSource(importedAssets))
                SpriteGenerator.ScheduleGenerate();
        }

        private static bool ContainsSource(string[] paths)
        {
            if (paths == null)
                return false;

            for (var i = 0; i < paths.Length; i++)
            {
                if (SpriteGenerator.IsSourcePath(paths[i]))
                    return true;
            }

            return false;
        }

        private static bool ContainsIncludedSource(string[] paths)
        {
            if (paths == null)
                return false;

            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];

                if (SpriteGenerator.IsSourcePath(path) == false)
                    continue;

                var importer = AssetImporter.GetAtPath(path);

                if (importer == null)
                    continue;

                if (SpriteCatalogMetadata.TryRead(importer, out var metadata) == false)
                    continue;

                if (metadata.Included)
                    return true;
            }

            return false;
        }
    }
}