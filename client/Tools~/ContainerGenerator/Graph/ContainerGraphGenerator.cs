using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    [Generator]
    public sealed class ContainerGraphGenerator : IIncrementalGenerator {
        public void Initialize(IncrementalGeneratorInitializationContext context) {
            context.RegisterSourceOutput(context.CompilationProvider, static (production, compilation) => {
                if (ShouldSkip(compilation.AssemblyName))
                    return;

                var references = ReferenceSymbols.Create(compilation);
                if (references == null || references.ScopeBuilder == null && references.EntityBuilder == null && references.Builder == null)
                    return;

                GraphDocument document;
                try {
                    document = new GraphWalker(compilation, references).Walk();
                }
                catch (System.Exception exception) {
                    production.ReportDiagnostic(Diagnostic.Create(
                        GraphDescriptors.UncoveredSyntax,
                        Location.None,
                        "generator exception: " + exception.GetType().Name + " " + exception.Message,
                        compilation.AssemblyName ?? "",
                        "",
                        "0"));
                    return;
                }

                foreach (var diagnostic in document.Diagnostics)
                    production.ReportDiagnostic(diagnostic.ToDiagnostic());

                if (document.Methods.Count == 0)
                    return;

                production.AddSource("ContainerGraph.g.cs", GraphEmitter.Emit(document));
            });
        }

        private static bool ShouldSkip(string? name) {
            if (string.IsNullOrEmpty(name))
                return true;

            var assemblyName = name ?? "";
            if (assemblyName.StartsWith("Unity") ||
                assemblyName.StartsWith("System") ||
                assemblyName.StartsWith("Microsoft") ||
                assemblyName.StartsWith("VContainer") ||
                assemblyName == "mscorlib" ||
                assemblyName == "netstandard" ||
                assemblyName == "ContainerGenerator")
                return true;

            return false;
        }
    }
}
