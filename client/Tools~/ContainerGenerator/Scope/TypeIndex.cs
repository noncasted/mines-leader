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

            return null;
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
