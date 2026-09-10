using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    // Что нужно типу: параметры конструктора и метода с [Inject]. Рёбра по ним строит EdgeResolver.
    // Метод инжекта ищется только по атрибуту Internal.InjectAttribute, имя метода не важно.
    internal static class TypeAnalyzer {
        public static InjectorModel? Analyze(
            INamedTypeSymbol type,
            ReferenceSymbols references,
            LocationInfo? location,
            bool requireConstructOrExplicitCtor = true) {
            if (type.TypeKind != TypeKind.Class)
                return null;
            if (type.IsStatic || type.IsAbstract)
                return null;
            if (type.IsImplicitClass)
                return null;
            if (InheritsAttribute(type))
                return null;

            var constructMethods = new List<IMethodSymbol>();
            foreach (var member in type.GetMembers()) {
                if (member is not IMethodSymbol method)
                    continue;
                if (method.MethodKind != MethodKind.Ordinary || method.IsStatic)
                    continue;
                if (HasInjectAttribute(method, references) == false)
                    continue;

                constructMethods.Add(method);
            }

            IMethodSymbol? construct = null;
            var multipleConstruct = constructMethods.Count > 1;
            if (multipleConstruct == false && constructMethods.Count == 1)
                construct = constructMethods[0];

            IMethodSymbol? constructor = SelectConstructor(type);
            var hasConstruct = construct != null;
            var hasExplicitCtor = constructor != null && constructor.IsImplicitlyDeclared == false;

            if (hasConstruct == false && hasExplicitCtor == false && requireConstructOrExplicitCtor)
                return null;

            var constructorParameters = EquatableArray<ParameterModel>.Empty;
            if (constructor != null && constructor.CanCallFromSameAssembly())
                constructorParameters = ToParameters(constructor);

            var constructParameters = EquatableArray<ParameterModel>.Empty;
            if (construct != null)
                constructParameters = ToParameters(construct);

            var ns = type.ContainingNamespace;
            return new InjectorModel(
                type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                TypeNames.ForCode(type),
                ns == null || ns.IsGlobalNamespace ? "" : ns.ToDisplayString(),
                type.Name,
                type.ContainingType != null,
                type.IsAbstract,
                type.IsGenericType,
                references.UnityObject != null && type.InheritsFrom(references.UnityObject),
                hasConstruct,
                construct != null && construct.CanCallFromSameAssembly(),
                construct != null && construct.Arity > 0,
                multipleConstruct,
                constructor != null,
                constructor != null && constructor.CanCallFromSameAssembly(),
                constructorParameters,
                constructParameters,
                construct != null ? construct.Name : "",
                location);
        }

        private static bool HasInjectAttribute(IMethodSymbol method, ReferenceSymbols references) {
            foreach (var attribute in method.GetAttributes()) {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass == null)
                    continue;
                if (references.InjectAttribute != null &&
                    SymbolEqualityComparer.Default.Equals(attributeClass, references.InjectAttribute))
                    return true;
                if (attributeClass.Name == "InjectAttribute" &&
                    attributeClass.ContainingNamespace?.ToDisplayString() == "Internal")
                    return true;
            }

            return false;
        }

        private static bool InheritsAttribute(INamedTypeSymbol type) {
            var current = type;
            while (current != null) {
                if (current.Name == "Attribute" && current.ContainingNamespace?.ToDisplayString() == "System")
                    return true;

                current = current.BaseType;
            }

            return false;
        }

        private static IMethodSymbol? SelectConstructor(INamedTypeSymbol type) {
            IMethodSymbol? best = null;
            foreach (var ctor in type.InstanceConstructors) {
                if (ctor.IsStatic)
                    continue;
                if (ctor.CanCallFromSameAssembly() == false)
                    continue;

                if (best == null || ctor.Parameters.Length > best.Parameters.Length)
                    best = ctor;
            }

            return best;
        }

        private static EquatableArray<ParameterModel> ToParameters(IMethodSymbol method) {
            if (method.Parameters.Length == 0)
                return EquatableArray<ParameterModel>.Empty;

            var items = new ParameterModel[method.Parameters.Length];
            for (var i = 0; i < method.Parameters.Length; i++) {
                var parameter = method.Parameters[i];
                items[i] = new ParameterModel(TypeNames.ForCode(parameter.Type), parameter.Name);
            }

            return new EquatableArray<ParameterModel>(items);
        }
    }
}
