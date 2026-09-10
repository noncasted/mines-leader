using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Заявка на связывание переживает перезагрузку домена: сначала пишем код, после компиляции
    // возвращаемся к тому же объекту и заполняем ссылки.
    [Serializable]
    internal sealed class HierarchyBindingsRequest
    {
        public string AssetPath;
        public string ObjectPath;
        public string TypeName;
        public string Namespace;
        public bool IsPrefabAsset;
        public bool IsSceneService;
        public bool IsEntityComponent;
    }

    // Заявок может быть много: перегенерация сцены пишет все классы разом, а компиляция после
    // этого одна на всех, поэтому очередь разбирается целиком в один проход.
    [Serializable]
    internal sealed class HierarchyBindingsQueue
    {
        private const string SessionKey = "Internal.HierarchyBindings.PendingRequests";

        public List<HierarchyBindingsRequest> Requests = new();

        public static void Enqueue(HierarchyBindingsRequest request)
        {
            var queue = Load();
            queue.Requests.Add(request);
            SessionState.SetString(SessionKey, JsonUtility.ToJson(queue));
        }

        public static List<HierarchyBindingsRequest> Take()
        {
            var requests = Load().Requests;
            Clear();
            return requests;
        }

        public static bool IsEmpty()
        {
            return Load().Requests.Count == 0;
        }

        public static void Clear()
        {
            SessionState.EraseString(SessionKey);
        }

        private static HierarchyBindingsQueue Load()
        {
            var json = SessionState.GetString(SessionKey, string.Empty);

            if (string.IsNullOrEmpty(json))
                return new HierarchyBindingsQueue();

            try
            {
                return JsonUtility.FromJson<HierarchyBindingsQueue>(json) ?? new HierarchyBindingsQueue();
            }
            catch (Exception)
            {
                Clear();
                return new HierarchyBindingsQueue();
            }
        }
    }
}