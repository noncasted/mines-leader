using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class EdgeResolver {
        public static IReadOnlyList<ScopeGraph> Resolve(
            GraphDocument document,
            Compilation compilation,
            ReferenceSymbols references) {
            var graphs = new List<ScopeGraph>();
            if (document == null)
                return graphs;

            for (var i = 0; i < document.Methods.Count; i++) {
                var method = document.Methods[i];
                if (method.IsRoot == false)
                    continue;
                graphs.Add(ResolveRoot(document, method, compilation, references));
            }

            return graphs;
        }

        public static ScopeGraph? ResolveByHint(
            GraphDocument document,
            Compilation compilation,
            ReferenceSymbols references,
            string hint) {
            if (document == null || string.IsNullOrEmpty(hint))
                return null;

            GraphMethod? match = null;
            for (var i = 0; i < document.Methods.Count; i++) {
                var method = document.Methods[i];
                if (method.IsRoot == false)
                    continue;
                if (method.Id.IndexOf(hint, StringComparison.Ordinal) < 0)
                    continue;
                if (match != null)
                    return null;
                match = method;
            }

            if (match == null)
                return null;

            return ResolveRoot(document, match, compilation, references);
        }

        public static ScopeGraph ResolveRoot(
            GraphDocument document,
            GraphMethod root,
            Compilation compilation,
            ReferenceSymbols references) {
            var graph = new ScopeGraph {
                RootId = root.Id,
                Variant = root.Variant,
                ViewType = root.ViewType,
            };
            Flatten(document, root, graph.Registrations);

            var index = new TypeIndex(compilation);
            var implicitTypes = ImplicitTypes(compilation, index);
            var lastByType = new Dictionary<string, int>(StringComparer.Ordinal);
            var allByType = new Dictionary<string, List<int>>(StringComparer.Ordinal);

            for (var i = 0; i < graph.Registrations.Count; i++)
                IndexServices(graph.Registrations[i], i, lastByType, allByType);

            for (var i = 0; i < graph.Registrations.Count; i++)
                Bind(graph, i, index, compilation, references, implicitTypes, lastByType, allByType);

            DetectCycles(graph);
            graph.ConstructionOrder.AddRange(SortTopologically(graph));
            return graph;
        }

        private static void Flatten(
            GraphDocument document,
            GraphMethod root,
            List<GraphRegistration> output) {
            var methods = new Dictionary<string, GraphMethod>(StringComparer.Ordinal);
            for (var i = 0; i < document.Methods.Count; i++)
                methods[document.Methods[i].Id] = document.Methods[i];

            var visited = new HashSet<string>(StringComparer.Ordinal);
            Walk(root);

            void Walk(GraphMethod method) {
                if (visited.Add(method.Id) == false)
                    return;

                var steps = OrderedSteps(method);
                for (var i = 0; i < steps.Count; i++) {
                    var step = steps[i];
                    if (step.Registration != null)
                        output.Add(step.Registration.Clone());
                    if (step.CallId != null && methods.TryGetValue(step.CallId, out var callee))
                        Walk(callee);
                }
            }
        }

        private static List<Step> OrderedSteps(GraphMethod method) {
            var steps = new List<Step>();
            for (var i = 0; i < method.Registrations.Count; i++)
                steps.Add(new Step(method.Registrations[i].Ordinal, method.Registrations[i], null));

            var callCount = method.Calls.Count;
            var ordinalCount = method.CallOrdinals.Count;
            for (var i = 0; i < callCount; i++) {
                var ordinal = i < ordinalCount ? method.CallOrdinals[i] : int.MaxValue - callCount + i;
                steps.Add(new Step(ordinal, null, method.Calls[i]));
            }

            steps.Sort((a, b) => a.Ordinal.CompareTo(b.Ordinal));
            return steps;
        }

        private static void IndexServices(
            GraphRegistration registration,
            int index,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType) {
            var added = false;
            for (var i = 0; i < registration.ServiceTypes.Count; i++) {
                AddService(registration.ServiceTypes[i], index, lastByType, allByType);
                added = true;
            }

            if (added == false && string.IsNullOrEmpty(registration.ImplementationType) == false)
                AddService(registration.ImplementationType, index, lastByType, allByType);
        }

        private static void AddService(
            string type,
            int index,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType) {
            if (string.IsNullOrEmpty(type))
                return;

            lastByType[type] = index;
            if (allByType.TryGetValue(type, out var list) == false) {
                list = new List<int>();
                allByType[type] = list;
            }

            list.Add(index);
        }

        private static void Bind(
            ScopeGraph graph,
            int index,
            TypeIndex types,
            Compilation compilation,
            ReferenceSymbols references,
            HashSet<string> implicitTypes,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType) {
            var registration = graph.Registrations[index];
            if (registration.Origin == "SceneServices" || registration.Origin == "ExternalInstaller")
                return;

            if (registration.Origin == "SwitchFactory") {
                for (var i = 0; i < registration.Arms.Count; i++) {
                    var armType = types.Find(registration.Arms[i].ImplementationType);
                    if (armType == null)
                        continue;
                    BindType(graph, index, armType, types, compilation, references, implicitTypes, lastByType, allByType, true);
                }

                return;
            }

            var implementation = types.Find(registration.ImplementationType);
            if (implementation == null)
                return;

            BindType(
                graph,
                index,
                implementation,
                types,
                compilation,
                references,
                implicitTypes,
                lastByType,
                allByType,
                AllowsUnresolvedHole(registration));
        }

        private static void BindType(
            ScopeGraph graph,
            int ownerIndex,
            INamedTypeSymbol implementation,
            TypeIndex types,
            Compilation compilation,
            ReferenceSymbols references,
            HashSet<string> implicitTypes,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType,
            bool allowsHole) {
            var registration = graph.Registrations[ownerIndex];
            var model = TypeAnalyzer.Analyze(implementation, references, registration.Location, false);
            if (model == null)
                return;

            if (BindsConstructor(registration.Origin))
                BindParameters(
                    graph,
                    ownerIndex,
                    model.ConstructorParameters,
                    "Constructor",
                    types,
                    compilation,
                    implicitTypes,
                    lastByType,
                    allByType,
                    allowsHole);

            BindParameters(
                graph,
                ownerIndex,
                model.ConstructParameters,
                "Construct",
                types,
                compilation,
                implicitTypes,
                lastByType,
                allByType,
                allowsHole);
        }

        private static void BindParameters(
            ScopeGraph graph,
            int ownerIndex,
            EquatableArray<ParameterModel> parameters,
            string source,
            TypeIndex types,
            Compilation compilation,
            HashSet<string> implicitTypes,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType,
            bool allowsHole) {
            var registration = graph.Registrations[ownerIndex];
            for (var i = 0; i < parameters.Count; i++) {
                var parameter = parameters[i];
                var edge = new GraphEdge {
                    ParameterName = parameter.Name,
                    ParameterType = parameter.TypeFullName,
                    Source = source,
                };

                if (lastByType.TryGetValue(parameter.TypeFullName, out var target)) {
                    edge.Kind = "Registration";
                    edge.TargetIndex = target;
                }
                else if (TryGetCollectionElement(parameter.TypeFullName, out var element) &&
                         allByType.TryGetValue(element, out var indices)) {
                    edge.Kind = "Collection";
                    edge.CollectionIndices.AddRange(indices);
                    if (indices.Count > 0)
                        edge.TargetIndex = indices[indices.Count - 1];
                }
                else if (implicitTypes.Contains(parameter.TypeFullName)) {
                    edge.Kind = "Implicit";
                }
                else if (allowsHole || IsExternalHole(parameter.TypeFullName, types, compilation)) {
                    edge.Kind = "Hole";
                }
                else {
                    edge.Kind = "Missing";
                    graph.Diagnostics.Add(Missing(registration, parameter));
                }

                registration.Dependencies.Add(edge);
            }
        }

        private static bool IsExternalHole(string typeFullName, TypeIndex types, Compilation compilation) {
            var symbol = types.Find(typeFullName);
            if (symbol == null)
                return true;
            if (symbol.TypeKind == TypeKind.Interface || symbol.TypeKind == TypeKind.TypeParameter)
                return true;
            if (SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, compilation.Assembly) == false)
                return true;

            return false;
        }

        private static DiagnosticInfo Missing(GraphRegistration registration, ParameterModel parameter) {
            var line = registration.Line.ToString(CultureInfo.InvariantCulture);
            var implementation = string.IsNullOrEmpty(registration.ImplementationType)
                ? registration.Kind
                : registration.ImplementationType;
            return new DiagnosticInfo(
                GraphDescriptors.MissingRegistration,
                registration.Location,
                parameter.Name,
                parameter.TypeFullName,
                implementation,
                registration.File ?? "",
                line);
        }

        private static HashSet<string> ImplicitTypes(Compilation compilation, TypeIndex index) {
            var set = new HashSet<string>(StringComparer.Ordinal);
            AddImplicit(set, index, compilation.GetTypeByMetadataName("Internal.IContainer"));
            AddImplicit(set, index, compilation.GetTypeByMetadataName("Internal.IReadOnlyLifetime"));
            AddImplicit(set, index, compilation.GetTypeByMetadataName("Internal.ILifetime"));
            if (set.Count == 0) {
                set.Add("global::Internal.IContainer");
                set.Add("global::Internal.IReadOnlyLifetime");
                set.Add("global::Internal.ILifetime");
            }

            return set;
        }

        private static void AddImplicit(HashSet<string> set, TypeIndex index, INamedTypeSymbol? type) {
            var formatted = index.Format(type);
            if (string.IsNullOrEmpty(formatted) == false)
                set.Add(formatted);
        }

        private static bool BindsConstructor(string origin) {
            return origin == "ConstructedType" ||
                   origin == "Alternative" ||
                   origin == "ParameterHole" ||
                   origin == "SwitchFactory";
        }

        private static bool AllowsUnresolvedHole(GraphRegistration registration) {
            if (registration.Origin == "ParameterHole")
                return true;
            if (registration.Origin == "SwitchFactory") {
                for (var i = 0; i < registration.Arms.Count; i++) {
                    if (string.IsNullOrEmpty(registration.Arms[i].ParameterExpression) == false)
                        return true;
                }
            }

            return false;
        }

        private static bool TryGetCollectionElement(string typeFullName, out string elementType) {
            elementType = "";
            if (string.IsNullOrEmpty(typeFullName))
                return false;
            if (typeFullName.EndsWith("[]", StringComparison.Ordinal)) {
                elementType = typeFullName.Substring(0, typeFullName.Length - 2);
                return true;
            }

            return TryUnwrap(typeFullName, "global::System.Collections.Generic.IReadOnlyList<", out elementType) ||
                   TryUnwrap(typeFullName, "global::System.Collections.Generic.IEnumerable<", out elementType) ||
                   TryUnwrap(typeFullName, "global::System.Collections.Generic.IReadOnlyCollection<", out elementType);
        }

        private static bool TryUnwrap(string typeFullName, string prefix, out string elementType) {
            elementType = "";
            if (typeFullName.StartsWith(prefix, StringComparison.Ordinal) == false)
                return false;
            if (typeFullName.Length <= prefix.Length || typeFullName[typeFullName.Length - 1] != '>')
                return false;

            elementType = typeFullName.Substring(prefix.Length, typeFullName.Length - prefix.Length - 1);
            return true;
        }

        private static void DetectCycles(ScopeGraph graph) {
            var count = graph.Registrations.Count;
            var state = new int[count];
            var stack = new List<int>();

            for (var i = 0; i < count; i++) {
                if (state[i] == 0)
                    Visit(i);
            }

            void Visit(int slot) {
                state[slot] = 1;
                stack.Add(slot);

                foreach (var dependency in Neighbors(graph.Registrations[slot])) {
                    if (dependency < 0 || dependency >= count)
                        continue;

                    if (state[dependency] == 1) {
                        graph.Diagnostics.Add(Cycle(graph, stack, dependency));
                        continue;
                    }

                    if (state[dependency] == 0)
                        Visit(dependency);
                }

                stack.RemoveAt(stack.Count - 1);
                state[slot] = 2;
            }
        }

        private static DiagnosticInfo Cycle(ScopeGraph graph, List<int> stack, int start) {
            var startIndex = 0;
            for (var i = 0; i < stack.Count; i++) {
                if (stack[i] == start) {
                    startIndex = i;
                    break;
                }
            }

            var names = new List<string>();
            for (var i = startIndex; i < stack.Count; i++)
                names.Add(Display(graph.Registrations[stack[i]]));
            names.Add(Display(graph.Registrations[start]));

            var path = string.Join(" -> ", names);
            var location = graph.Registrations[start].Location;
            return new DiagnosticInfo(GraphDescriptors.CircularDependency, location, path);
        }

        private static string Display(GraphRegistration registration) {
            if (string.IsNullOrEmpty(registration.ImplementationType) == false)
                return registration.ImplementationType;
            if (registration.Arms.Count > 0 && string.IsNullOrEmpty(registration.Arms[0].ImplementationType) == false)
                return registration.Arms[0].ImplementationType;
            return registration.Kind;
        }

        private static List<int> SortTopologically(ScopeGraph graph) {
            var count = graph.Registrations.Count;
            var order = new List<int>(count);
            var visited = new bool[count];

            for (var i = 0; i < count; i++)
                Visit(i);

            return order;

            void Visit(int slot) {
                if (visited[slot])
                    return;

                visited[slot] = true;
                foreach (var dependency in Neighbors(graph.Registrations[slot])) {
                    if (dependency >= 0 && dependency < count && dependency != slot)
                        Visit(dependency);
                }

                order.Add(slot);
            }
        }

        private static IEnumerable<int> Neighbors(GraphRegistration registration) {
            for (var i = 0; i < registration.Dependencies.Count; i++) {
                var edge = registration.Dependencies[i];
                if (edge.Kind == "Collection") {
                    for (var c = 0; c < edge.CollectionIndices.Count; c++)
                        yield return edge.CollectionIndices[c];
                    continue;
                }

                if (edge.Kind == "Registration")
                    yield return edge.TargetIndex;
            }
        }

        private readonly struct Step {
            public readonly int Ordinal;
            public readonly GraphRegistration? Registration;
            public readonly string? CallId;

            public Step(int ordinal, GraphRegistration? registration, string? callId) {
                Ordinal = ordinal;
                Registration = registration;
                CallId = callId;
            }
        }
    }
}
