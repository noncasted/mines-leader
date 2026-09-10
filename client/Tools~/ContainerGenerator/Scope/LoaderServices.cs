using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class LoaderServices {
        public const string EventLoopImplementation = "Internal.EventLoop";
        public const string EventLoopService = "Internal.IEventLoop";
        public const string ViewInjectorImplementation = "Internal.GeneratedViewInjector";
        public const string ViewInjectorService = "Internal.IViewInjector";
        public const string Origin = "LoaderService";

        public static readonly string[] Implementations = {
            EventLoopImplementation,
            ViewInjectorImplementation,
        };

        public static readonly string[][] Services = {
            new[] { EventLoopService },
            new[] { ViewInjectorService },
        };

        public static INamedTypeSymbol? Find(Compilation compilation, string metadataName) {
            if (compilation == null || string.IsNullOrEmpty(metadataName))
                return null;
            return compilation.GetTypeByMetadataName(metadataName);
        }
    }
}
