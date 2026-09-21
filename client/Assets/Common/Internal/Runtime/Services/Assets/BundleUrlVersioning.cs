using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Internal
{
    // Хеш в имени бандла (AppendHash) считается от входных ассетов, а не от байт: после апгрейда Unity
    // бандл пересобирается под тем же именем. WebGL-загрузчик отдаёт .bundle из UnityCache как immutable
    // (см. index.html), поэтому URL обязан меняться вместе с содержимым — дописываем к нему CRC бандла.
    public static class BundleUrlVersioning
    {
        public static void Setup()
        {
            Addressables.InternalIdTransformFunc = Transform;
        }

        private static string Transform(IResourceLocation location)
        {
            var id = location.InternalId;

            if (location.Data is not AssetBundleRequestOptions options || options.Crc == 0)
                return id;

            // Локальные пути грузятся через LoadFromFile, им query ломает путь.
            if (id.StartsWith("http") == false)
                return id;

            return $"{id}?crc={options.Crc:x8}";
        }
    }
}
