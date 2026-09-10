using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class TypeNames {
        public static string ForCode(ITypeSymbol type) {
            return type.WithNullableAnnotation(NullableAnnotation.None)
                       .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        public static string ForMetadata(ITypeSymbol type) {
            return type.WithNullableAnnotation(NullableAnnotation.None)
                       .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                       .Replace("global::", "");
        }

        public static string SafeHint(string fullTypeName) {
            return fullTypeName
                   .Replace("global::", "")
                   .Replace(".", "_")
                   .Replace("<", "_")
                   .Replace(">", "_")
                   .Replace(",", "_")
                   .Replace(" ", "")
                   .Replace("+", "_");
        }

        public static string SafeAssembly(string? assemblyName) {
            if (string.IsNullOrEmpty(assemblyName))
                return "Assembly";

            var value = assemblyName ?? "Assembly";
            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++) {
                if (char.IsLetterOrDigit(chars[i]) == false)
                    chars[i] = '_';
            }

            if (char.IsDigit(chars[0]))
                return "_" + new string(chars);

            return new string(chars);
        }
    }
}
