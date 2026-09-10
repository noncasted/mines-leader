using System;
using System.IO;
using UnityEditor;

namespace Internal {
    // Ссылки между ассетами меняются в сценах, префабах и материалах. Ассеты каталогов
    // сюда не входят: их генераторы сами планируют пересчёт после синхронизации.
    public sealed class SharedAddressablesPostprocessor : AssetPostprocessor {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths) {
            if (ContainsReferencingAsset(importedAssets) ||
                ContainsReferencingAsset(deletedAssets) ||
                ContainsReferencingAsset(movedAssets))
                SharedAddressablesSync.ScheduleSync();
        }

        private static bool ContainsReferencingAsset(string[] paths) {
            if (paths == null)
                return false;

            for (var i = 0; i < paths.Length; i++) {
                var extension = Path.GetExtension(paths[i]);
                if (extension.Equals(".unity", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".mat", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
