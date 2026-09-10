using System;
using System.Collections.Generic;
using System.IO;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    /// <summary>
    /// Live Graph Toolkit view of <see cref="ContainerRegistryDebug.Roots"/>.
    /// The graph asset is a transient scratch file, not a source of truth.
    /// </summary>
    [Serializable]
    [Graph(AssetExtension)]
    public sealed class ContainerLiveGraph : Graph
    {
        public const string AssetExtension = "containergraph";
        public const string EmptyEditModeStatus = "No containers. Enter Play Mode to inspect a built graph.";
        public const string EmptyPlayModeStatus = "No containers yet. Waiting for a container Build.";

        internal const string AssetPath =
            "Assets/Common/Internal/Editor/Tools/ContainerGraph/Transient/ContainerLiveGraph.containergraph";

        private const float OriginX = 40f;

        private const float OriginY = 40f;

        // Дерево растёт слева направо, как идут связи Children → Parent: колонка — глубина скоупа.
        private const float ColumnWidth = 480f;
        private const float SiblingGap = 24f;
        private const float RootGap = 64f;

        [NonSerialized]
        private Lifetime _lifetime;

        [NonSerialized]
        private bool _rebuildScheduled;

        [NonSerialized]
        private bool _isRebuilding;

        public string Status { get; private set; } = EmptyEditModeStatus;

        [MenuItem("Tools/Container Graph")]
        public static void Open()
        {
            try
            {
                var graph = EnsureGraph();

                if (graph == null)
                    return;

                var path = GraphDatabase.GetGraphAssetPath(graph);

                if (string.IsNullOrEmpty(path))
                    path = AssetPath;

                var asset = AssetDatabase.LoadMainAssetAtPath(path);

                if (asset != null)
                    AssetDatabase.OpenAsset(asset);

                graph.Rebuild();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public override void OnEnable()
        {
            base.OnEnable();

            _lifetime?.Terminate();
            _lifetime = new Lifetime();

            try
            {
                ContainerRegistryDebug.Changed.Advise(_lifetime, ScheduleRebuild);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            ScheduleRebuild();
        }

        public override void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _lifetime?.Terminate();
            _lifetime = null;
            _rebuildScheduled = false;
            base.OnDisable();
        }

        internal void Rebuild()
        {
            if (_isRebuilding)
                return;

            _isRebuilding = true;

            try
            {
                RebuildNodes();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Status = "Failed to read container diagnostics";
            }
            finally
            {
                _isRebuilding = false;
            }
        }

        private void ScheduleRebuild()
        {
            if (_rebuildScheduled)
                return;

            _rebuildScheduled = true;
            EditorApplication.delayCall += OnDelayedRebuild;
        }

        private void OnDelayedRebuild()
        {
            _rebuildScheduled = false;

            if (IsDisplayed() == false)
                return;

            Rebuild();
        }

        private void OnPlayModeChanged(PlayModeStateChange _)
        {
            ScheduleRebuild();
        }

        private void RebuildNodes()
        {
            var roots = ContainerGraphRead.List(() => ContainerRegistryDebug.Roots);
            var nodes = new Dictionary<IContainerDiagnostics, ContainerNode>();
            var visited = new HashSet<IContainerDiagnostics>();

            UndoBeginRecordGraph("Refresh container tree");

            try
            {
                ClearNodes();

                var cursorY = OriginY;

                foreach (var root in roots)
                {
                    if (root == null)
                        continue;

                    var height = AddTree(root, null, OriginX, cursorY, nodes, visited);

                    if (height > 0f)
                        cursorY += height + RootGap;
                }

                ConnectParents(nodes);
            }
            finally
            {
                UndoEndRecordGraph();
            }

            Status = FormatStatus(roots.Count, nodes.Count);

            try
            {
                GraphDatabase.SaveGraph(this);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private float AddTree(
            IContainerDiagnostics diagnostics,
            IContainerDiagnostics parent,
            float x,
            float y,
            Dictionary<IContainerDiagnostics, ContainerNode> nodes,
            HashSet<IContainerDiagnostics> visited)
        {
            if (visited.Add(diagnostics) == false)
                return 0f;

            var node = new ContainerNode();
            node.Capture(diagnostics, parent);
            AddNode(node);
            node.Position = new Vector2(x, y);
            node.ApplyPresentation();
            nodes[diagnostics] = node;

            var children = ContainerGraphRead.List(() => diagnostics.Children);
            var childY = y;

            foreach (var child in children)
            {
                if (child == null)
                    continue;

                var height = AddTree(child, diagnostics, x + ColumnWidth, childY, nodes, visited);

                if (height > 0f)
                    childY += height + SiblingGap;
            }

            var childrenHeight = childY > y ? childY - y - SiblingGap : 0f;
            return Math.Max(node.EstimatedHeight, childrenHeight);
        }

        private void ConnectParents(Dictionary<IContainerDiagnostics, ContainerNode> nodes)
        {
            foreach (var pair in nodes)
            {
                var parent = ContainerGraphRead.Parent(pair.Key);

                if (parent == null)
                    continue;

                if (nodes.TryGetValue(parent, out var parentNode) == false)
                    continue;

                var output = parentNode.GetOutputPortByName(ContainerNode.ChildrenPortName);
                var input = pair.Value.GetInputPortByName(ContainerNode.ParentPortName);

                if (output == null || input == null)
                    continue;

                Connect(output, input);
            }
        }

        private void ClearNodes()
        {
            var existing = new List<INode>();

            foreach (var node in GetNodes())
                existing.Add(node);

            foreach (var node in existing)
                RemoveNode(node);
        }

        private bool IsDisplayed()
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();

            foreach (var window in windows)
            {
                if (window is IGraphWindow graphWindow && ReferenceEquals(graphWindow.Graph, this))
                    return true;
            }

            return false;
        }

        private static string FormatStatus(int rootCount, int containerCount)
        {
            if (rootCount == 0 || containerCount == 0)
            {
                return EditorApplication.isPlaying
                    ? EmptyPlayModeStatus
                    : EmptyEditModeStatus;
            }

            var mode = EditorApplication.isPlaying ? "Play Mode" : "Edit Mode";
            return $"{rootCount} root(s) · {containerCount} container(s) · {mode}";
        }

        private static ContainerLiveGraph EnsureGraph()
        {
            var directory = Path.Combine(
                Application.dataPath,
                "Common/Internal/Editor/Tools/ContainerGraph/Transient");

            Directory.CreateDirectory(directory);

            try
            {
                if (File.Exists(Path.Combine(directory, "ContainerLiveGraph.containergraph")))
                {
                    var loaded = GraphDatabase.LoadGraph<ContainerLiveGraph>(AssetPath);

                    if (loaded != null)
                        return loaded;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            return GraphDatabase.CreateGraph<ContainerLiveGraph>(AssetPath);
        }
    }

    internal static class ContainerGraphRead
    {
        public static IReadOnlyList<T> List<T>(Func<IReadOnlyList<T>> read)
        {
            try
            {
                return read() ?? Array.Empty<T>();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return Array.Empty<T>();
            }
        }

        public static IContainerDiagnostics Parent(IContainerDiagnostics diagnostics)
        {
            if (diagnostics == null)
                return null;

            try
            {
                return diagnostics.Parent;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
        }

        public static string Name(IContainerDiagnostics diagnostics)
        {
            if (diagnostics == null)
                return "(none)";

            try
            {
                return string.IsNullOrEmpty(diagnostics.Name) ? "(unnamed)" : diagnostics.Name;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return "(error)";
            }
        }

        public static string TypeName(Type type)
        {
            if (type == null)
                return "—";

            if (type.IsGenericType == false)
                return type.Name;

            var tick = type.Name.IndexOf('`');
            var name = tick >= 0 ? type.Name.Substring(0, tick) : type.Name;
            var arguments = type.GetGenericArguments();
            var inner = new string[arguments.Length];

            for (var i = 0; i < arguments.Length; i++)
                inner[i] = TypeName(arguments[i]);

            return $"{name}<{string.Join(", ", inner)}>";
        }

        public static string Registration(RegistrationInfo info)
        {
            var implementation = TypeName(info.ImplementationType);
            var services = ServiceTypes(info.ServiceTypes);
            return $"[{info.Slot}] {implementation} : {services} · {info.Lifetime}";
        }

        public static string Asset(LoadedAssetInfo info)
        {
            var label = string.IsNullOrEmpty(info.Label) ? "(no label)" : info.Label;
            var group = string.IsNullOrEmpty(info.GroupName) ? "(no group)" : info.GroupName;
            return $"{label} · {group}";
        }

        public static string PathOf(IContainerDiagnostics diagnostics, IContainerDiagnostics parent)
        {
            var name = Name(diagnostics);

            if (parent == null)
                return name;

            return $"{Name(parent)} / {name}";
        }

        private static string ServiceTypes(IReadOnlyList<Type> types)
        {
            if (types == null || types.Count == 0)
                return "—";

            if (types.Count == 1)
                return TypeName(types[0]);

            var names = new string[types.Count];

            for (var i = 0; i < types.Count; i++)
                names[i] = TypeName(types[i]);

            return string.Join(", ", names);
        }
    }
}