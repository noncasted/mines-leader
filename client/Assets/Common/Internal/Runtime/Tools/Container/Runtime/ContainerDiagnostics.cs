using System;
using System.Collections.Generic;

namespace Internal
{
    // Регистрации — статическая таблица сгенерированного класса, общая для всех его экземпляров.
    // Дети и загруженные ассеты дописываются при сборке (GeneratedScopes, ContainerRegistryDebug).
    public sealed class ContainerDiagnostics : IContainerDiagnostics
    {
        public ContainerDiagnostics(
            string name,
            IContainerDiagnostics parent,
            IReadOnlyList<RegistrationInfo> registrations,
            IReadOnlyList<int> buildOrder)
        {
            Name = name;
            Parent = parent;
            Registrations = registrations ?? Array.Empty<RegistrationInfo>();
            BuildOrder = buildOrder ?? Array.Empty<int>();
        }

        private List<IContainerDiagnostics> _children;

        public string Name { get; }
        public IContainerDiagnostics Parent { get; }

        public IReadOnlyList<IContainerDiagnostics> Children => (IReadOnlyList<IContainerDiagnostics>)_children ??
                                                                Array.Empty<IContainerDiagnostics>();

        public IReadOnlyList<RegistrationInfo> Registrations { get; }
        public IReadOnlyList<int> BuildOrder { get; }
        public IReadOnlyList<LoadedAssetInfo> LoadedAssets { get; private set; } = Array.Empty<LoadedAssetInfo>();
        public bool IsHistoryEnabled { get; set; }
        public IReadOnlyList<ResolveRecord> History => Array.Empty<ResolveRecord>();

        internal void SetLoadedAssets(IReadOnlyList<LoadedAssetInfo> loadedAssets)
        {
            LoadedAssets = loadedAssets ?? Array.Empty<LoadedAssetInfo>();
        }

        internal void AddChild(IContainerDiagnostics child)
        {
            _children ??= new List<IContainerDiagnostics>();
            _children.Add(child);
        }

        internal bool RemoveChild(IContainerDiagnostics child)
        {
            return _children != null && _children.Remove(child);
        }
    }
}