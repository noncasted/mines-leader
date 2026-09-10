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

            BindParents(document);
            for (var i = 0; i < document.Methods.Count; i++) {
                var method = document.Methods[i];
                if (method.IsRoot == false)
                    continue;
                if (IsLocalRoot(method, compilation) == false)
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
                if (IsLocalRoot(method, compilation) == false)
                    continue;
                if (method.Id.IndexOf(hint, StringComparison.Ordinal) < 0)
                    continue;
                if (match != null)
                    return null;
                match = method;
            }

            if (match == null)
                return null;

            BindParents(document);
            return ResolveRoot(document, match, compilation, references);
        }

        public static void BindParents(GraphDocument document) {
            if (document == null)
                return;

            for (var i = 0; i < document.Methods.Count; i++) {
                var method = document.Methods[i];
                var hint = method.ParentHint;
                if (string.IsNullOrEmpty(hint))
                    hint = method.ParentId;
                if (string.IsNullOrEmpty(hint))
                    continue;

                var id = MatchParent(document, hint);
                if (string.IsNullOrEmpty(id)) {
                    method.ParentUnresolved = true;
                    // Корень из манифеста чужой сборки: его родителя проверяет сборка-владелец.
                    if (string.IsNullOrEmpty(method.AssemblyName) == false &&
                        string.Equals(method.AssemblyName, document.AssemblyName, StringComparison.Ordinal) == false)
                        continue;
                    document.Diagnostics.Add(MissingParentManifest(
                        hint,
                        method.ParentAssembly,
                        method.Id));
                    continue;
                }

                method.ParentId = id;
            }
        }

        public static string MatchParent(GraphDocument document, string hint) {
            if (document == null || string.IsNullOrEmpty(hint))
                return "";

            for (var i = 0; i < document.Methods.Count; i++) {
                if (document.Methods[i].Id == hint)
                    return document.Methods[i].Id;
            }

            return "";
        }

        public static ScopeGraph ResolveRoot(
            GraphDocument document,
            GraphMethod root,
            Compilation compilation,
            ReferenceSymbols references) {
            var graph = new ScopeGraph {
                RootId = root.Id,
                ParentId = root.ParentId,
                Variant = root.Variant,
                ViewType = root.ViewType,
            };
            Flatten(document, root, graph.Registrations);
            graph.ParentMissing = root.ParentUnresolved;

            var index = new TypeIndex(compilation);
            var implicitTypes = ImplicitTypes(compilation, index);
            EnsureLoaderServices(graph, compilation, index);
            var lastByType = new Dictionary<string, int>(StringComparer.Ordinal);
            var allByType = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var parentExports = CollectParentExports(document, root, graph);

            for (var i = 0; i < graph.Registrations.Count; i++)
                IndexServices(graph.Registrations[i], i, lastByType, allByType);

            for (var i = 0; i < graph.Registrations.Count; i++)
                Bind(graph, i, index, compilation, references, implicitTypes, lastByType, allByType, parentExports);

            DetectCycles(graph);
            graph.ConstructionOrder.AddRange(SortTopologically(graph));
            return graph;
        }

        private static bool IsLocalRoot(GraphMethod method, Compilation compilation) {
            if (method == null || compilation == null)
                return false;
            if (string.IsNullOrEmpty(method.AssemblyName) == false &&
                string.Equals(method.AssemblyName, compilation.AssemblyName, StringComparison.Ordinal) == false)
                return false;
            return true;
        }

        private static void Flatten(
            GraphDocument document,
            GraphMethod root,
            List<GraphRegistration> output) {
            var methods = new Dictionary<string, GraphMethod>(StringComparer.Ordinal);
            for (var i = 0; i < document.Methods.Count; i++)
                methods[document.Methods[i].Id] = document.Methods[i];

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var stack = new HashSet<string>(StringComparer.Ordinal);
            Walk(root, new List<string>(), "", "");

            void Walk(GraphMethod method, List<string> typeArgs, string asServices, string hole) {
                var substitution = typeArgs ?? new List<string>();
                var generic = substitution.Count > 0;
                if (stack.Contains(method.Id))
                    return;
                if (generic == false && visited.Add(method.Id) == false)
                    return;

                stack.Add(method.Id);
                GraphRegistration? returned = null;
                var steps = OrderedSteps(method);
                for (var i = 0; i < steps.Count; i++) {
                    var step = steps[i];
                    if (step.Registration != null) {
                        var clone = TypeSubstitution.Instantiate(step.Registration, substitution);
                        if (method.ReturnedOrdinal == step.Registration.Ordinal)
                            returned = clone;
                        output.Add(clone);
                    }

                    if (step.CallId != null && methods.TryGetValue(step.CallId, out var callee)) {
                        var callArgs = TypeSubstitution.DecodeTypeArgs(method.CallTypesAt(step.CallIndex));
                        if (substitution.Count > 0) {
                            for (var a = 0; a < callArgs.Count; a++)
                                callArgs[a] = TypeSubstitution.ReplacePlaceholders(callArgs[a], substitution);
                        }

                        Walk(
                            callee,
                            callArgs,
                            TypeSubstitution.ReplacePlaceholders(method.CallAsAt(step.CallIndex), substitution),
                            TypeSubstitution.ReplacePlaceholders(method.CallHoleAt(step.CallIndex), substitution));
                    }
                }

                if (returned != null) {
                    ApplyCallAs(returned, asServices);
                    if (string.IsNullOrEmpty(hole) == false) {
                        returned.Hole = string.IsNullOrEmpty(returned.Hole) ? hole : returned.Hole + "; " + hole;
                        if (returned.Arms.Count == 0 && returned.Origin == "ConstructedType")
                            returned.Origin = "ParameterHole";
                    }
                }

                stack.Remove(method.Id);
            }
        }

        private static void ApplyCallAs(GraphRegistration registration, string asServices) {
            if (string.IsNullOrEmpty(asServices))
                return;

            var parts = asServices.Split(';');
            for (var i = 0; i < parts.Length; i++) {
                var service = parts[i].Trim();
                if (string.IsNullOrEmpty(service))
                    continue;
                if (registration.ServiceTypes.Contains(service))
                    continue;
                registration.ServiceTypes.Add(service);
            }
        }

        private static List<Step> OrderedSteps(GraphMethod method) {
            var steps = new List<Step>();
            for (var i = 0; i < method.Registrations.Count; i++)
                steps.Add(new Step(method.Registrations[i].Ordinal, steps.Count, method.Registrations[i], null, -1));

            method.PadCallLists();
            var callCount = method.Calls.Count;
            var ordinalCount = method.CallOrdinals.Count;
            for (var i = 0; i < callCount; i++) {
                var ordinal = i < ordinalCount ? method.CallOrdinals[i] : int.MaxValue - callCount + i;
                steps.Add(new Step(ordinal, steps.Count, null, method.Calls[i], i));
            }

            // Стабильно: сервисы сцены делят ординал вызова сцены и идут в порядке SceneServicesFactory.
            steps.Sort((a, b) => a.Ordinal != b.Ordinal
                ? a.Ordinal.CompareTo(b.Ordinal)
                : a.Sequence.CompareTo(b.Sequence));
            return steps;
        }

        private static void IndexServices(
            GraphRegistration registration,
            int index,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType) {
            // Injectable не сервис: его никто не резолвит, скоуп только инжектит готовый экземпляр.
            if (registration.Origin == "Injectable")
                return;

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

        private static Dictionary<string, string> CollectParentExports(
            GraphDocument document,
            GraphMethod root,
            ScopeGraph graph) {
            var exports = new Dictionary<string, string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = root.ParentId;
            while (string.IsNullOrEmpty(current) == false && visited.Add(current)) {
                GraphMethod? method = FindMethod(document, current);
                if (method == null) {
                    graph.ParentMissing = true;
                    graph.Diagnostics.Add(MissingParentManifest(
                        current,
                        ParentAssemblyOf(document, root, current),
                        root.Id));
                    return exports;
                }

                var registrations = new List<GraphRegistration>();
                Flatten(document, method, registrations);
                for (var i = 0; i < registrations.Count; i++)
                    IndexParentServices(registrations[i], current, exports);

                current = string.IsNullOrEmpty(method.ParentId) ? method.ParentHint : method.ParentId;
            }

            return exports;
        }

        private static GraphMethod? FindMethod(GraphDocument document, string id) {
            for (var i = 0; i < document.Methods.Count; i++) {
                if (document.Methods[i].Id == id)
                    return document.Methods[i];
            }

            return null;
        }

        private static string ParentAssemblyOf(GraphDocument document, GraphMethod root, string parentId) {
            if (string.IsNullOrEmpty(root.ParentAssembly) == false &&
                (root.ParentId == parentId || root.ParentHint == parentId))
                return root.ParentAssembly;

            var method = FindMethod(document, parentId);
            if (method != null && string.IsNullOrEmpty(method.AssemblyName) == false)
                return method.AssemblyName;
            return root.ParentAssembly;
        }

        private static DiagnosticInfo MissingParentManifest(string parentId, string assembly, string declaredOn) {
            return new DiagnosticInfo(
                GraphDescriptors.UnresolvedParent,
                null,
                parentId ?? "",
                string.IsNullOrEmpty(assembly) ? "" : assembly,
                declaredOn ?? "");
        }

        private static void EnsureLoaderServices(ScopeGraph graph, Compilation compilation, TypeIndex types) {
            for (var i = 0; i < LoaderServices.Implementations.Length; i++) {
                if (LoaderServices.Implementations[i] == LoaderServices.EventLoopImplementation) {
                    EnsureEventLoopHole(graph, compilation, types);
                    continue;
                }

                var implementation = LoaderServices.Find(compilation, LoaderServices.Implementations[i]);
                if (implementation == null)
                    continue;

                var implementationName = types.Format(implementation);
                if (string.IsNullOrEmpty(implementationName) || HasService(graph, implementationName))
                    continue;

                var registration = new GraphRegistration {
                    Kind = "Register",
                    ImplementationType = implementationName,
                    Lifetime = "Scoped",
                    Origin = LoaderServices.Origin,
                    Source = "Loader",
                };
                var serviceNames = LoaderServices.Services[i];
                for (var s = 0; s < serviceNames.Length; s++) {
                    var service = LoaderServices.Find(compilation, serviceNames[s]);
                    var formatted = types.Format(service);
                    if (string.IsNullOrEmpty(formatted) == false)
                        registration.ServiceTypes.Add(formatted);
                }

                if (HasAnyService(graph, registration))
                    continue;
                graph.Registrations.Add(registration);
            }
        }

        // IEventLoop — тот самый builder.Events загрузчика: на нём висят AddBeforeBuild/AddBeforeDispose
        // и по нему загрузчик гонит фазы. Свой экземпляр в классе скоупа был бы другим циклом.
        private static void EnsureEventLoopHole(ScopeGraph graph, Compilation compilation, TypeIndex types) {
            var service = types.Format(LoaderServices.Find(compilation, LoaderServices.EventLoopService));
            if (string.IsNullOrEmpty(service) || HasService(graph, service))
                return;

            var registration = new GraphRegistration {
                Kind = "RegisterInstance",
                ImplementationType = service,
                Lifetime = "Singleton",
                Origin = "InstanceHole",
                Source = "Loader",
                Hole = "builder.Events",
            };
            registration.ServiceTypes.Add(service);
            graph.Registrations.Add(registration);
        }

        private static bool HasService(ScopeGraph graph, string type) {
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                if (registration.ImplementationType == type)
                    return true;
                for (var s = 0; s < registration.ServiceTypes.Count; s++) {
                    if (registration.ServiceTypes[s] == type)
                        return true;
                }
            }

            return false;
        }

        private static bool HasAnyService(ScopeGraph graph, GraphRegistration candidate) {
            if (HasService(graph, candidate.ImplementationType))
                return true;
            for (var i = 0; i < candidate.ServiceTypes.Count; i++) {
                if (HasService(graph, candidate.ServiceTypes[i]))
                    return true;
            }

            return false;
        }

        private static void IndexParentServices(
            GraphRegistration registration,
            string parentId,
            Dictionary<string, string> exports) {
            if (registration.Origin == "Injectable")
                return;

            var added = false;
            for (var i = 0; i < registration.ServiceTypes.Count; i++) {
                if (string.IsNullOrEmpty(registration.ServiceTypes[i]))
                    continue;
                if (exports.ContainsKey(registration.ServiceTypes[i]) == false)
                    exports.Add(registration.ServiceTypes[i], parentId);
                added = true;
            }

            if (added == false && string.IsNullOrEmpty(registration.ImplementationType) == false) {
                if (exports.ContainsKey(registration.ImplementationType) == false)
                    exports.Add(registration.ImplementationType, parentId);
            }
        }

        private static void Bind(
            ScopeGraph graph,
            int index,
            TypeIndex types,
            Compilation compilation,
            ReferenceSymbols references,
            HashSet<string> implicitTypes,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType,
            Dictionary<string, string> parentExports) {
            var registration = graph.Registrations[index];
            if (registration.Origin == "SceneServices" || registration.Origin == "ExternalInstaller")
                return;

            if (registration.Origin == "SwitchFactory") {
                for (var i = 0; i < registration.Arms.Count; i++) {
                    var armType = types.Find(registration.Arms[i].ImplementationType);
                    if (armType == null) {
                        if (string.IsNullOrEmpty(registration.Arms[i].ImplementationType) == false)
                            graph.Diagnostics.Add(MissingType(registration, registration.Arms[i].ImplementationType));
                        continue;
                    }

                    BindType(
                        graph,
                        index,
                        armType,
                        types,
                        compilation,
                        references,
                        implicitTypes,
                        lastByType,
                        allByType,
                        parentExports,
                        false,
                        i,
                        registration.Arms[i].ParameterType);
                }

                BindDiscriminant(graph, registration, lastByType, parentExports);
                return;
            }

            if (string.IsNullOrEmpty(registration.ImplementationType)) {
                // Регистрация без типа — вызов не связался (ошибка в исходнике): класс с пустым типом не эмитится.
                if (registration.Origin != LoaderServices.Origin)
                    graph.Diagnostics.Add(MissingType(registration, "(unbound " + registration.Kind + ")"));
                return;
            }

            var implementation = types.Find(registration.ImplementationType);
            if (implementation == null) {
                graph.Diagnostics.Add(MissingType(registration, registration.ImplementationType));
                return;
            }

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
                parentExports,
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
            Dictionary<string, string> parentExports,
            bool allowsHole,
            int arm = -1,
            string armParameterType = "") {
            var registration = graph.Registrations[ownerIndex];
            var model = TypeAnalyzer.Analyze(implementation, references, registration.Location, false);
            if (registration.Origin == "Injectable" && (model == null || string.IsNullOrEmpty(model.ConstructName)))
                graph.Diagnostics.Add(NoInjectMethod(registration));
            if (model == null)
                return;

            // У [Inject] без параметров нет рёбер: имя метода нужно эмиттеру отдельно.
            if (arm < 0)
                registration.InjectMethod = model.ConstructName;

            if (BindsConstructor(registration.Origin))
                BindParameters(
                    graph,
                    ownerIndex,
                    model.ConstructorParameters,
                    "Constructor",
                    types,
                    implicitTypes,
                    lastByType,
                    allByType,
                    parentExports,
                    allowsHole,
                    arm,
                    armParameterType);

            BindParameters(
                graph,
                ownerIndex,
                model.ConstructParameters,
                "Construct",
                types,
                implicitTypes,
                lastByType,
                allByType,
                parentExports,
                allowsHole,
                arm,
                armParameterType,
                model.ConstructName);
        }

        // switch по enum строит ветку по значению, зарегистрированному в скоупе (RegisterInstance(definition.Type)).
        private static void BindDiscriminant(
            ScopeGraph graph,
            GraphRegistration registration,
            Dictionary<string, int> lastByType,
            Dictionary<string, string> parentExports) {
            var type = ScopePlan.InferDiscriminantType(registration);
            var edge = new GraphEdge {
                ParameterName = "discriminant",
                ParameterType = type,
                Source = "Discriminant",
            };

            if (lastByType.TryGetValue(type, out var target)) {
                edge.Kind = "Registration";
                edge.TargetIndex = target;
            }
            else if (parentExports.TryGetValue(type, out var parentId)) {
                edge.Kind = "Hole";
                edge.ParentRootId = parentId;
            }
            else {
                edge.Kind = "Missing";
                graph.Diagnostics.Add(Missing(registration, new ParameterModel(type, "discriminant")));
            }

            registration.Dependencies.Add(edge);
        }

        private static void BindParameters(
            ScopeGraph graph,
            int ownerIndex,
            EquatableArray<ParameterModel> parameters,
            string source,
            TypeIndex types,
            HashSet<string> implicitTypes,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType,
            Dictionary<string, string> parentExports,
            bool allowsHole,
            int arm,
            string armParameterType,
            string method = "") {
            var registration = graph.Registrations[ownerIndex];
            for (var i = 0; i < parameters.Count; i++) {
                var parameter = parameters[i];
                var edge = new GraphEdge {
                    ParameterName = parameter.Name,
                    ParameterType = parameter.TypeFullName,
                    Source = source,
                    Method = method,
                    Arm = arm,
                };

                if (string.IsNullOrEmpty(armParameterType) == false && parameter.TypeFullName == armParameterType) {
                    // WithParameter своей ветки: в рантайме в билдере лежит только значение выбранной ветки.
                    edge.Kind = "ArmParameter";
                }
                else if (lastByType.TryGetValue(parameter.TypeFullName, out var target)) {
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
                else if (TryGetCollectionElement(parameter.TypeFullName, out _) &&
                         parentExports.ContainsKey(parameter.TypeFullName) == false) {
                    // Коллекция — всегда ResolveAll своего скоупа (как ContainerLocal<IReadOnlyList<T>>),
                    // даже у регистрации с WithParameter: пустой список, а не дырка.
                    edge.Kind = "Collection";
                }
                else if (allowsHole) {
                    edge.Kind = "Hole";
                }
                else if (parentExports.TryGetValue(parameter.TypeFullName, out var parentId)) {
                    edge.Kind = "Hole";
                    edge.ParentRootId = parentId;
                }
                else if (TryGetCollectionElement(parameter.TypeFullName, out _)) {
                    // Нет ни одной регистрации элемента в скоупе — пустой массив, как ResolveAll.
                    edge.Kind = "Collection";
                }
                else if (graph.ParentMissing) {
                    edge.Kind = "Missing";
                }
                else {
                    edge.Kind = "Missing";
                    graph.Diagnostics.Add(Missing(registration, parameter));
                }

                registration.Dependencies.Add(edge);
            }
        }

        private static DiagnosticInfo MissingType(GraphRegistration registration, string typeFullName) {
            var line = registration.Line.ToString(CultureInfo.InvariantCulture);
            return new DiagnosticInfo(
                GraphDescriptors.MissingRegistration,
                registration.Location,
                "(implementation)",
                typeFullName,
                string.IsNullOrEmpty(registration.ImplementationType) ? registration.Kind : registration.ImplementationType,
                registration.File ?? "",
                line);
        }

        private static DiagnosticInfo NoInjectMethod(GraphRegistration registration) {
            return new DiagnosticInfo(
                GraphDescriptors.InjectableWithoutInject,
                registration.Location,
                registration.ImplementationType,
                registration.File ?? "",
                registration.Line.ToString(CultureInfo.InvariantCulture));
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
                   origin == "SwitchFactory" ||
                   origin == LoaderServices.Origin;
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
            public readonly int Sequence;
            public readonly GraphRegistration? Registration;
            public readonly string? CallId;
            public readonly int CallIndex;

            public Step(int ordinal, int sequence, GraphRegistration? registration, string? callId, int callIndex) {
                Ordinal = ordinal;
                Sequence = sequence;
                Registration = registration;
                CallId = callId;
                CallIndex = callIndex;
            }
        }
    }
}
