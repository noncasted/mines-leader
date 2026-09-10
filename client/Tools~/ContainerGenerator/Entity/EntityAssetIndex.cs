using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal sealed class EntityAsset {
        public string AssetPath = "";
        public string HolderType = "";
        public List<string> ComponentTypes = new List<string>();
    }

    internal sealed class EntityAssetIndex {
        public List<EntityAsset> Assets = new List<EntityAsset>();
        public List<DiagnosticInfo> Diagnostics = new List<DiagnosticInfo>();

        public EntityAsset? Find(string holderType) {
            var key = Normalize(holderType);
            if (string.IsNullOrEmpty(key))
                return null;

            for (var i = 0; i < Assets.Count; i++) {
                if (Normalize(Assets[i].HolderType) == key)
                    return Assets[i];
            }

            return null;
        }

        public static EntityAssetIndex Read(Compilation compilation) {
            var index = new EntityAssetIndex();
            if (compilation == null)
                return index;

            ReadSyntax(compilation, index);
            ReadAttributes(compilation.Assembly, index);
            foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
                ReadAttributes(assembly, index);

            ReportCollisions(compilation, index);
            return index;
        }

        private static void ReadSyntax(Compilation compilation, EntityAssetIndex index) {
            foreach (var tree in compilation.SyntaxTrees) {
                var root = tree.GetRoot();
                foreach (var node in root.DescendantNodes()) {
                    if (node is AttributeSyntax attribute)
                        ReadAttributeSyntax(attribute, index);
                    else if (node is ObjectCreationExpressionSyntax creation)
                        ReadCreation(creation, index);
                }
            }
        }

        private static void ReadAttributeSyntax(AttributeSyntax attribute, EntityAssetIndex index) {
            var name = attribute.Name.ToString();
            if (name.IndexOf("ContainerGraphAsset", StringComparison.Ordinal) < 0)
                return;
            if (attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count < 2)
                return;

            var asset = new EntityAsset {
                AssetPath = Literal(attribute.ArgumentList.Arguments[0].Expression),
                HolderType = Literal(attribute.ArgumentList.Arguments[1].Expression),
            };
            if (attribute.ArgumentList.Arguments.Count > 2)
                ReadStringArray(attribute.ArgumentList.Arguments[2].Expression, asset.ComponentTypes);
            Add(index, asset);
        }

        private static void ReadCreation(ObjectCreationExpressionSyntax creation, EntityAssetIndex index) {
            var type = creation.Type.ToString();
            if (type.IndexOf("ContainerGraphAsset", StringComparison.Ordinal) < 0)
                return;
            if (creation.ArgumentList == null || creation.ArgumentList.Arguments.Count < 2)
                return;

            var asset = new EntityAsset {
                AssetPath = Literal(creation.ArgumentList.Arguments[0].Expression),
                HolderType = Literal(creation.ArgumentList.Arguments[1].Expression),
            };
            if (creation.ArgumentList.Arguments.Count > 2)
                ReadStringArray(creation.ArgumentList.Arguments[2].Expression, asset.ComponentTypes);
            Add(index, asset);
        }

        private static void ReadAttributes(IAssemblySymbol assembly, EntityAssetIndex index) {
            if (assembly == null)
                return;

            foreach (var attribute in assembly.GetAttributes()) {
                var type = attribute.AttributeClass;
                if (type == null || type.Name != "ContainerGraphAssetAttribute")
                    continue;
                var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
                if (ns != "Internal")
                    continue;
                if (attribute.ConstructorArguments.Length < 2)
                    continue;

                var asset = new EntityAsset {
                    AssetPath = AsString(attribute.ConstructorArguments[0]),
                    HolderType = AsString(attribute.ConstructorArguments[1]),
                };
                if (attribute.ConstructorArguments.Length > 2)
                    AsStrings(attribute.ConstructorArguments[2], asset.ComponentTypes);
                Add(index, asset);
            }
        }

        private static void ReportCollisions(Compilation compilation, EntityAssetIndex index) {
            var byHolder = new Dictionary<string, List<EntityAsset>>(StringComparer.Ordinal);
            for (var i = 0; i < index.Assets.Count; i++) {
                var asset = index.Assets[i];
                var key = Normalize(asset.HolderType);
                if (string.IsNullOrEmpty(key))
                    continue;
                if (byHolder.TryGetValue(key, out var list) == false) {
                    list = new List<EntityAsset>();
                    byHolder[key] = list;
                }

                var seen = false;
                for (var p = 0; p < list.Count; p++) {
                    if (string.Equals(list[p].AssetPath, asset.AssetPath, StringComparison.Ordinal)) {
                        seen = true;
                        break;
                    }
                }

                if (seen == false)
                    list.Add(asset);
            }

            foreach (var pair in byHolder) {
                if (pair.Value.Count < 2)
                    continue;
                if (IsSceneFactory(pair.Value[0].HolderType))
                    continue;
                if (IsBaseViewType(compilation, pair.Value[0].HolderType))
                    continue;

                index.Diagnostics.Add(new DiagnosticInfo(
                    GraphDescriptors.DuplicateViewType,
                    null,
                    pair.Value[0].HolderType,
                    pair.Value[0].AssetPath,
                    pair.Value[1].AssetPath));
            }
        }

        private static bool IsSceneFactory(string holderType) {
            var name = Normalize(holderType);
            return name.EndsWith(".SceneServicesFactory", StringComparison.Ordinal) ||
                   name == "SceneServicesFactory" ||
                   name == "Internal.SceneServicesFactory";
        }

        internal static bool IsBaseViewType(Compilation compilation, string holderType) {
            var symbol = FindType(compilation, holderType);
            if (symbol == null)
                return false;
            if (symbol.IsAbstract)
                return true;
            if (symbol.Name == "ScopeEntityView")
                return true;

            foreach (var tree in compilation.SyntaxTrees) {
                var model = compilation.GetSemanticModel(tree);
                foreach (var node in tree.GetRoot().DescendantNodes()) {
                    if (node is not ClassDeclarationSyntax declaration)
                        continue;
                    var type = model.GetDeclaredSymbol(declaration) as INamedTypeSymbol;
                    if (type == null || SymbolEqualityComparer.Default.Equals(type, symbol))
                        continue;
                    if (Inherits(type, symbol))
                        return true;
                }
            }

            return false;
        }

        private static bool Inherits(INamedTypeSymbol type, INamedTypeSymbol parent) {
            var current = type.BaseType;
            while (current != null) {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, parent.OriginalDefinition))
                    return true;
                if (Normalize(TypeNames.ForMetadata(current)) == Normalize(TypeNames.ForMetadata(parent)))
                    return true;
                current = current.BaseType;
            }

            return false;
        }

        private static INamedTypeSymbol? FindType(Compilation compilation, string holderType) {
            var name = Normalize(holderType);
            if (string.IsNullOrEmpty(name))
                return null;

            var found = compilation.GetTypeByMetadataName(name);
            if (found != null)
                return found;

            foreach (var tree in compilation.SyntaxTrees) {
                var model = compilation.GetSemanticModel(tree);
                foreach (var node in tree.GetRoot().DescendantNodes()) {
                    if (node is not ClassDeclarationSyntax declaration)
                        continue;
                    var type = model.GetDeclaredSymbol(declaration) as INamedTypeSymbol;
                    if (type == null)
                        continue;
                    if (Normalize(TypeNames.ForMetadata(type)) == name)
                        return type;
                }
            }

            return null;
        }

        private static void Add(EntityAssetIndex index, EntityAsset asset) {
            if (string.IsNullOrEmpty(asset.AssetPath) && string.IsNullOrEmpty(asset.HolderType))
                return;

            for (var i = 0; i < index.Assets.Count; i++) {
                var existing = index.Assets[i];
                if (existing.AssetPath != asset.AssetPath || Normalize(existing.HolderType) != Normalize(asset.HolderType))
                    continue;

                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var c = 0; c < existing.ComponentTypes.Count; c++)
                    seen.Add(existing.ComponentTypes[c]);
                for (var c = 0; c < asset.ComponentTypes.Count; c++) {
                    if (seen.Add(asset.ComponentTypes[c]))
                        existing.ComponentTypes.Add(asset.ComponentTypes[c]);
                }

                return;
            }

            index.Assets.Add(asset);
        }

        private static void ReadStringArray(ExpressionSyntax expression, List<string> output) {
            expression = Unwrap(expression);
            InitializerExpressionSyntax? initializer = expression switch {
                ArrayCreationExpressionSyntax array => array.Initializer,
                ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer,
                _ => null,
            };
            if (initializer == null)
                return;

            foreach (var value in initializer.Expressions)
                output.Add(Literal(value));
        }

        private static string Literal(ExpressionSyntax expression) {
            expression = Unwrap(expression);
            if (expression is LiteralExpressionSyntax literal && literal.Token.Value is string text)
                return text;
            var raw = expression.ToString();
            if (raw.StartsWith("@\"", StringComparison.Ordinal) && raw.EndsWith("\"", StringComparison.Ordinal) && raw.Length >= 3)
                return raw.Substring(2, raw.Length - 3).Replace("\"\"", "\"");
            if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
                return raw.Substring(1, raw.Length - 2);
            return raw;
        }

        private static ExpressionSyntax Unwrap(ExpressionSyntax expression) {
            while (expression is ParenthesizedExpressionSyntax parenthesized)
                expression = parenthesized.Expression;
            return expression;
        }

        private static string AsString(TypedConstant constant) {
            return constant.Value as string ?? "";
        }

        private static void AsStrings(TypedConstant constant, List<string> output) {
            if (constant.Kind != TypedConstantKind.Array)
                return;
            foreach (var value in constant.Values) {
                var text = value.Value as string;
                if (string.IsNullOrEmpty(text) == false)
                    output.Add(text!);
            }
        }

        internal static string Normalize(string type) {
            if (string.IsNullOrEmpty(type))
                return "";
            const string prefix = "global::";
            return type.StartsWith(prefix, StringComparison.Ordinal) ? type.Substring(prefix.Length) : type;
        }
    }
}
