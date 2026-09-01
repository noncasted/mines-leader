using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Internal {
    // Метаданные каталогов живут в importer.userData под своим корневым ключом,
    // чтобы каталоги не затирали ни чужие ключи, ни данные других инструментов.
    public static class AssetUserData {
        public static bool TryRead(AssetImporter importer, string key, string logTag, out JObject payload) {
            payload = null;

            if (importer == null)
                throw new ArgumentNullException(nameof(importer));

            var userData = importer.userData;
            if (string.IsNullOrWhiteSpace(userData))
                return false;

            try {
                var root = JObject.Parse(userData);
                if (root[key] is not JObject catalog)
                    return false;

                payload = catalog;
                return true;
            }
            catch (Exception exception) {
                Debug.LogError($"[{logTag}] Failed to read userData at {importer.assetPath}: {exception}");
                return false;
            }
        }

        // Возвращает true, если userData действительно изменился.
        public static bool Write(AssetImporter importer, string key, string logTag, JObject payload) {
            if (importer == null)
                throw new ArgumentNullException(nameof(importer));
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try {
                var nextUserData = Merge(importer.userData, key, payload);
                if (importer.userData == nextUserData)
                    return false;

                importer.userData = nextUserData;
                EditorUtility.SetDirty(importer);
                AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);
                return true;
            }
            catch (Exception exception) {
                Debug.LogError($"[{logTag}] Failed to write userData at {importer.assetPath}: {exception}");
                return false;
            }
        }

        private static string Merge(string userData, string key, JObject payload) {
            JObject root;
            if (string.IsNullOrWhiteSpace(userData)) {
                root = new JObject();
            }
            else {
                var parsed = JToken.Parse(userData);
                if (parsed is not JObject parsedObject)
                    throw new InvalidOperationException("userData is not a JSON object");

                root = parsedObject;
            }

            root[key] = payload;
            return root.ToString(Formatting.Indented);
        }
    }
}
