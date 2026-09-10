using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal sealed class ScopePlan {
        public string RootId = "";
        public string Namespace = "";
        public string ClassName = "";
        public string HintName = "";
        public string LifetimeType = "global::Internal.ILifetime";
        public string LifetimeParam = "lifetime";
        public string LifetimeField = "_lifetime";
        public string ParentType = "global::Internal.IContainer";
        public string ParentParam = "parent";
        public string ParentField = "_parent";
        public string OriginRootId = "";
        public string ViewType = "";
        public bool NeedsRequest;
        public bool HasContainerInterface;
        public bool HasUnityGameObject;
        public bool HasProvides;
        public bool HasDiagnosticsType;
        public bool HasRegistrationInfo;
        public bool HasServiceLifetime;
        public bool HasRuntimeInitialize;
        public bool HasContainerDiagnostics;
        public INamedTypeSymbol? ProvidesDefinition;
        public List<DiagnosticRegistration> DiagnosticRegistrations = new List<DiagnosticRegistration>();
        public List<CtorParam> CtorParams = new List<CtorParam>();
        public List<Field> Fields = new List<Field>();
        public List<Slot> Slots = new List<Slot>();
        public List<int> AlternativeGroups = new List<int>();
        public List<Marker> Markers = new List<Marker>();
        public List<Export> Exports = new List<Export>();
        public List<Factory> Factories = new List<Factory>();
        public List<Provide> Provides = new List<Provide>();
        public List<int> DisposeOrder = new List<int>();
        public HashSet<int> DisposableSlots = new HashSet<int>();

        public sealed class CtorParam {
            public string Type = "";
            public string Name = "";
            public int Slot = -1;
            public string FieldName = "";
            public bool IsLifetime;
            public bool IsParent;
            public bool IsRequest;
            public bool IsAlternative;
            public int AlternativeGroup = -1;
        }

        public sealed class Field {
            public string Type = "";
            public string Name = "";
            public int Slot = -1;
            public bool IsMarker;
            public bool IsExports;
            public bool IsLifetime;
            public bool IsDiagnostics;
        }

        public sealed class Slot {
            public int Index;
            public GraphRegistration Registration = null!;
            public string ImplementationType = "";
            public string FieldName = "";
            public string FieldType = "";
            public string CtorParamName = "";
            public string CreateMethod = "";
            public bool HasField;
            public bool IsTransient;
            public bool IsHole;
            public bool IsAlternative;
            public bool IsSwitch;
            public bool SkipEmit;
            public bool IsUnity;
            public int AlternativeGroup = -1;
            public bool IsGroupPrimary;
            public List<int> GroupMembers = new List<int>();
        }

        public sealed class Marker {
            public string ElementType = "";
            public string FieldName = "";
            public List<int> Slots = new List<int>();
        }

        public sealed class Export {
            public string ServiceType = "";
            public int Slot;
        }

        public sealed class Factory {
            public int Slot;
            public string MethodName = "";
            public string ReturnType = "";
            public string DiscriminantType = "";
            public string DiscriminantName = "discriminant";
        }

        public sealed class Provide {
            public string TargetType = "";
            public int Slot;
        }

        // Строка таблицы Internal.RegistrationInfo; пустой тип — typeof из сборки скоупа не написать.
        public sealed class DiagnosticRegistration {
            public string ImplementationType = "";
            public List<string> ServiceTypes = new List<string>();
            public string Lifetime = "Singleton";
            public List<int> Dependencies = new List<int>();
            public bool IsInstantiated;
            public bool IsExternal;
        }

        public static ScopePlan? Build(
            ScopeGraph graph,
            GraphDocument document,
            Compilation compilation,
            ReferenceSymbols references) {
            if (graph == null || document == null)
                return null;

            GraphMethod? root = null;
            for (var i = 0; i < document.Methods.Count; i++) {
                if (document.Methods[i].Id == graph.RootId) {
                    root = document.Methods[i];
                    break;
                }
            }

            if (root == null)
                return null;
            if (IsHarvest(graph.RootId))
                return null;
            if (HasEmitBlockingDiagnostic(graph, document, root))
                return null;

            var lookupId = string.IsNullOrEmpty(root.OriginId) ? graph.RootId : root.OriginId;
            var method = MethodIds.Find(compilation, lookupId) ?? MethodIds.Find(compilation, graph.RootId);
            if (method == null)
                return null;

            var plan = new ScopePlan { RootId = graph.RootId };
            ScopeNames.ParseRoot(graph.RootId, out plan.Namespace, out var typeName, out var methodName, out plan.ClassName);
            // Имя по внешнему методу, пока корень в нём один. Варианты и несколько корней-локальных
            // функций одного метода называются по локальной функции, иначе совпадут класс и hint.
            if (string.IsNullOrEmpty(graph.Variant) == false || HasSiblingRoots(document, graph.RootId)) {
                var constructName = ConstructMethodName(graph.RootId, methodName);
                plan.ClassName = typeName + constructName + (graph.Variant ?? "") + "Container";
            }

            plan.HintName = plan.ClassName + ".g.cs";
            plan.ViewType = graph.ViewType ?? "";
            plan.OriginRootId = graph.RootId;
            if (string.IsNullOrEmpty(graph.Variant) == false &&
                graph.RootId.EndsWith("+" + graph.Variant, StringComparison.Ordinal))
                plan.OriginRootId = graph.RootId.Substring(0, graph.RootId.Length - graph.Variant.Length - 1);
            plan.HasContainerInterface = compilation.GetTypeByMetadataName("Internal.IContainer") != null;
            plan.HasUnityGameObject = compilation.GetTypeByMetadataName("UnityEngine.GameObject") != null;
            plan.HasDiagnosticsType = compilation.GetTypeByMetadataName("Internal.IContainerDiagnostics") != null;
            plan.HasRegistrationInfo = compilation.GetTypeByMetadataName("Internal.RegistrationInfo") != null;
            plan.HasServiceLifetime = compilation.GetTypeByMetadataName("Internal.ServiceLifetime") != null;
            plan.HasRuntimeInitialize = references.RuntimeInitialize != null;
            plan.HasContainerDiagnostics = plan.HasDiagnosticsType && plan.HasRegistrationInfo && plan.HasServiceLifetime &&
                                           compilation.GetTypeByMetadataName("Internal.ContainerDiagnostics") != null;
            plan.ProvidesDefinition = compilation.GetTypeByMetadataName("Internal.IProvides`1");
            plan.HasProvides = plan.ProvidesDefinition != null;

            var types = new TypeIndex(compilation);
            var used = new HashSet<string>(StringComparer.Ordinal);
            used.Add(plan.LifetimeField);
            used.Add(plan.LifetimeParam);
            used.Add(plan.ParentField);
            used.Add(plan.ParentParam);
            used.Add("_exports");
            used.Add("_diagnostics");
            used.Add("_registrationInfos");
            used.Add("_buildOrder");
            used.Add("_disposed");

            BuildSlots(plan, graph, types, used);
            GroupAlternatives(plan, graph);
            AssignFields(plan, graph, used);
            AssignHoles(plan, graph, used);
            BuildMarkers(plan, graph, used);
            BuildExports(plan, graph);
            BuildFactories(plan, graph, used);
            BuildProvides(plan, graph);
            CollectDisposable(plan, graph, types);
            if (plan.HasContainerDiagnostics)
                BuildDiagnostics(plan, compilation, types);
            return plan;
        }

        // Таблица для графа контейнеров: свои регистрации по слотам, затем дырки рёбер — то, что
        // скоуп берёт у родителя или из WithParameter.
        private static void BuildDiagnostics(ScopePlan plan, Compilation compilation, TypeIndex types) {
            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                var registration = slot.Registration;
                var entry = new DiagnosticRegistration {
                    ImplementationType = DiagnosticType(compilation, types, slot.ImplementationType),
                    Lifetime = registration.Lifetime,
                    IsInstantiated = slot.IsTransient == false && registration.Origin != "Injectable",
                };

                for (var s = 0; s < registration.ServiceTypes.Count; s++)
                    AddDistinct(entry.ServiceTypes, DiagnosticType(compilation, types, registration.ServiceTypes[s]));

                for (var e = 0; e < registration.Dependencies.Count; e++) {
                    var edge = registration.Dependencies[e];
                    if (edge.Kind == "Registration")
                        AddDependency(plan, entry.Dependencies, edge.TargetIndex);
                    else if (edge.Kind == "Collection") {
                        for (var c = 0; c < edge.CollectionIndices.Count; c++)
                            AddDependency(plan, entry.Dependencies, edge.CollectionIndices[c]);
                    }
                }

                plan.DiagnosticRegistrations.Add(entry);
            }

            for (var i = 0; i < plan.CtorParams.Count; i++) {
                var param = plan.CtorParams[i];
                if (string.IsNullOrEmpty(param.FieldName))
                    continue;

                var type = DiagnosticType(compilation, types, param.Type);
                var entry = new DiagnosticRegistration {
                    ImplementationType = type,
                    IsInstantiated = true,
                    IsExternal = true,
                };
                AddDistinct(entry.ServiceTypes, type);
                plan.DiagnosticRegistrations.Add(entry);
            }
        }

        private static string DiagnosticType(Compilation compilation, TypeIndex types, string type) {
            if (string.IsNullOrEmpty(type) || type.IndexOf('{') >= 0)
                return "";

            var symbol = types.Find(type);
            if (symbol == null || symbol.IsUnboundGenericType || ContainsTypeParameter(symbol))
                return "";
            if (compilation.IsSymbolAccessibleWithin(symbol, compilation.Assembly) == false)
                return "";
            return TypeNames.ForCode(symbol);
        }

        private static bool ContainsTypeParameter(ITypeSymbol type) {
            if (type is ITypeParameterSymbol)
                return true;
            if (type is IArrayTypeSymbol array)
                return ContainsTypeParameter(array.ElementType);
            if (type is INamedTypeSymbol named) {
                for (var i = 0; i < named.TypeArguments.Length; i++) {
                    if (ContainsTypeParameter(named.TypeArguments[i]))
                        return true;
                }
            }

            return false;
        }

        private static void AddDependency(ScopePlan plan, List<int> dependencies, int index) {
            if (index < 0 || index >= plan.Slots.Count || dependencies.Contains(index))
                return;
            dependencies.Add(index);
        }

        private static void AddDistinct(List<string> list, string value) {
            if (string.IsNullOrEmpty(value) || list.Contains(value))
                return;
            list.Add(value);
        }

        private static string ConstructMethodName(string rootId, string fallback) {
            if (string.IsNullOrEmpty(rootId))
                return fallback;
            var first = rootId.IndexOf('+');
            if (first < 0)
                return fallback;
            var rest = rootId.Substring(first + 1);
            var second = rest.IndexOf('+');
            var local = second < 0 ? rest : rest.Substring(0, second);
            return string.IsNullOrEmpty(local) ? fallback : local;
        }

        private static bool HasSiblingRoots(GraphDocument document, string rootId) {
            var plus = rootId.IndexOf('+');
            if (plus < 0)
                return false;

            var outer = rootId.Substring(0, plus + 1);
            for (var i = 0; i < document.Methods.Count; i++) {
                var other = document.Methods[i];
                if (other.IsRoot == false || other.Id == rootId || IsHarvest(other.Id))
                    continue;
                if (other.Id.StartsWith(outer, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static bool IsHarvest(string rootId) {
            return string.IsNullOrEmpty(rootId) == false &&
                   rootId.IndexOf("ContainerInstallerHarvest", StringComparison.Ordinal) >= 0;
        }

        public static bool HasEmitBlockingDiagnostic(ScopeGraph graph, GraphDocument document, GraphMethod root) {
            for (var i = 0; i < graph.Diagnostics.Count; i++) {
                var id = graph.Diagnostics[i].Descriptor.Id;
                if (id == "CINGR003" || id == "CINGR004" || id == "CINGR001" || id == "CINGR002" ||
                    id == "CINGR005" || id == "CINGR007")
                    return true;
            }

            var methods = new HashSet<string>(StringComparer.Ordinal);
            CollectMethods(document, root, methods);
            for (var i = 0; i < document.Diagnostics.Count; i++) {
                var diagnostic = document.Diagnostics[i];
                var id = diagnostic.Descriptor.Id;
                if (id == "CINGR007")
                    return true;
                if (id != "CINGR001" && id != "CINGR002" && id != "CINGR005")
                    continue;
                if (diagnostic.MessageArgs.Count < 2)
                    return true;
                if (methods.Contains(diagnostic.MessageArgs[1]))
                    return true;
            }

            return false;
        }

        private static void CollectMethods(GraphDocument document, GraphMethod root, HashSet<string> output) {
            var map = new Dictionary<string, GraphMethod>(StringComparer.Ordinal);
            for (var i = 0; i < document.Methods.Count; i++)
                map[document.Methods[i].Id] = document.Methods[i];

            Walk(root);

            void Walk(GraphMethod method) {
                if (output.Add(method.Id) == false)
                    return;
                method.PadCallLists();
                for (var i = 0; i < method.Calls.Count; i++) {
                    if (map.TryGetValue(method.Calls[i], out var callee))
                        Walk(callee);
                }
            }
        }

        private static void BuildSlots(ScopePlan plan, ScopeGraph graph, TypeIndex types, HashSet<string> used) {
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                var slot = new Slot {
                    Index = i,
                    Registration = registration,
                    ImplementationType = registration.ImplementationType,
                    IsTransient = registration.Lifetime == "Transient",
                    IsHole = IsHoleOrigin(registration.Origin),
                    IsAlternative = registration.Origin == "Alternative",
                    IsSwitch = registration.Origin == "SwitchFactory",
                    // Injectable: без поля, экспорта и конструирования — только ветка в Inject().
                    SkipEmit = registration.Origin == "SceneServices" ||
                               registration.Origin == "ExternalInstaller" ||
                               registration.Origin == "Injectable",
                };

                var symbol = types.Find(registration.ImplementationType);
                if (symbol != null)
                    slot.IsUnity = IsUnityObject(symbol);

                if (slot.IsUnity && slot.IsHole == false && slot.IsSwitch == false && slot.IsTransient == false)
                    slot.IsHole = true;

                if (slot.IsSwitch)
                    slot.CreateMethod = ScopeNames.CreateMethod(
                        string.IsNullOrEmpty(registration.ImplementationType)
                            ? FirstService(registration)
                            : registration.ImplementationType,
                        used);

                if (slot.IsTransient && slot.SkipEmit == false)
                    slot.CreateMethod = ScopeNames.CreateMethod(registration.ImplementationType, used);

                plan.Slots.Add(slot);
            }
        }

        private static void GroupAlternatives(ScopePlan plan, ScopeGraph graph) {
            var groups = new List<List<int>>();
            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                if (slot.IsAlternative == false)
                    continue;

                var groupIndex = -1;
                for (var g = 0; g < groups.Count; g++) {
                    if (SharesService(graph, groups[g], slot.Registration)) {
                        groupIndex = g;
                        break;
                    }
                }

                if (groupIndex < 0) {
                    groupIndex = groups.Count;
                    groups.Add(new List<int>());
                    plan.AlternativeGroups.Add(groupIndex);
                }

                groups[groupIndex].Add(i);
                slot.AlternativeGroup = groupIndex;
            }

            for (var g = 0; g < groups.Count; g++) {
                var members = groups[g];
                var primary = members[members.Count - 1];
                for (var i = 0; i < members.Count; i++) {
                    var slot = plan.Slots[members[i]];
                    slot.GroupMembers.Clear();
                    slot.GroupMembers.AddRange(members);
                    slot.IsGroupPrimary = members[i] == primary;
                    slot.AlternativeGroup = g;
                }
            }
        }

        private static bool SharesService(ScopeGraph graph, List<int> group, GraphRegistration candidate) {
            for (var i = 0; i < group.Count; i++) {
                var existing = graph.Registrations[group[i]];
                if (SharesService(existing, candidate))
                    return true;
            }

            return false;
        }

        private static bool SharesService(GraphRegistration left, GraphRegistration right) {
            for (var i = 0; i < left.ServiceTypes.Count; i++) {
                for (var j = 0; j < right.ServiceTypes.Count; j++) {
                    if (left.ServiceTypes[i] == right.ServiceTypes[j])
                        return true;
                }
            }

            return false;
        }

        private static void AssignFields(ScopePlan plan, ScopeGraph graph, HashSet<string> used) {
            plan.Fields.Add(new Field {
                Type = plan.LifetimeType,
                Name = plan.LifetimeField,
                IsLifetime = true,
            });
            plan.Fields.Add(new Field {
                Type = plan.ParentType,
                Name = plan.ParentField,
            });

            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                if (slot.SkipEmit || slot.IsTransient)
                    continue;
                if (slot.IsAlternative && slot.IsGroupPrimary == false)
                    continue;

                slot.HasField = true;
                if (slot.IsAlternative && slot.IsGroupPrimary) {
                    slot.FieldType = CommonService(graph, slot.GroupMembers);
                    if (string.IsNullOrEmpty(slot.FieldType))
                        slot.FieldType = slot.ImplementationType;
                }
                else {
                    slot.FieldType = string.IsNullOrEmpty(slot.ImplementationType)
                        ? FirstService(slot.Registration)
                        : slot.ImplementationType;
                }

                if (string.IsNullOrEmpty(slot.FieldType))
                    slot.FieldType = "object";

                slot.FieldName = ScopeNames.Field(slot.FieldType, used);
                plan.Fields.Add(new Field {
                    Type = slot.FieldType,
                    Name = slot.FieldName,
                    Slot = slot.Index,
                });

                if (slot.IsAlternative) {
                    for (var m = 0; m < slot.GroupMembers.Count; m++) {
                        var member = plan.Slots[slot.GroupMembers[m]];
                        member.FieldName = slot.FieldName;
                        member.FieldType = slot.FieldType;
                        member.HasField = member.IsGroupPrimary;
                    }
                }
            }
        }

        private static void AssignHoles(ScopePlan plan, ScopeGraph graph, HashSet<string> used) {
            plan.CtorParams.Add(new CtorParam {
                Type = plan.LifetimeType,
                Name = plan.LifetimeParam,
                IsLifetime = true,
            });
            plan.CtorParams.Add(new CtorParam {
                Type = plan.ParentType,
                Name = plan.ParentParam,
                IsParent = true,
            });

            // Параметры веток switch берутся из запроса в момент построения выбранной ветки.
            plan.NeedsRequest = HasArmParameters(graph) || plan.AlternativeGroups.Count > 0;
            if (plan.NeedsRequest) {
                used.Add("request");
                plan.CtorParams.Add(new CtorParam {
                    Type = "global::Internal.GeneratedScopeRequest",
                    Name = "request",
                    IsRequest = true,
                });
            }

            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                if (slot.IsHole == false || slot.SkipEmit)
                    continue;
                if (slot.HasField == false && slot.IsAlternative == false)
                    continue;

                var preferred = slot.Registration.Hole ?? "";
                if (preferred.IndexOf(';') >= 0)
                    preferred = preferred.Substring(0, preferred.IndexOf(';')).Trim();

                slot.CtorParamName = ScopeNames.Parameter(preferred, slot.FieldType, used);
                plan.CtorParams.Add(new CtorParam {
                    Type = slot.FieldType,
                    Name = slot.CtorParamName,
                    Slot = slot.Index,
                });
            }

            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                var registration = slot.Registration;
                for (var e = 0; e < registration.Dependencies.Count; e++) {
                    var edge = registration.Dependencies[e];
                    if (edge.Kind != "Hole")
                        continue;
                    if (HasCtorParam(plan, edge.ParameterType, edge.ParameterName))
                        continue;

                    // Дырка ребра (экспорт родителя, WithParameter) — своё поле: Construct компонента
                    // зовётся и из конструктора, и из Inject / IProvides.
                    var name = ScopeNames.Parameter(edge.ParameterName, edge.ParameterType, used);
                    var field = ScopeNames.Unique("_" + name.TrimStart('@'), used);
                    plan.Fields.Add(new Field {
                        Type = edge.ParameterType,
                        Name = field,
                    });
                    plan.CtorParams.Add(new CtorParam {
                        Type = edge.ParameterType,
                        Name = name,
                        FieldName = field,
                    });
                }
            }

        }

        private static bool HasArmParameters(ScopeGraph graph) {
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var dependencies = graph.Registrations[i].Dependencies;
                for (var e = 0; e < dependencies.Count; e++) {
                    if (dependencies[e].Kind == "ArmParameter")
                        return true;
                }
            }

            return false;
        }

        private static bool HasCtorParam(ScopePlan plan, string type, string name) {
            for (var i = 0; i < plan.CtorParams.Count; i++) {
                var param = plan.CtorParams[i];
                if (param.Type == type && (param.Name == name || param.Name == ScopeNames.Escape(name)))
                    return true;
            }

            return false;
        }

        private static void BuildMarkers(ScopePlan plan, ScopeGraph graph, HashSet<string> used) {
            var byType = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                var slot = plan.Slots[i];
                if (slot.SkipEmit)
                    continue;

                var added = false;
                for (var s = 0; s < registration.ServiceTypes.Count; s++) {
                    AddMarker(byType, registration.ServiceTypes[s], i);
                    added = true;
                }

                if (added == false && string.IsNullOrEmpty(registration.ImplementationType) == false)
                    AddMarker(byType, registration.ImplementationType, i);
            }

            var wanted = new HashSet<string>(StringComparer.Ordinal);
            AddKnownMarkers(wanted);
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                for (var e = 0; e < registration.Dependencies.Count; e++) {
                    var edge = registration.Dependencies[e];
                    if (edge.Kind != "Collection")
                        continue;
                    if (TryCollectionElement(edge.ParameterType, out var element))
                        wanted.Add(element);
                }
            }

            // Transient не лежит в _exports, поэтому даже одиночный получает список: иначе
            // ResolveAll молча вернёт пустоту.
            foreach (var pair in byType) {
                if (pair.Value.Count > 1 || HasTransientSlot(plan, pair.Value))
                    wanted.Add(pair.Key);
            }

            foreach (var type in wanted) {
                if (byType.TryGetValue(type, out var slots) == false || slots.Count == 0)
                    continue;

                var marker = new Marker {
                    ElementType = type,
                    FieldName = ScopeNames.MarkerField(type, used),
                };
                marker.Slots.AddRange(slots);
                plan.Markers.Add(marker);
                if (HasTransientSlot(plan, slots))
                    continue;

                plan.Fields.Add(new Field {
                    Type = type + "[]",
                    Name = marker.FieldName,
                    IsMarker = true,
                });
            }
        }

        public static bool HasTransientSlot(ScopePlan plan, List<int> slots) {
            for (var i = 0; i < slots.Count; i++) {
                if (plan.Slots[slots[i]].IsTransient)
                    return true;
            }

            return false;
        }

        private static void AddKnownMarkers(HashSet<string> wanted) {
            wanted.Add("global::Internal.IScopeBaseSetup");
            wanted.Add("global::Internal.IScopeBaseSetupAsync");
            wanted.Add("global::Internal.IScopeSetup");
            wanted.Add("global::Internal.IScopeSetupAsync");
            wanted.Add("global::Internal.IScopeSetupCompletion");
            wanted.Add("global::Internal.IScopeSetupCompletionAsync");
            wanted.Add("global::Internal.IScopeLoaded");
            wanted.Add("global::Internal.IScopeLoadedAsync");
            wanted.Add("global::Internal.IScopeDispose");
            wanted.Add("global::Internal.IScopeDisposeAsync");
            wanted.Add("global::Internal.ISceneService");
            wanted.Add("global::Internal.IEntityComponent");
        }

        private static void AddMarker(Dictionary<string, List<int>> byType, string type, int index) {
            if (string.IsNullOrEmpty(type))
                return;
            if (byType.TryGetValue(type, out var list) == false) {
                list = new List<int>();
                byType[type] = list;
            }

            list.Add(index);
        }

        private static void BuildExports(ScopePlan plan, ScopeGraph graph) {
            var last = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var slot = plan.Slots[i];
                if (slot.SkipEmit || slot.IsTransient)
                    continue;
                if (slot.IsAlternative && slot.IsGroupPrimary == false)
                    continue;
                if (slot.HasField == false && string.IsNullOrEmpty(slot.FieldName))
                    continue;

                var registration = graph.Registrations[i];
                if (slot.IsAlternative) {
                    last[slot.FieldType] = i;
                    var shared = CommonService(graph, slot.GroupMembers);
                    if (string.IsNullOrEmpty(shared) == false)
                        last[shared] = i;
                    continue;
                }

                for (var s = 0; s < registration.ServiceTypes.Count; s++) {
                    if (string.IsNullOrEmpty(registration.ServiceTypes[s]) == false)
                        last[registration.ServiceTypes[s]] = i;
                }

                if (string.IsNullOrEmpty(registration.ImplementationType) == false)
                    last[registration.ImplementationType] = i;
            }

            foreach (var pair in last) {
                if (string.IsNullOrEmpty(pair.Key))
                    continue;
                plan.Exports.Add(new Export { ServiceType = pair.Key, Slot = pair.Value });
            }
        }

        private static void BuildFactories(ScopePlan plan, ScopeGraph graph, HashSet<string> used) {
            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                if (slot.IsSwitch == false)
                    continue;

                var returnType = FirstService(slot.Registration);
                if (string.IsNullOrEmpty(returnType))
                    returnType = slot.ImplementationType;
                if (string.IsNullOrEmpty(returnType) && slot.Registration.Arms.Count > 0)
                    returnType = slot.Registration.Arms[0].ImplementationType;
                if (string.IsNullOrEmpty(returnType))
                    returnType = "object";

                var discriminantType = InferDiscriminantType(slot.Registration);
                plan.Factories.Add(new Factory {
                    Slot = i,
                    MethodName = slot.CreateMethod,
                    ReturnType = returnType,
                    DiscriminantType = discriminantType,
                });
            }
        }

        internal static string InferDiscriminantType(GraphRegistration registration) {
            if (registration.Arms.Count == 0)
                return "object";

            var pattern = registration.Arms[0].Discriminant ?? "";
            var dot = pattern.LastIndexOf('.');
            if (dot <= 0)
                return "object";

            var type = pattern.Substring(0, dot).Trim();
            if (type.StartsWith("global::", StringComparison.Ordinal) == false && type.IndexOf('.') >= 0)
                type = "global::" + type;
            if (string.IsNullOrEmpty(type))
                return "object";
            return type;
        }

        private static void BuildProvides(ScopePlan plan, ScopeGraph graph) {
            if (plan.HasProvides == false)
                return;

            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                if (slot.IsHole == false || slot.SkipEmit)
                    continue;
                if (string.IsNullOrEmpty(slot.ImplementationType))
                    continue;

                var origin = slot.Registration.Origin;
                if (origin != "PrefabInstance" && origin != "PrefabAsset" && origin != "InjectExisting")
                    continue;

                var hasConstruct = false;
                for (var e = 0; e < slot.Registration.Dependencies.Count; e++) {
                    if (slot.Registration.Dependencies[e].Source == "Construct")
                        hasConstruct = true;
                }

                if (hasConstruct == false)
                    continue;

                var duplicate = false;
                for (var p = 0; p < plan.Provides.Count; p++) {
                    if (plan.Provides[p].TargetType == slot.ImplementationType) {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                    continue;

                plan.Provides.Add(new Provide {
                    TargetType = slot.ImplementationType,
                    Slot = i,
                });
            }
        }

        private static void CollectDisposable(ScopePlan plan, ScopeGraph graph, TypeIndex types) {
            var disposable = types.Find("global::System.IDisposable") ??
                             types.Find("System.IDisposable");

            for (var i = graph.ConstructionOrder.Count - 1; i >= 0; i--) {
                var index = graph.ConstructionOrder[i];
                if (index < 0 || index >= plan.Slots.Count)
                    continue;

                var slot = plan.Slots[index];
                if (slot.HasField == false && string.IsNullOrEmpty(slot.FieldName))
                    continue;
                if (slot.IsTransient || slot.IsSwitch || slot.SkipEmit)
                    continue;

                var symbol = types.Find(slot.ImplementationType);
                if (symbol == null)
                    symbol = types.Find(slot.FieldType);
                if (IsDisposable(symbol, disposable) == false)
                    continue;

                if (slot.IsAlternative && slot.IsGroupPrimary == false)
                    continue;

                plan.DisposeOrder.Add(index);
                plan.DisposableSlots.Add(index);
            }
        }

        private static bool IsDisposable(INamedTypeSymbol? type, INamedTypeSymbol? disposable) {
            if (type == null)
                return false;
            if (disposable != null && type.Implements(disposable))
                return true;
            foreach (var implemented in type.AllInterfaces) {
                if (implemented.SpecialType == SpecialType.System_IDisposable)
                    return true;
                if (implemented.Name == "IDisposable" && implemented.ContainingNamespace?.ToDisplayString() == "System")
                    return true;
            }

            return false;
        }

        private static bool IsUnityObject(INamedTypeSymbol type) {
            var current = type;
            while (current != null) {
                if (current.Name == "Object" && current.ContainingNamespace?.ToDisplayString() == "UnityEngine")
                    return true;
                current = current.BaseType;
            }

            return false;
        }

        private static bool IsHoleOrigin(string origin) {
            return origin == "InstanceHole" ||
                   origin == "PrefabInstance" ||
                   origin == "PrefabAsset" ||
                   origin == "SceneServices" ||
                   origin == "InjectExisting";
        }

        private static string CommonService(ScopeGraph graph, List<int> members) {
            if (members.Count == 0)
                return "";

            var first = graph.Registrations[members[0]];
            var fallback = "";
            for (var s = 0; s < first.ServiceTypes.Count; s++) {
                var type = first.ServiceTypes[s];
                if (string.IsNullOrEmpty(type))
                    continue;

                var shared = true;
                for (var m = 1; m < members.Count; m++) {
                    if (graph.Registrations[members[m]].ServiceTypes.Contains(type) == false) {
                        shared = false;
                        break;
                    }
                }

                if (shared == false)
                    continue;

                fallback = type;
                var shortName = ScopeNames.Short(type);
                if (shortName.Length > 1 && shortName[0] == 'I' && char.IsUpper(shortName[1]))
                    return type;
            }

            return fallback;
        }

        private static string FirstService(GraphRegistration registration) {
            for (var i = 0; i < registration.ServiceTypes.Count; i++) {
                if (string.IsNullOrEmpty(registration.ServiceTypes[i]) == false)
                    return registration.ServiceTypes[i];
            }

            return "";
        }

        private static bool TryCollectionElement(string typeFullName, out string elementType) {
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
    }
}
