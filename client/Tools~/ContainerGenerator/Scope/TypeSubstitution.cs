using System.Collections.Generic;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class TypeSubstitution {
        public static string Template(IMethodSymbol method, ITypeSymbol? type) {
            if (type == null)
                return "";
            if (type is ITypeParameterSymbol parameter) {
                var index = IndexOf(method, parameter);
                if (index >= 0)
                    return Placeholder(index);
                return TypeNames.ForCode(type);
            }

            if (type is INamedTypeSymbol named && named.IsGenericType && named.IsUnboundGenericType == false) {
                var args = new string[named.TypeArguments.Length];
                var any = false;
                for (var i = 0; i < named.TypeArguments.Length; i++) {
                    args[i] = Template(method, named.TypeArguments[i]);
                    if (args[i].IndexOf('{') >= 0)
                        any = true;
                }

                if (any == false)
                    return TypeNames.ForCode(named);

                return FillUnbound(TypeNames.ForCode(named.ConstructUnboundGenericType()), args);
            }

            if (type is IArrayTypeSymbol array) {
                var element = Template(method, array.ElementType);
                if (element.IndexOf('{') < 0)
                    return TypeNames.ForCode(type);
                return element + "[]";
            }

            return TypeNames.ForCode(type);
        }

        public static string Map(IMethodSymbol method, ITypeSymbol? type) {
            if (type == null)
                return "";
            if (type is ITypeParameterSymbol parameter) {
                var index = IndexOf(method, parameter);
                return index >= 0 ? "T" + index.ToString(CultureInfo.InvariantCulture) : "";
            }

            if (type is INamedTypeSymbol named && named.IsGenericType && named.IsUnboundGenericType == false) {
                var parts = new List<string>();
                var any = false;
                for (var i = 0; i < named.TypeArguments.Length; i++) {
                    var inner = Map(method, named.TypeArguments[i]);
                    if (string.IsNullOrEmpty(inner)) {
                        parts.Add("*");
                        continue;
                    }

                    if (inner.Length > 1 && inner[0] == 'T')
                        inner = inner.Substring(1);
                    parts.Add(inner);
                    any = true;
                }

                return any ? string.Join(",", parts) : "";
            }

            if (type is IArrayTypeSymbol array)
                return Map(method, array.ElementType);

            return "";
        }

        public static string Apply(string template, string map, IReadOnlyList<string> typeArgs) {
            if (typeArgs == null || typeArgs.Count == 0)
                return template ?? "";
            if (string.IsNullOrEmpty(map) && (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0))
                return template ?? "";

            if (string.IsNullOrEmpty(map) == false && map[0] == 'T') {
                if (int.TryParse(map.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) &&
                    index >= 0 && index < typeArgs.Count)
                    return typeArgs[index];
            }

            if (string.IsNullOrEmpty(template) == false && template.IndexOf('{') >= 0)
                return ReplacePlaceholders(template, typeArgs);

            if (IsUnbound(template) && string.IsNullOrEmpty(map) == false)
                return CloseUnbound(template, map, typeArgs);

            return ReplacePlaceholders(template, typeArgs);
        }

        public static bool IsUnbound(string type) {
            if (string.IsNullOrEmpty(type))
                return false;
            return type.IndexOf("<>", System.StringComparison.Ordinal) >= 0 ||
                   type.IndexOf("<,", System.StringComparison.Ordinal) >= 0;
        }

        public static string CloseUnbound(string unbound, string map, IReadOnlyList<string> typeArgs) {
            if (string.IsNullOrEmpty(unbound) || string.IsNullOrEmpty(map) || typeArgs == null || typeArgs.Count == 0)
                return unbound;

            var parts = map.Split(',');
            var args = new string[parts.Length];
            for (var i = 0; i < parts.Length; i++) {
                var part = parts[i].Trim();
                if (part.Length > 0 && part[0] == 'T')
                    part = part.Substring(1);
                if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) == false ||
                    index < 0 || index >= typeArgs.Count || string.IsNullOrEmpty(typeArgs[index]))
                    return unbound;
                args[i] = typeArgs[index];
            }

            return FillUnbound(unbound, args);
        }

        public static GraphRegistration Instantiate(GraphRegistration open, IReadOnlyList<string> typeArgs) {
            var copy = open.Clone();
            if (typeArgs == null || typeArgs.Count == 0)
                return copy;

            copy.ImplementationType = Apply(open.ImplementationType, open.TypeMap, typeArgs);
            copy.ServiceTypes.Clear();
            for (var i = 0; i < open.ServiceTypes.Count; i++) {
                var map = i < open.ServiceMaps.Count ? open.ServiceMaps[i] : "";
                copy.ServiceTypes.Add(Apply(open.ServiceTypes[i], map, typeArgs));
            }

            copy.ServiceMaps.Clear();
            copy.TypeMap = "";
            return copy;
        }

        public static string ReplacePlaceholders(string template, IReadOnlyList<string> typeArgs) {
            if (string.IsNullOrEmpty(template) || typeArgs == null || typeArgs.Count == 0)
                return template ?? "";

            var result = template;
            for (var i = 0; i < typeArgs.Count; i++) {
                if (string.IsNullOrEmpty(typeArgs[i]))
                    continue;
                result = result.Replace(Placeholder(i), typeArgs[i]);
            }

            return result;
        }

        public static string EncodeTypeArgs(IMethodSymbol? method) {
            if (method == null || method.TypeArguments.Length == 0)
                return "";

            var parts = new string[method.TypeArguments.Length];
            for (var i = 0; i < method.TypeArguments.Length; i++)
                parts[i] = GraphEmitter.Escape(TypeNames.ForCode(method.TypeArguments[i]));
            return string.Join(";", parts);
        }

        public static List<string> DecodeTypeArgs(string encoded) {
            var list = new List<string>();
            if (string.IsNullOrEmpty(encoded))
                return list;

            var parts = encoded.Split(';');
            for (var i = 0; i < parts.Length; i++) {
                var value = GraphEmitter.Unescape(parts[i]);
                if (string.IsNullOrEmpty(value) == false)
                    list.Add(value);
            }

            return list;
        }

        public static string ToUnbound(string template) {
            if (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0)
                return template;
            if (template[0] == '{' && template[template.Length - 1] == '}' && template.IndexOf('<') < 0)
                return "";

            var start = template.IndexOf('<');
            var end = template.LastIndexOf('>');
            if (start < 0 || end <= start)
                return template;

            var inner = template.Substring(start + 1, end - start - 1);
            var arity = 1;
            var depth = 0;
            for (var i = 0; i < inner.Length; i++) {
                if (inner[i] == '<')
                    depth++;
                else if (inner[i] == '>')
                    depth--;
                else if (inner[i] == ',' && depth == 0)
                    arity++;
            }

            var commas = arity <= 1 ? "" : new string(',', arity - 1);
            return template.Substring(0, start + 1) + commas + ">";
        }

        public static int IndexOf(IMethodSymbol method, ITypeParameterSymbol parameter) {
            if (method == null || parameter == null)
                return -1;

            var definition = method.OriginalDefinition ?? method;
            for (var i = 0; i < definition.TypeParameters.Length; i++) {
                if (SymbolEqualityComparer.Default.Equals(definition.TypeParameters[i], parameter))
                    return i;
            }

            if (method.ReducedFrom != null &&
                SymbolEqualityComparer.Default.Equals(method.ReducedFrom, method) == false)
                return IndexOf(method.ReducedFrom, parameter);

            return -1;
        }

        public static string FillUnbound(string unbound, IReadOnlyList<string> args) {
            var start = unbound.IndexOf('<');
            var end = unbound.LastIndexOf('>');
            if (start < 0 || end <= start)
                return unbound;
            return unbound.Substring(0, start + 1) + string.Join(", ", args) + unbound.Substring(end);
        }

        private static string Placeholder(int index) {
            return "{" + index.ToString(CultureInfo.InvariantCulture) + "}";
        }
    }
}
