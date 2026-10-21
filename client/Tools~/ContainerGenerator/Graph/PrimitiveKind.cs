using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    // Mirror of Internal.ContainerPrimitive (Common/Internal/Runtime/Tools/Container/Generated/ContainerPrimitiveAttribute.cs):
    // the builder API marks its registration primitives with [ContainerPrimitive(kind)], the walker reads the kind.
    internal enum PrimitiveKind {
        None = 0,
        Register = 1,
        RegisterInstance = 2,
        RegisterComponent = 3,
        As = 4,
        AsSelf = 5,
        AsSelfResolvable = 6,
        WithParameter = 7,
        WithScopeLifetime = 8,
        Inject = 9,
        Injectable = 10,
        Instantiate = 11,
        SceneServices = 12,
    }

    internal static class Primitives {
        public const string AttributeName = "Internal.ContainerPrimitiveAttribute";

        // A bound call is a primitive only when its method carries the attribute. A call without a symbol (the code
        // does not compile yet, e.g. a generated member is still missing) falls back to the method name, so the walk
        // keeps producing a graph for the rest of the installer.
        public static PrimitiveKind Of(IMethodSymbol? symbol, string name) {
            if (symbol == null)
                return OfName(name);
            return OfSymbol(symbol);
        }

        public static PrimitiveKind OfSymbol(IMethodSymbol symbol) {
            var definition = symbol.ReducedFrom ?? symbol.OriginalDefinition ?? symbol;
            if (definition.ReducedFrom != null)
                definition = definition.ReducedFrom;
            foreach (var attribute in definition.GetAttributes()) {
                if (attribute.AttributeClass?.ToDisplayString() != AttributeName)
                    continue;
                if (attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].Value is not int value)
                    return PrimitiveKind.None;
                return (PrimitiveKind)value;
            }

            return PrimitiveKind.None;
        }

        public static bool IsRegistration(PrimitiveKind kind) {
            return kind == PrimitiveKind.Register || kind == PrimitiveKind.RegisterInstance ||
                   kind == PrimitiveKind.RegisterComponent;
        }

        private static PrimitiveKind OfName(string name) {
            switch (name) {
                case "Register": return PrimitiveKind.Register;
                case "RegisterInstance": return PrimitiveKind.RegisterInstance;
                case "RegisterComponent": return PrimitiveKind.RegisterComponent;
                case "As": return PrimitiveKind.As;
                case "AsSelf": return PrimitiveKind.AsSelf;
                case "AsSelfResolvable": return PrimitiveKind.AsSelfResolvable;
                case "WithParameter": return PrimitiveKind.WithParameter;
                case "WithScopeLifetime": return PrimitiveKind.WithScopeLifetime;
                case "Inject": return PrimitiveKind.Inject;
                case "Injectable": return PrimitiveKind.Injectable;
                case "Instantiate": return PrimitiveKind.Instantiate;
                case "FindOrLoadSceneWithServices": return PrimitiveKind.SceneServices;
                default: return PrimitiveKind.None;
            }
        }
    }
}
