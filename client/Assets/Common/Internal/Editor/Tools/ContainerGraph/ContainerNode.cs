using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Internal
{
    [Serializable]
    [Node("Containers")]
    [UseWithGraph(typeof(ContainerLiveGraph))]
    public sealed class ContainerNode : Node
    {
        internal const string ParentPortName = "parent";
        internal const string ChildrenPortName = "children";
        internal const string ExternalOptionName = "external";
        internal const string ServicesOptionName = "services";
        internal const string AssetsOptionName = "assets";

        private static readonly Color RootColor = new(0.29f, 0.72f, 0.71f);
        private static readonly Color ChildColor = new(0.36f, 0.63f, 0.80f);

        [SerializeField]
        private string _containerName = "(unnamed)";

        [SerializeField]
        private string _path = string.Empty;

        [SerializeField]
        private bool _isRoot;

        [SerializeField]
        private string[] _externalDependencies = Array.Empty<string>();

        [SerializeField]
        private string[] _services = Array.Empty<string>();

        [SerializeField]
        private string[] _loadedAssets = Array.Empty<string>();

        internal string ContainerName => string.IsNullOrEmpty(_containerName) ? "(unnamed)" : _containerName;

        internal string Path => _path ?? string.Empty;

        internal IReadOnlyList<string> ExternalDependencies => _externalDependencies ?? Array.Empty<string>();

        internal IReadOnlyList<string> Services => _services ?? Array.Empty<string>();

        internal IReadOnlyList<string> LoadedAssets => _loadedAssets ?? Array.Empty<string>();

        // Высота для раскладки: заголовок с портами и три секции, скролл секции — до 72px (ContainerNodeView).
        internal float EstimatedHeight => HeaderHeight +
                                          SectionHeight(ExternalDependencies) +
                                          SectionHeight(Services) +
                                          SectionHeight(LoadedAssets);

        private const float HeaderHeight = 72f;
        private const float SectionTitleHeight = 20f;
        private const float LineHeight = 14f;
        private const float SectionScrollHeight = 72f;

        internal void Capture(IContainerDiagnostics diagnostics, IContainerDiagnostics parent)
        {
            _containerName = ContainerGraphRead.Name(diagnostics);
            _path = ContainerGraphRead.PathOf(diagnostics, parent);
            _isRoot = parent == null;
            _externalDependencies = CollectRegistrations(diagnostics, isExternal: true);
            _services = CollectRegistrations(diagnostics, isExternal: false);
            _loadedAssets = CollectAssets(diagnostics);
        }

        internal void ApplyPresentation()
        {
            var external = ExternalDependencies;
            var services = Services;
            var assets = LoadedAssets;

            Title = ContainerName;
            Subtitle = $"{external.Count} external · {services.Count} services · {assets.Count} assets";
            Tooltip = Path;
            DefaultColor = _isRoot ? RootColor : ChildColor;

            SetOption(ExternalOptionName, Join(external));
            SetOption(ServicesOptionName, Join(services));
            SetOption(AssetsOptionName, Join(assets));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort(ParentPortName)
                   .WithDisplayName("Parent")
                   .WithCapacity(PortCapacity.Single)
                   .Build();

            context.AddOutputPort(ChildrenPortName)
                   .WithDisplayName("Children")
                   .WithCapacity(PortCapacity.Multi)
                   .Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>(ExternalOptionName)
                   .WithDisplayName("External dependencies")
                   .WithTooltip("Registrations with IsExternal == true (slots flattened from the parent).")
                   .AsTextArea(3, 16)
                   .ShowInInspectorOnly()
                   .Build();

            context.AddOption<string>(ServicesOptionName)
                   .WithDisplayName("Registry")
                   .WithTooltip("Registrations owned by this container (IsExternal == false).")
                   .AsTextArea(3, 16)
                   .ShowInInspectorOnly()
                   .Build();

            context.AddOption<string>(AssetsOptionName)
                   .WithDisplayName("Loaded assets")
                   .WithTooltip("Assets loaded into the scope via LoadAssetGroup.")
                   .AsTextArea(3, 16)
                   .ShowInInspectorOnly()
                   .Build();
        }

        private void SetOption(string name, string value)
        {
            var option = GetNodeOptionByName(name);

            if (option == null)
                return;

            option.TrySetValue(value);
        }

        private static string[] CollectRegistrations(IContainerDiagnostics diagnostics, bool isExternal)
        {
            var registrations = ContainerGraphRead.List(() => diagnostics.Registrations);
            var lines = new List<string>();

            foreach (var info in registrations)
            {
                if (info.IsExternal != isExternal)
                    continue;

                lines.Add(ContainerGraphRead.Registration(info));
            }

            return lines.ToArray();
        }

        private static string[] CollectAssets(IContainerDiagnostics diagnostics)
        {
            var assets = ContainerGraphRead.List(() => diagnostics.LoadedAssets);
            var lines = new string[assets.Count];

            for (var i = 0; i < assets.Count; i++)
                lines[i] = ContainerGraphRead.Asset(assets[i]);

            return lines;
        }

        private static float SectionHeight(IReadOnlyList<string> lines)
        {
            return SectionTitleHeight + Math.Min(SectionScrollHeight, LineHeight * Math.Max(1, lines.Count));
        }

        private static string Join(IReadOnlyList<string> lines)
        {
            if (lines == null || lines.Count == 0)
                return "(none)";

            return string.Join("\n", lines);
        }
    }
}