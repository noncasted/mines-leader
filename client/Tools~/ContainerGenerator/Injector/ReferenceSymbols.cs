using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal sealed class ReferenceSymbols {
        public INamedTypeSymbol Container { get; }
        public INamedTypeSymbol? UnityObject { get; }
        public INamedTypeSymbol? RuntimeInitialize { get; }
        public INamedTypeSymbol? GraphRootAttribute { get; }
        public INamedTypeSymbol? ScopeParentAttribute { get; }
        public INamedTypeSymbol? ScopeBuilder { get; }
        public INamedTypeSymbol? EntityBuilder { get; }
        public INamedTypeSymbol? Builder { get; }
        public INamedTypeSymbol? ServiceRegistration { get; }
        public INamedTypeSymbol? ContainerRegistry { get; }
        public INamedTypeSymbol? EntityComponent { get; }
        public INamedTypeSymbol? SceneService { get; }
        public INamedTypeSymbol? InjectAttribute { get; }

        private ReferenceSymbols(
            INamedTypeSymbol container,
            INamedTypeSymbol? unityObject,
            INamedTypeSymbol? runtimeInitialize,
            INamedTypeSymbol? graphRootAttribute,
            INamedTypeSymbol? scopeParentAttribute,
            INamedTypeSymbol? scopeBuilder,
            INamedTypeSymbol? entityBuilder,
            INamedTypeSymbol? builder,
            INamedTypeSymbol? serviceRegistration,
            INamedTypeSymbol? containerRegistry,
            INamedTypeSymbol? entityComponent,
            INamedTypeSymbol? sceneService,
            INamedTypeSymbol? injectAttribute) {
            Container = container;
            UnityObject = unityObject;
            RuntimeInitialize = runtimeInitialize;
            GraphRootAttribute = graphRootAttribute;
            ScopeParentAttribute = scopeParentAttribute;
            ScopeBuilder = scopeBuilder;
            EntityBuilder = entityBuilder;
            Builder = builder;
            ServiceRegistration = serviceRegistration;
            ContainerRegistry = containerRegistry;
            EntityComponent = entityComponent;
            SceneService = sceneService;
            InjectAttribute = injectAttribute;
        }

        // Сборка без Internal.IContainer контейнером не пользуется — генератору в ней делать нечего.
        public static ReferenceSymbols? Create(Compilation compilation) {
            var container = compilation.GetTypeByMetadataName("Internal.IContainer");
            if (container == null)
                return null;

            return new ReferenceSymbols(
                container,
                compilation.GetTypeByMetadataName("UnityEngine.Object"),
                compilation.GetTypeByMetadataName("UnityEngine.RuntimeInitializeOnLoadMethodAttribute"),
                compilation.GetTypeByMetadataName("Internal.ContainerGraphRootAttribute"),
                compilation.GetTypeByMetadataName("Internal.ContainerScopeParentAttribute"),
                compilation.GetTypeByMetadataName("Internal.IScopeBuilder"),
                compilation.GetTypeByMetadataName("Internal.IEntityBuilder"),
                compilation.GetTypeByMetadataName("Internal.IBuilder"),
                compilation.GetTypeByMetadataName("Internal.IServiceRegistration"),
                compilation.GetTypeByMetadataName("Internal.IContainerRegistry"),
                compilation.GetTypeByMetadataName("Internal.IEntityComponent"),
                compilation.GetTypeByMetadataName("Internal.ISceneService"),
                compilation.GetTypeByMetadataName("Internal.InjectAttribute"));
        }
    }
}
