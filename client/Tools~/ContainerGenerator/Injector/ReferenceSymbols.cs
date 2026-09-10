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
        public INamedTypeSymbol? Registration { get; }
        public INamedTypeSymbol? ServiceRegistration { get; }
        public INamedTypeSymbol? EntityComponent { get; }
        public INamedTypeSymbol? SceneService { get; }

        private ReferenceSymbols(
            INamedTypeSymbol container,
            INamedTypeSymbol? unityObject,
            INamedTypeSymbol? runtimeInitialize,
            INamedTypeSymbol? graphRootAttribute,
            INamedTypeSymbol? scopeParentAttribute,
            INamedTypeSymbol? scopeBuilder,
            INamedTypeSymbol? entityBuilder,
            INamedTypeSymbol? builder,
            INamedTypeSymbol? registration,
            INamedTypeSymbol? serviceRegistration,
            INamedTypeSymbol? entityComponent,
            INamedTypeSymbol? sceneService) {
            Container = container;
            UnityObject = unityObject;
            RuntimeInitialize = runtimeInitialize;
            GraphRootAttribute = graphRootAttribute;
            ScopeParentAttribute = scopeParentAttribute;
            ScopeBuilder = scopeBuilder;
            EntityBuilder = entityBuilder;
            Builder = builder;
            Registration = registration;
            ServiceRegistration = serviceRegistration;
            EntityComponent = entityComponent;
            SceneService = sceneService;
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
                compilation.GetTypeByMetadataName("Internal.IRegistration"),
                compilation.GetTypeByMetadataName("Internal.IServiceRegistration"),
                compilation.GetTypeByMetadataName("Internal.IEntityComponent"),
                compilation.GetTypeByMetadataName("Internal.ISceneService"));
        }
    }
}
