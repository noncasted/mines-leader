using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class SymbolExtensions {
        public static bool CanCallFromSameAssembly(this IMethodSymbol method) {
            switch (method.DeclaredAccessibility) {
                case Accessibility.Public:
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsOpenGeneric(this INamedTypeSymbol type) {
            return type.IsGenericType && type.IsUnboundGenericType == false && type.TypeArguments.Length > 0 && HasUnbound(type);
        }

        public static bool InheritsFrom(this INamedTypeSymbol type, INamedTypeSymbol? baseType) {
            if (baseType == null)
                return false;

            var current = type;
            while (current != null) {
                if (SymbolEqualityComparer.Default.Equals(current, baseType))
                    return true;

                current = current.BaseType;
            }

            return false;
        }

        public static bool Implements(this ITypeSymbol type, INamedTypeSymbol? interfaceType) {
            if (interfaceType == null)
                return false;

            if (SymbolEqualityComparer.Default.Equals(type, interfaceType))
                return true;

            foreach (var implemented in type.AllInterfaces) {
                if (SymbolEqualityComparer.Default.Equals(implemented, interfaceType) ||
                    SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, interfaceType))
                    return true;
            }

            return false;
        }

        public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol ns) {
            foreach (var type in ns.GetTypeMembers()) {
                yield return type;
                foreach (var nested in GetNested(type))
                    yield return nested;
            }

            foreach (var child in ns.GetNamespaceMembers()) {
                foreach (var type in GetAllTypes(child))
                    yield return type;
            }
        }

        private static IEnumerable<INamedTypeSymbol> GetNested(INamedTypeSymbol type) {
            foreach (var nested in type.GetTypeMembers()) {
                yield return nested;
                foreach (var inner in GetNested(nested))
                    yield return inner;
            }
        }

        private static bool HasUnbound(INamedTypeSymbol type) {
            foreach (var argument in type.TypeArguments) {
                if (argument.TypeKind == TypeKind.TypeParameter)
                    return true;

                if (argument is INamedTypeSymbol named && HasUnbound(named))
                    return true;
            }

            return false;
        }
    }
}
