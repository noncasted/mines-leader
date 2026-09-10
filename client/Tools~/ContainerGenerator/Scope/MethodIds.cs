using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class MethodIds {
        public static string Of(IMethodSymbol method) {
            if (method.MethodKind == MethodKind.LocalFunction && method.ContainingSymbol is IMethodSymbol parent)
                return Of(parent) + "+" + method.Name;

            var definition = method.ReducedFrom ?? method.OriginalDefinition ?? method;
            var type = definition.ContainingType != null ? TypeNames.ForMetadata(definition.ContainingType) : "";
            var name = string.IsNullOrEmpty(type) ? definition.Name : type + "." + definition.Name;
            if (method.TypeArguments.Length == 0)
                return name;

            var arguments = new string[method.TypeArguments.Length];
            for (var i = 0; i < method.TypeArguments.Length; i++)
                arguments[i] = TypeNames.ForMetadata(method.TypeArguments[i]);
            return name + "<" + string.Join(", ", arguments) + ">";
        }

        public static IMethodSymbol? Find(Compilation compilation, string rootId) {
            if (compilation == null || string.IsNullOrEmpty(rootId))
                return null;

            foreach (var tree in compilation.SyntaxTrees) {
                var model = compilation.GetSemanticModel(tree);
                foreach (var node in tree.GetRoot().DescendantNodes()) {
                    IMethodSymbol? method = null;
                    if (node is Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax methodSyntax)
                        method = model.GetDeclaredSymbol(methodSyntax) as IMethodSymbol;
                    else if (node is Microsoft.CodeAnalysis.CSharp.Syntax.LocalFunctionStatementSyntax localSyntax)
                        method = model.GetDeclaredSymbol(localSyntax) as IMethodSymbol;

                    if (method == null)
                        continue;
                    if (Of(method) == rootId)
                        return method;
                }
            }

            return null;
        }

        public static bool HasRuntimeScope(IMethodSymbol? method, INamedTypeSymbol? attribute) {
            if (method == null || attribute == null)
                return false;

            var current = method;
            while (current != null) {
                foreach (var data in current.GetAttributes()) {
                    if (SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute))
                        return true;
                }

                current = current.ContainingSymbol as IMethodSymbol;
            }

            return false;
        }
    }
}
