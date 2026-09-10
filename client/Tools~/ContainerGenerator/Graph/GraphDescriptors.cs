using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class GraphDescriptors {
        public const string Category = "ContainerGenerator.Graph";

        public static readonly DiagnosticDescriptor UncoveredSyntax = new DiagnosticDescriptor(
            id: "CINGR001",
            title: "Uncovered container graph syntax",
            messageFormat: "Container graph walker cannot understand {0} in installer '{1}'. File {2} line {3}.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnresolvedInstaller = new DiagnosticDescriptor(
            id: "CINGR002",
            title: "Unresolved installer invocation",
            messageFormat: "Container graph walker cannot resolve installer call '{0}' in '{1}'. File {2} line {3}.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MissingRegistration = new DiagnosticDescriptor(
            id: "CINGR003",
            title: "Missing container registration",
            messageFormat: "Cannot resolve parameter '{0}' of type '{1}' for '{2}'. File {3} line {4}.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor CircularDependency = new DiagnosticDescriptor(
            id: "CINGR004",
            title: "Circular container dependency",
            messageFormat: "Circular dependency: {0}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnenumerableVariant = new DiagnosticDescriptor(
            id: "CINGR005",
            title: "Cannot enumerate entity scope variants",
            messageFormat: "Cannot enumerate scope variants from '{0}' in '{1}'. Only bool and closed enum parameters may change the graph. Simplify the condition or split the root. File {2} line {3}.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnresolvedParent = new DiagnosticDescriptor(
            id: "CINGR007",
            title: "Cannot resolve declared parent scope",
            messageFormat: "Parent scope manifest '{0}' from assembly '{1}' was not found (declared on '{2}')",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateViewType = new DiagnosticDescriptor(
            id: "CINGR006",
            title: "Duplicate ScopeEntityView type",
            messageFormat: "View type '{0}' is used by more than one asset: '{1}' and '{2}'. Create a distinct ScopeEntityView subtype for each asset.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
