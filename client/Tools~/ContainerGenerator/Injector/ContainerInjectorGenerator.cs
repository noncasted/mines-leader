using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    [Generator]
    public sealed class ContainerInjectorGenerator : IIncrementalGenerator {
        public const string EmitStepName = "ContainerInjectorEmit";

        public void Initialize(IncrementalGeneratorInitializationContext context) {
            var assembly = context.CompilationProvider.Select(static (compilation, _) => CreateAssemblyContext(compilation));

            var fromTypes = context.SyntaxProvider
                                   .CreateSyntaxProvider(
                                       static (node, _) => TypeAnalyzer.IsClassCandidate(node),
                                       static (ctx, cancellation) => TransformType(ctx, cancellation))
                                   .Where(static model => model != null)
                                   .Select(static (model, _) => model!);

            var fromRegister = context.SyntaxProvider
                                      .CreateSyntaxProvider(
                                          static (node, _) => TypeAnalyzer.IsRegisterCandidate(node),
                                          static (ctx, cancellation) => TransformRegister(ctx, cancellation))
                                      .SelectMany(static (models, _) => models);

            var models = fromTypes.Collect().Combine(fromRegister.Collect())
                                  .Select(static (pair, _) => Merge(pair.Left, pair.Right));

            var pipeline = models.Combine(assembly).Select(static (pair, _) => {
                if (pair.Right.ShouldProcess == false)
                    return new EmitBatch(pair.Right, EquatableArray<GeneratedInjectorSource>.Empty, null);

                return BuildBatch(pair.Left, pair.Right);
            }).WithTrackingName(EmitStepName);

            context.RegisterSourceOutput(pipeline, static (ctx, batch) => {
                if (batch.Assembly.ShouldProcess == false)
                    return;

                var seen = new HashSet<string>();
                var registered = new List<InjectorModel>();
                for (var i = 0; i < batch.Sources.Count; i++) {
                    var generated = batch.Sources[i];
                    if (seen.Add(generated.HintName) == false)
                        continue;

                    for (var d = 0; d < generated.Diagnostics.Count; d++)
                        ctx.ReportDiagnostic(generated.Diagnostics[d].ToDiagnostic());

                    if (generated.Source == null)
                        continue;

                    ctx.AddSource(generated.HintName, generated.Source);
                    registered.Add(generated.Model);
                }

                if (batch.Registry != null)
                    ctx.AddSource("ContainerInjectors_" + TypeNames.SafeAssembly(batch.Assembly.AssemblyName) + ".g.cs", batch.Registry);
            });
        }

        private static AssemblyEmitContext CreateAssemblyContext(Compilation compilation) {
            var name = compilation.AssemblyName ?? "Assembly";
            if (ShouldSkipAssembly(name))
                return new AssemblyEmitContext(name, false, false);

            var references = ReferenceSymbols.Create(compilation);
            if (references == null)
                return new AssemblyEmitContext(name, false, false);

            return new AssemblyEmitContext(name, true, references.RuntimeInitialize != null);
        }

        private static bool ShouldSkipAssembly(string name) {
            if (name.StartsWith("Unity") ||
                name.StartsWith("System") ||
                name.StartsWith("Microsoft") ||
                name.StartsWith("VContainer") ||
                name.StartsWith("Mono.") ||
                name == "mscorlib" ||
                name == "netstandard" ||
                name == "ContainerGenerator")
                return true;

            return false;
        }

        private static InjectorModel? TransformType(GeneratorSyntaxContext context, CancellationToken cancellation) {
            cancellation.ThrowIfCancellationRequested();
            var references = ReferenceSymbols.Create(context.SemanticModel.Compilation);
            if (references == null)
                return null;

            var declaration = (ClassDeclarationSyntax)context.Node;
            if (context.SemanticModel.GetDeclaredSymbol(declaration, cancellation) is not INamedTypeSymbol type)
                return null;

            if (SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, context.SemanticModel.Compilation.Assembly) == false)
                return null;

            var syntaxRef = type.DeclaringSyntaxReferences;
            if (syntaxRef.Length > 0) {
                var first = syntaxRef[0];
                if (first.SyntaxTree != declaration.SyntaxTree || first.Span != declaration.Span)
                    return null;
            }

            return TypeAnalyzer.Analyze(type, references, LocationInfo.CreateFrom(declaration.Identifier.GetLocation()));
        }

        private static EquatableArray<InjectorModel> TransformRegister(
            GeneratorSyntaxContext context,
            CancellationToken cancellation) {
            cancellation.ThrowIfCancellationRequested();
            var references = ReferenceSymbols.Create(context.SemanticModel.Compilation);
            if (references == null)
                return EquatableArray<InjectorModel>.Empty;

            var invocation = (InvocationExpressionSyntax)context.Node;
            if (context.SemanticModel.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol method)
                return EquatableArray<InjectorModel>.Empty;

            if (method.OriginalDefinition.Name != "Register" &&
                method.OriginalDefinition.Name != "RegisterInstance" &&
                method.OriginalDefinition.Name != "RegisterComponent")
                return EquatableArray<InjectorModel>.Empty;

            var models = new List<InjectorModel>();
            var location = LocationInfo.CreateFrom(invocation.GetLocation());
            foreach (var argument in method.TypeArguments)
                TryAdd(models, argument, context.SemanticModel.Compilation.Assembly, references, location);

            if (method.TypeArguments.Length == 0 && invocation.ArgumentList.Arguments.Count > 0) {
                var argumentType = context.SemanticModel.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression, cancellation).Type;
                TryAdd(models, argumentType, context.SemanticModel.Compilation.Assembly, references, location);
            }

            return models.Count == 0
                ? EquatableArray<InjectorModel>.Empty
                : new EquatableArray<InjectorModel>(models.ToArray());
        }

        private static void TryAdd(
            List<InjectorModel> models,
            ITypeSymbol? argument,
            IAssemblySymbol assembly,
            ReferenceSymbols references,
            LocationInfo? location) {
            if (argument is not INamedTypeSymbol type)
                return;
            if (SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, assembly) == false)
                return;

            var model = TypeAnalyzer.Analyze(type, references, location, requireConstructOrExplicitCtor: false);
            if (model != null)
                models.Add(model);
        }

        private static EquatableArray<InjectorModel> Merge(
            ImmutableArray<InjectorModel> left,
            ImmutableArray<InjectorModel> right) {
            var map = new Dictionary<string, InjectorModel>();
            if (left.IsDefaultOrEmpty == false)
                Add(map, left);
            if (right.IsDefaultOrEmpty == false)
                Add(map, right);
            if (map.Count == 0)
                return EquatableArray<InjectorModel>.Empty;

            var items = new InjectorModel[map.Count];
            map.Values.CopyTo(items, 0);
            System.Array.Sort(items, (a, b) => string.CompareOrdinal(a.FullTypeName, b.FullTypeName));
            return new EquatableArray<InjectorModel>(items);
        }

        private static void Add(Dictionary<string, InjectorModel> map, ImmutableArray<InjectorModel> models) {
            foreach (var model in models) {
                if (model == null)
                    continue;
                if (map.ContainsKey(model.FullTypeName) == false)
                    map.Add(model.FullTypeName, model);
            }
        }

        private static EmitBatch BuildBatch(EquatableArray<InjectorModel> models, AssemblyEmitContext assembly) {
            var sources = new GeneratedInjectorSource[models.Count];
            var registered = new List<InjectorModel>(models.Count);
            for (var i = 0; i < models.Count; i++) {
                var generated = InjectorEmitter.Emit(models[i]);
                sources[i] = generated;
                if (generated.Source != null)
                    registered.Add(generated.Model);
            }

            string? registry = null;
            if (registered.Count > 0)
                registry = InjectorEmitter.EmitRegistry(assembly.AssemblyName, assembly.HasUnityInit, registered);

            return new EmitBatch(assembly, new EquatableArray<GeneratedInjectorSource>(sources), registry);
        }

        private sealed class EmitBatch : System.IEquatable<EmitBatch> {
            public AssemblyEmitContext Assembly { get; }
            public EquatableArray<GeneratedInjectorSource> Sources { get; }
            public string? Registry { get; }

            public EmitBatch(AssemblyEmitContext assembly, EquatableArray<GeneratedInjectorSource> sources, string? registry) {
                Assembly = assembly;
                Sources = sources;
                Registry = registry;
            }

            public bool Equals(EmitBatch? other) {
                return other != null &&
                       Assembly.Equals(other.Assembly) &&
                       Sources.Equals(other.Sources) &&
                       Registry == other.Registry;
            }

            public override bool Equals(object? obj) {
                return Equals(obj as EmitBatch);
            }

            public override int GetHashCode() {
                return HashCodes.Combine(Assembly.GetHashCode(), Sources.GetHashCode(), HashCodes.Of(Registry));
            }
        }
    }
}
