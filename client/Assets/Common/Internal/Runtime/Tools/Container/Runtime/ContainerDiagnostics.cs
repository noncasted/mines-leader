using System;
using System.Collections.Generic;

namespace Internal
{
    internal sealed class ContainerDiagnostics : IContainerDiagnostics
    {
        public ContainerDiagnostics(
            string name,
            IContainerDiagnostics parent,
            RegistrationInfo[] registrations,
            int[] buildOrder)
        {
            Name = name;
            Parent = parent;
            Registrations = registrations;
            BuildOrder = buildOrder;
            LoadedAssets = Array.Empty<LoadedAssetInfo>();
        }

        private readonly List<IContainerDiagnostics> _children = new();
        private readonly List<ResolveRecord> _history = new();

        public string Name { get; }
        public IContainerDiagnostics Parent { get; }
        public IReadOnlyList<IContainerDiagnostics> Children => _children;
        public IReadOnlyList<RegistrationInfo> Registrations { get; private set; }
        public IReadOnlyList<int> BuildOrder { get; private set; }
        public IReadOnlyList<LoadedAssetInfo> LoadedAssets { get; private set; }
        public bool IsHistoryEnabled { get; set; }
        public IReadOnlyList<ResolveRecord> History => _history;

        internal void SetSnapshot(
            RegistrationInfo[] registrations,
            int[] buildOrder,
            IReadOnlyList<LoadedAssetInfo> loadedAssets)
        {
            Registrations = registrations;
            BuildOrder = buildOrder;
            LoadedAssets = loadedAssets ?? Array.Empty<LoadedAssetInfo>();
        }

        internal void AddChild(IContainerDiagnostics child)
        {
            _children.Add(child);
        }

        internal void RemoveChild(IContainerDiagnostics child)
        {
            _children.Remove(child);
        }

        internal void Record(ResolveRecord record)
        {
            _history.Add(record);
        }
    }
}
