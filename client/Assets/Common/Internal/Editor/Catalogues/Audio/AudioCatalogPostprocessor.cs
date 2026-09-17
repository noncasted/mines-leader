using UnityEditor;

namespace Internal
{
    public sealed class AudioCatalogPostprocessor : AssetPostprocessor
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
                AudioCatalogGenerator.ScheduleGenerate();
        }

        private static bool ContainsSource(string[] paths)
        {
            if (paths == null)
                return false;

            for (var i = 0; i < paths.Length; i++)
            {
                if (AudioCatalogGenerator.IsSourcePath(paths[i]))
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

                if (AudioCatalogGenerator.IsSourcePath(path) == false)
                    continue;

                var importer = AssetImporter.GetAtPath(path);

                if (importer == null)
                    continue;

                if (AudioCatalogMetadata.TryRead(importer, out var metadata) == false)
                    continue;

                if (metadata.Included)
                    return true;
            }

            return false;
        }
    }
}
