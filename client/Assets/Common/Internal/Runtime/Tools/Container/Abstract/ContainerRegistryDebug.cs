using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public static class ContainerRegistryDebug
    {
        public static IReadOnlyList<IContainerDiagnostics> Roots => _roots;

        public static IViewableDelegate Changed => _changed;

        private static readonly List<IContainerDiagnostics> _roots = new();
        private static readonly ViewableDelegate _changed = new();
        private static readonly Dictionary<IReadOnlyLifetime, List<LoadedAssetInfo>> _pendingAssets = new();

        internal static void AddRoot(IContainerDiagnostics root)
        {
#if UNITY_EDITOR || DEBUG
            if (root == null)
                return;

            if (_roots.Contains(root))
                return;

            _roots.Add(root);
            _changed.Invoke();
#endif
        }

        internal static void RemoveRoot(IContainerDiagnostics root)
        {
#if UNITY_EDITOR || DEBUG
            if (_roots.Remove(root) == false)
                return;

            _changed.Invoke();
#endif
        }

        public static void RecordLoadedAsset(IReadOnlyLifetime lifetime, string label, string groupName)
        {
#if UNITY_EDITOR || DEBUG
            if (lifetime == null)
                return;

            if (_pendingAssets.TryGetValue(lifetime, out var list) == false)
            {
                list = new List<LoadedAssetInfo>();
                _pendingAssets.Add(lifetime, list);
            }

            list.Add(new LoadedAssetInfo(label, groupName));
#endif
        }

        internal static IReadOnlyList<LoadedAssetInfo> TakeLoadedAssets(IReadOnlyLifetime lifetime)
        {
#if UNITY_EDITOR || DEBUG
            if (lifetime == null)
                return Array.Empty<LoadedAssetInfo>();

            if (_pendingAssets.TryGetValue(lifetime, out var list) == false)
                return Array.Empty<LoadedAssetInfo>();

            _pendingAssets.Remove(lifetime);
            return list;
#else
            return Array.Empty<LoadedAssetInfo>();
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _roots.Clear();
            _pendingAssets.Clear();
        }
    }
}
