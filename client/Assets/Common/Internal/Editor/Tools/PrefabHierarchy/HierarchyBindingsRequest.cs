using System;
using UnityEditor;
using UnityEngine;

namespace Internal {
    // Заявка на связывание переживает перезагрузку домена: сначала пишем код, после компиляции
    // возвращаемся к тому же объекту и заполняем ссылки.
    [Serializable]
    internal sealed class HierarchyBindingsRequest {
        private const string SessionKey = "Internal.HierarchyBindings.PendingRequest";

        public string AssetPath;
        public string ObjectPath;
        public string TypeName;
        public string Namespace;
        public bool IsPrefabAsset;
        public bool IsSceneService;
        public bool IsEntityComponent;

        public static void Save(HierarchyBindingsRequest request) {
            SessionState.SetString(SessionKey, JsonUtility.ToJson(request));
        }

        public static HierarchyBindingsRequest Load() {
            var json = SessionState.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return null;

            try {
                return JsonUtility.FromJson<HierarchyBindingsRequest>(json);
            }
            catch (Exception) {
                Clear();
                return null;
            }
        }

        public static void Clear() {
            SessionState.EraseString(SessionKey);
        }
    }
}
