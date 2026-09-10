using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal sealed class TypeIndex {
        private readonly Compilation _compilation;
        private readonly Dictionary<string, INamedTypeSymbol> _byFullyQualified;

        public TypeIndex(Compilation compilation) {
            _compilation = compilation;
            _byFullyQualified = new Dictionary<string, INamedTypeSymbol>();
        }

        public INamedTypeSymbol? Find(string fullyQualified) {
            if (string.IsNullOrEmpty(fullyQualified))
                return null;
            if (_byFullyQualified.TryGetValue(fullyQualified, out var cached))
                return cached;

            var symbol = Lookup(fullyQualified);
            if (symbol != null)
                _byFullyQualified[fullyQualified] = symbol;

            return symbol;
        }

        public string Format(ITypeSymbol? type) {
            if (type == null)
                return "";
            return TypeNames.ForCode(type);
        }

        private INamedTypeSymbol? Lookup(string fullyQualified) {
            var metadata = StripGlobal(fullyQualified);
            var direct = _compilation.GetTypeByMetadataName(metadata);
            if (direct != null)
                return direct;

            var nested = TryNested(metadata);
            if (nested != null)
                return nested;

            EnsureIndexed();
            if (_byFullyQualified.TryGetValue(fullyQualified, out var indexed))
                return indexed;

            return TryGeneric(fullyQualified);
        }

        private INamedTypeSymbol? TryGeneric(string fullyQualified) {
            if (TrySplitGeneric(fullyQualified, out var name, out var args) == false)
                return null;

            var definition = FindDefinition(name, args.Count == 0 ? 1 : args.Count);
            if (definition == null)
                return null;
            definition = definition.OriginalDefinition;

            if (args.Count == 0 || AllEmpty(args)) {
                if (definition.IsUnboundGenericType)
                    return definition;
                return definition.IsGenericType ? definition.ConstructUnboundGenericType() : definition;
            }

            if (definition.TypeParameters.Length != args.Count)
                return null;

            var typeArgs = new ITypeSymbol[args.Count];
            for (var i = 0; i < args.Count; i++) {
                var arg = Find(args[i].Trim());
                if (arg == null)
                    return null;
                typeArgs[i] = arg;
            }

            return definition.Construct(typeArgs);
        }

        private INamedTypeSymbol? FindDefinition(string name, int arity) {
            var metadata = StripGlobal(name);
            if (arity > 0)
                metadata = metadata + "`" + arity.ToString();

            var direct = _compilation.GetTypeByMetadataName(metadata);
            if (direct != null)
                return direct;

            var nested = TryNested(metadata);
            if (nested != null)
                return nested;

            EnsureIndexed();
            var unbound = name + "<" + (arity <= 1 ? "" : new string(',', arity - 1)) + ">";
            if (_byFullyQualified.TryGetValue(unbound, out var indexed))
                return indexed;
            if (_byFullyQualified.TryGetValue(name, out indexed))
                return indexed;
            return null;
        }

        private static bool TrySplitGeneric(string type, out string name, out List<string> args) {
            name = type;
            args = new List<string>();
            var start = IndexOfTopLevel(type, '<');
            if (start < 0)
                return false;
            if (type.Length == 0 || type[type.Length - 1] != '>')
                return false;

            name = type.Substring(0, start);
            var inner = type.Substring(start + 1, type.Length - start - 2);
            if (string.IsNullOrEmpty(inner))
                return true;

            args = SplitTopLevel(inner, ',');
            return true;
        }

        private static int IndexOfTopLevel(string value, char token) {
            var depth = 0;
            for (var i = 0; i < value.Length; i++) {
                if (value[i] == '<') {
                    if (token == '<' && depth == 0)
                        return i;
                    depth++;
                    continue;
                }

                if (value[i] == '>')
                    depth--;
            }

            return -1;
        }

        private static List<string> SplitTopLevel(string value, char separator) {
            var parts = new List<string>();
            var depth = 0;
            var start = 0;
            for (var i = 0; i < value.Length; i++) {
                if (value[i] == '<')
                    depth++;
                else if (value[i] == '>')
                    depth--;
                else if (value[i] == separator && depth == 0) {
                    parts.Add(value.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }

            parts.Add(value.Substring(start).Trim());
            return parts;
        }

        private static bool AllEmpty(List<string> args) {
            for (var i = 0; i < args.Count; i++) {
                if (string.IsNullOrEmpty(args[i]) == false)
                    return false;
            }

            return true;
        }

        private INamedTypeSymbol? TryNested(string metadata) {
            var parts = metadata.Split('.');
            if (parts.Length < 2)
                return null;

            for (var split = parts.Length - 1; split >= 1; split--) {
                var candidate = Join('.', parts, 0, split) + "+" + Join('+', parts, split, parts.Length - split);
                var symbol = _compilation.GetTypeByMetadataName(candidate);
                if (symbol != null)
                    return symbol;
            }

            return null;
        }

        private void EnsureIndexed() {
            if (_indexed)
                return;

            _indexed = true;
            IndexAssembly(_compilation.Assembly);
            foreach (var reference in _compilation.SourceModule.ReferencedAssemblySymbols)
                IndexAssembly(reference);
        }

        private void IndexAssembly(IAssemblySymbol assembly) {
            foreach (var type in assembly.GlobalNamespace.GetAllTypes()) {
                var key = TypeNames.ForCode(type);
                if (_byFullyQualified.ContainsKey(key) == false)
                    _byFullyQualified.Add(key, type);
            }
        }

        private static string StripGlobal(string value) {
            const string prefix = "global::";
            if (value.StartsWith(prefix, System.StringComparison.Ordinal))
                return value.Substring(prefix.Length);
            return value;
        }

        private static string Join(char separator, string[] parts, int start, int count) {
            var result = parts[start];
            for (var i = 1; i < count; i++)
                result += separator + parts[start + i];
            return result;
        }

        private bool _indexed;
    }
}
