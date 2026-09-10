using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class GraphDescriptors {
        public const string Category = "ContainerGenerator.Graph";

        public static readonly DiagnosticDescriptor UncoveredSyntax = new DiagnosticDescriptor(
            id: "CINGR001",
            title: "Uncovered container graph syntax",
            messageFormat: "Container graph walker cannot understand {0} in installer '{1}'. File {2} line {3}.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnresolvedInstaller = new DiagnosticDescriptor(
            id: "CINGR002",
            title: "Unresolved installer invocation",
            messageFormat: "Container graph walker cannot resolve installer call '{0}' in '{1}'. File {2} line {3}.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
