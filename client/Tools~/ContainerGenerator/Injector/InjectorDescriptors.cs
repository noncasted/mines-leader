using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class InjectorDescriptors {
        public const string Category = "ContainerGenerator.Injector";

        public static readonly DiagnosticDescriptor NestedNotSupported = new DiagnosticDescriptor(
            id: "CING001",
            title: "Nested type is not generated",
            messageFormat: "Injectable type '{0}' is nested. Nested types are not covered by the injector generator.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor GenericNotSupported = new DiagnosticDescriptor(
            id: "CING002",
            title: "Generic type is not generated",
            messageFormat: "Injectable type '{0}' is generic. Generic types are not covered by the injector generator.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MultipleConstruct = new DiagnosticDescriptor(
            id: "CING003",
            title: "Multiple Construct methods",
            messageFormat: "Type '{0}' has more than one instance method named Construct; only one is allowed",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InaccessibleConstruct = new DiagnosticDescriptor(
            id: "CING004",
            title: "Construct is not accessible",
            messageFormat: "Construct on '{0}' is not public or internal, so the generated injector cannot call it",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor GenericConstruct = new DiagnosticDescriptor(
            id: "CING005",
            title: "Generic Construct is not generated",
            messageFormat: "Construct on '{0}' is generic and cannot be called by the generated injector",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
