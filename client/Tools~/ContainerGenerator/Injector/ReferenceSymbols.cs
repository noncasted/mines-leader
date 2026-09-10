using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal sealed class ReferenceSymbols {
        public INamedTypeSymbol Injector { get; }
        public INamedTypeSymbol? UnityObject { get; }
        public INamedTypeSymbol? RuntimeInitialize { get; }
        public INamedTypeSymbol? GraphRootAttribute { get; }
        public INamedTypeSymbol? RuntimeScopeAttribute { get; }
        public INamedTypeSymbol? ScopeBuilder { get; }
        public INamedTypeSymbol? EntityBuilder { get; }
        public INamedTypeSymbol? Builder { get; }
        public INamedTypeSymbol? Registration { get; }
        public INamedTypeSymbol? ServiceRegistration { get; }
        public INamedTypeSymbol? EntityComponent { get; }
        public INamedTypeSymbol? SceneService { get; }

        private ReferenceSymbols(
            INamedTypeSymbol injector,
            INamedTypeSymbol? unityObject,
            INamedTypeSymbol? runtimeInitialize,
            INamedTypeSymbol? graphRootAttribute,
            INamedTypeSymbol? runtimeScopeAttribute,
            INamedTypeSymbol? scopeBuilder,
            INamedTypeSymbol? entityBuilder,
            INamedTypeSymbol? builder,
            INamedTypeSymbol? registration,
            INamedTypeSymbol? serviceRegistration,
            INamedTypeSymbol? entityComponent,
            INamedTypeSymbol? sceneService) {
            Injector = injector;
            UnityObject = unityObject;
            RuntimeInitialize = runtimeInitialize;
            GraphRootAttribute = graphRootAttribute;
            RuntimeScopeAttribute = runtimeScopeAttribute;
            ScopeBuilder = scopeBuilder;
            EntityBuilder = entityBuilder;
            Builder = builder;
            Registration = registration;
            ServiceRegistration = serviceRegistration;
            EntityComponent = entityComponent;
            SceneService = sceneService;
        }

        public static ReferenceSymbols? Create(Compilation compilation) {
            var injector = compilation.GetTypeByMetadataName("Internal.IInjector");
            if (injector == null)
                return null;

            return new ReferenceSymbols(
                injector,
                compilation.GetTypeByMetadataName("UnityEngine.Object"),
                compilation.GetTypeByMetadataName("UnityEngine.RuntimeInitializeOnLoadMethodAttribute"),
                compilation.GetTypeByMetadataName("Internal.ContainerGraphRootAttribute"),
                compilation.GetTypeByMetadataName("Internal.ContainerRuntimeScopeAttribute"),
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
