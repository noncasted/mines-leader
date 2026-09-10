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

        // Новые контейнеры не создают диагностику, пока флаг снят (бенчмарк). В релизном билде
        // (без UNITY_EDITOR и DEBUG) сгенерированные классы её не создают вовсе.
        public static bool IsEnabled { get; set; } = true;

        private static readonly List<IContainerDiagnostics> _roots = new();
        private static readonly ViewableDelegate _changed = new();
        private static readonly Dictionary<IReadOnlyLifetime, List<LoadedAssetInfo>> _pendingAssets = new();

        // Контейнер с родителем висит в его Children, в Roots попадают только корни дерева.
        internal static void Add(IContainerDiagnostics diagnostics)
        {
#if UNITY_EDITOR || DEBUG
            if (diagnostics == null)
                return;

            if (diagnostics.Parent is ContainerDiagnostics parent)
                parent.AddChild(diagnostics);
            else if (_roots.Contains(diagnostics) == false)
                _roots.Add(diagnostics);
            else
                return;

            _changed.Invoke();
#endif
        }

        internal static void Remove(IContainerDiagnostics diagnostics)
        {
#if UNITY_EDITOR || DEBUG
            if (diagnostics == null)
                return;

            var removed = diagnostics.Parent is ContainerDiagnostics parent
                ? parent.RemoveChild(diagnostics)
                : _roots.Remove(diagnostics);

            if (removed == false)
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