using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ContainerGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ContainerGenerator.Verify {
    internal static class Program {
        private static int _failed;

        public static int Main() {
            Run("LoadGlobal graph", TestLoadGlobal);
            Run("CardFactory.Build graph", TestCardFactoryBuild);
            Run("CINGR003 missing parameter name", TestMissingRegistration);
            Run("CINGR004 cycle path", TestCycle);
            Run("IReadOnlyList FullyQualifiedFormat", TestCollectionTypeKey);
            Run("LoadGlobal emit compiles without IResolvePlan", TestLoadGlobalEmit);
            Run("Marker array order matches registration", TestMarkerOrderEmit);
            Run("Transient has method not field", TestTransientEmit);
            Run("ContainerRuntimeScope disables emit", TestRuntimeScopeEmit);
            Run("CINGR003 does not emit class", TestMissingDoesNotEmit);
            Run("CINGR001 uncovered syntax", TestUncoveredSyntax);
            Run("Marker collection order", TestMarkerCollectionOrder);
            Run("1b manifest uses assembly attributes", TestManifestEmitsAttributes);
            Run("1b cross-assembly installer no CINGR002", TestCrossAssemblyManifest);
            Run("1b harvested local functions keep order", TestHarvestLocalFunctionOrder);
            Run("1c entity Load splits CardFactory variants", TestEntityCardVariants);
            Run("1c entity emit two classes without IResolvePlan", TestEntityCardEmit);
            Run("1c GamePlayer variants bind stripped Board", TestEntityPlayerStrippedBoard);
            Run("1c CINGR006 duplicate view type names both paths", TestCingr006DuplicateView);
            Run("1c CINGR006 skips base view type", TestCingr006BaseViewSkipped);
            Run("1c CINGR006 skips SceneServicesFactory", TestCingr006SceneFactorySkipped);
            Run("1c CINGR005 non-enumerable variant condition", TestCingr005Unenumerable);
            if (_failed == 0)
                Console.WriteLine("ALL PASSED");
            else
                Console.WriteLine("FAILED " + _failed);
            return _failed == 0 ? 0 : 1;
        }

        private static void Run(string name, Action test) {
            try {
                test();
                Console.WriteLine("PASS  " + name);
            }
            catch (Exception exception) {
                _failed++;
                Console.WriteLine("FAIL  " + name);
                Console.WriteLine("      " + exception.Message);
            }
        }

        private static void TestLoadGlobal() {
            var graph = ResolveRoot(LoadGlobalSource, "LoadGlobal");
            Require(graph.Registrations.Count >= 8, "LoadGlobal registrations: " + graph.Registrations.Count);
            RequireNo(graph, "CINGR003");
            RequireNo(graph, "CINGR004");

            var delay = RequireEdge(graph, "DelayRunner", "updater");
            Require(delay.Kind == "Registration", "DelayRunner.updater kind " + delay.Kind);
            Require(
                graph.Registrations[delay.TargetIndex].ImplementationType.IndexOf("Updater", StringComparison.Ordinal) >= 0,
                "DelayRunner.updater target " + graph.Registrations[delay.TargetIndex].ImplementationType);

            var camera = RequireEdge(graph, "CameraUtils", "camera");
            Require(camera.Kind == "Registration", "CameraUtils.camera kind " + camera.Kind);

            var language = RequireEdge(graph, "ItchLanguageProvider", "api");
            Require(language.Kind == "Registration", "ItchLanguageProvider.api kind " + language.Kind);

            var machine = RequireEdge(graph, "UIStateMachine", "lifetime");
            Require(machine.Kind == "Implicit", "UIStateMachine.lifetime kind " + machine.Kind);

            var screen = RequireEdge(graph, "LoadingScreen", "updater");
            Require(screen.Kind == "Registration", "LoadingScreen.Construct updater kind " + screen.Kind);
            Require(screen.Source == "Construct", "LoadingScreen.updater source " + screen.Source);

            RequireOrder(graph, "CurrentCamera", "CameraUtils");
            RequireOrder(graph, "Updater", "DelayRunner");
            Dump(graph);
        }

        private static void TestCardFactoryBuild() {
            var graph = ResolveRoot(CardFactorySource, "CardFactory");
            Require(graph.RootId.IndexOf("Build", StringComparison.Ordinal) >= 0, "root id " + graph.RootId);
            Require(graph.Registrations.Count >= 8, "CardFactory registrations: " + graph.Registrations.Count);
            RequireNo(graph, "CINGR003");
            RequireNo(graph, "CINGR004");

            var guidIndex = graph.Registrations.FindIndex(r => r.ImplementationType.IndexOf("Guid", StringComparison.Ordinal) >= 0);
            var detectorIndex = graph.Registrations.FindIndex(r => r.ImplementationType.IndexOf("CardDropDetector", StringComparison.Ordinal) >= 0);
            Require(guidIndex >= 0 && detectorIndex > guidIndex,
                "flatten must keep RegisterInstance(cardId) before AddCardLocalComponents");
            RequireOrder(graph, "CardContext", "CardDropArea");

            var handle = RequireEdge(graph, "HandEntryHandle", "card");
            Require(handle.Kind == "Registration", "HandEntryHandle.card kind " + handle.Kind);
            Require(
                graph.Registrations[handle.TargetIndex].ImplementationType.IndexOf("LocalCard", StringComparison.Ordinal) >= 0,
                "HandEntryHandle.card target " + graph.Registrations[handle.TargetIndex].ImplementationType);

            var lifetime = RequireEdge(graph, "CardStateLifetime", "lifetime");
            Require(lifetime.Kind == "Implicit" || lifetime.Kind == "Hole", "CardStateLifetime.lifetime " + lifetime.Kind);

            var area = RequireEdge(graph, "CardDropArea", "context");
            Require(area.Kind == "Registration", "CardDropArea.context kind " + area.Kind);
            Dump(graph);
        }

        private static void TestMissingRegistration() {
            var graph = ResolveRoot(MissingSource, "MissingRoot");
            var diagnostic = RequireDiagnostic(graph, "CINGR003");
            Require(
                diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error,
                "CINGR003 severity " + diagnostic.Descriptor.DefaultSeverity);
            var message = diagnostic.ToDiagnostic().GetMessage();
            Require(message.IndexOf("parameter 'dependency'", StringComparison.Ordinal) >= 0, "CINGR003 message: " + message);
            Require(message.IndexOf("UnregisteredDependency", StringComparison.Ordinal) >= 0, "CINGR003 type: " + message);
            Require(graph.Diagnostics.Count == 1, "expected one CINGR003, got " + graph.Diagnostics.Count);
            Console.WriteLine("      " + message);
        }

        private static void TestCycle() {
            var graph = ResolveRoot(CycleSource, "CycleRoot");
            var diagnostic = RequireDiagnostic(graph, "CINGR004");
            Require(
                diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error,
                "CINGR004 severity " + diagnostic.Descriptor.DefaultSeverity);
            var message = diagnostic.ToDiagnostic().GetMessage();
            Require(message.IndexOf("CycleA", StringComparison.Ordinal) >= 0, "path A: " + message);
            Require(message.IndexOf("CycleB", StringComparison.Ordinal) >= 0, "path B: " + message);
            Require(message.IndexOf("CycleC", StringComparison.Ordinal) >= 0, "path C: " + message);
            Require(message.IndexOf("->", StringComparison.Ordinal) >= 0, "path arrows: " + message);
            var first = message.IndexOf("CycleA", StringComparison.Ordinal);
            var last = message.LastIndexOf("CycleA", StringComparison.Ordinal);
            Require(last > first, "cycle path must start and close: " + message);
            Console.WriteLine("      " + message);
        }

        private static void TestLoadGlobalEmit() {
            var emitted = EmitRoot(LoadGlobalSource, "LoadGlobal", out var graph);
            Require(emitted.IndexOf("IResolvePlan", StringComparison.Ordinal) < 0, "generated class must not use IResolvePlan");
            Require(emitted.IndexOf("Container", StringComparison.Ordinal) >= 0, "missing container class");
            Require(emitted.IndexOf("LoadGlobalContainer", StringComparison.Ordinal) >= 0,
                "class name must follow {RootType}{RootMethod}Container; got snippet:\n" + Head(emitted));
            Require(emitted.IndexOf("internal sealed class GlobalScopeExtensionsLoadGlobalContainer", StringComparison.Ordinal) >= 0,
                "expected GlobalScopeExtensionsLoadGlobalContainer:\n" + Head(emitted));
            Require(emitted.IndexOf("IsGenerated => true", StringComparison.Ordinal) >= 0, "IsGenerated must be true");
            Require(emitted.IndexOf("alternative0", StringComparison.Ordinal) >= 0, "alternative must be a constructor parameter");
            Require(emitted.IndexOf("ItchLanguageDebugAPI", StringComparison.Ordinal) >= 0, "alternative true branch missing");
            Require(emitted.IndexOf("ItchLanguageExternAPI", StringComparison.Ordinal) >= 0, "alternative false branch missing");
            Require(emitted.IndexOf("CreatePopup", StringComparison.Ordinal) < 0, "LoadGlobal has no transients");
            Require(emitted.IndexOf("GeneratedScopes.Register", StringComparison.Ordinal) >= 0,
                "generated class must register with GeneratedScopes:\n" + Head(emitted));
            Require(emitted.IndexOf("LoadGlobal+Construct", StringComparison.Ordinal) >= 0,
                "GeneratedScopes.Register rootId must contain LoadGlobal+Construct:\n" + Head(emitted, 2000));
            RequireCompiles(emitted, LoadGlobalSource);
            DumpEmit(graph, emitted);
        }

        private static void TestMarkerOrderEmit() {
            var emitted = EmitRoot(CollectionSource, "CollectionRoot", out _);
            var marker = emitted.IndexOf("new global::Sample.IAchievementTier[]", StringComparison.Ordinal);
            Require(marker >= 0, "missing IAchievementTier marker array:\n" + emitted);
            var bronze = emitted.IndexOf("BronzeTier", marker, StringComparison.Ordinal);
            var gold = emitted.IndexOf("GoldTier", marker, StringComparison.Ordinal);
            Require(bronze >= 0 && gold > bronze, "marker order must be Bronze then Gold");
            Require(emitted.IndexOf("IResolvePlan", StringComparison.Ordinal) < 0, "marker emit used IResolvePlan");
            RequireCompiles(emitted, CollectionSource);
        }

        private static void TestTransientEmit() {
            var emitted = EmitRoot(TransientSource, "TransientRoot", out _);
            Require(emitted.IndexOf("CreatePopup", StringComparison.Ordinal) >= 0, "missing CreatePopup method:\n" + Head(emitted));
            Require(emitted.IndexOf("private readonly global::Sample.Popup _popup", StringComparison.Ordinal) < 0,
                "Transient must not have a field");
            Require(emitted.IndexOf("IResolvePlan", StringComparison.Ordinal) < 0, "transient emit used IResolvePlan");
            RequireCompiles(emitted, TransientSource);
        }

        private static void TestRuntimeScopeEmit() {
            var compilation = Compile(RuntimeScopeSource);
            var references = ReferenceSymbols.Create(compilation);
            Require(references != null, "ReferenceSymbols.Create returned null");
            var document = new GraphWalker(compilation, references!).Walk();
            var graph = EdgeResolver.ResolveByHint(document, compilation, references!, "RuntimeRoot");
            Require(graph != null, "no RuntimeRoot");
            Require(ScopeEmitter.IsRuntimeScope(compilation, references!, graph!.RootId), "attribute must be detected");
            var emitted = ScopeEmitter.TryEmit(graph, document, compilation, references!, out _, out var source);
            Require(emitted == false, "runtime scope must not emit; got:\n" + source);
        }

        private static void TestMissingDoesNotEmit() {
            var compilation = Compile(MissingSource);
            var references = ReferenceSymbols.Create(compilation);
            Require(references != null, "ReferenceSymbols.Create returned null");
            var document = new GraphWalker(compilation, references!).Walk();
            var graph = EdgeResolver.ResolveByHint(document, compilation, references!, "MissingRoot");
            Require(graph != null, "no MissingRoot");
            var emitted = ScopeEmitter.TryEmit(graph!, document, compilation, references!, out _, out var source);
            Require(emitted == false, "CINGR003 scope must not emit; got:\n" + source);
        }

        private static void TestCollectionTypeKey() {
            var graph = ResolveRoot(CollectionSource, "CollectionRoot");
            RequireNo(graph, "CINGR003");
            var edge = RequireEdge(graph, "AchievementRow", "tiers");
            Require(edge.Kind == "Collection", "tiers kind " + edge.Kind);
            Require(edge.CollectionIndices.Count == 2, "tiers count " + edge.CollectionIndices.Count);
            Require(
                graph.Registrations[edge.CollectionIndices[0]].ImplementationType.IndexOf("BronzeTier", StringComparison.Ordinal) >= 0,
                "first collection item must follow registration order");
            Require(
                graph.Registrations[edge.CollectionIndices[1]].ImplementationType.IndexOf("GoldTier", StringComparison.Ordinal) >= 0,
                "second collection item must follow registration order");
        }

        private static void TestUncoveredSyntax() {
            var compilation = Compile(UncoveredSource);
            var references = ReferenceSymbols.Create(compilation);
            Require(references != null, "ReferenceSymbols.Create returned null");
            var document = new GraphWalker(compilation, references!).Walk();
            var diagnostic = RequireDocumentDiagnostic(document, "CINGR001");
            var message = diagnostic.ToDiagnostic().GetMessage();
            Require(message.IndexOf("LockStatement", StringComparison.Ordinal) >= 0, "CINGR001 message: " + message);
            Console.WriteLine("      " + message);
        }

        private static void TestManifestEmitsAttributes() {
            var compilation = Compile(ManifestLibSource, "InternalLib");
            var document = WalkDocument(compilation);
            var emitted = GraphEmitter.Emit(document, compilation);
            Require(emitted.IndexOf("[assembly: global::Internal.ContainerInstaller(", StringComparison.Ordinal) >= 0,
                "expected assembly ContainerInstaller attributes:\n" + Head(emitted));
            Require(emitted.IndexOf("static readonly ContainerGraphMethod[]", StringComparison.Ordinal) < 0,
                "GraphEmitter must not emit static readonly method arrays");
            var attributeClass = GraphEmitter.EmitAttributeClass(compilation);
            Require(attributeClass != null && attributeClass.IndexOf("class ContainerInstallerAttribute", StringComparison.Ordinal) >= 0,
                "attribute class must be generated when missing");
        }

        private static void TestCrossAssemblyManifest() {
            var shared = EmitStubs("ContainerStubs");
            var libCompilation = Compile(ManifestLibSource, "InternalLib", extra: new[] { shared }, includeStubs: false);
            var libDocument = WalkDocument(libCompilation);
            var libEmit = GraphEmitter.Emit(libDocument, libCompilation);
            var libReference = EmitAssembly(libCompilation, libEmit, "InternalLib");

            var consumerCompilation = Compile(
                ManifestConsumerSource,
                "GamePlayLib",
                extra: new[] { shared, libReference },
                includeStubs: false);
            var consumerDocument = WalkDocument(consumerCompilation, harvest: false);
            ManifestReader.Merge(consumerDocument, consumerCompilation);
            ManifestReader.ReportUnresolved(consumerDocument);
            RequireNoDocument(consumerDocument, "CINGR002");

            var connection = FindMethod(consumerDocument, "AddNetworkConnection");
            Require(connection.Registrations.Count >= 2, "manifest must carry AddNetworkConnection registrations: " +
                                                         connection.Registrations.Count);
            Require(
                connection.Registrations[0].ImplementationType.IndexOf("NetworkConnection", StringComparison.Ordinal) >= 0,
                "first registration " + connection.Registrations[0].ImplementationType);
            Require(
                connection.Registrations[1].ImplementationType.IndexOf("NetworkCommandsCollection", StringComparison.Ordinal) >= 0,
                "second registration " + connection.Registrations[1].ImplementationType);

            var remote = FindMethod(consumerDocument, "AddRemoteEntity");
            Require(remote.Registrations.Count >= 1, "manifest must carry AddRemoteEntity");
            Require(
                remote.Registrations[0].ImplementationType.IndexOf("NetworkEntity", StringComparison.Ordinal) >= 0,
                "AddRemoteEntity impl " + remote.Registrations[0].ImplementationType);

            var references = ReferenceSymbols.Create(consumerCompilation);
            Require(references != null, "consumer ReferenceSymbols");
            var graph = EdgeResolver.ResolveByHint(consumerDocument, consumerCompilation, references!, "GamePlayScopeExtensions");
            Require(graph != null, "consumer root missing");
            RequireNo(graph!, "CINGR002");
            FindRegistration(graph!, "NetworkConnection");
            FindRegistration(graph!, "NetworkObjectsCollection");
            FindRegistration(graph!, "SessionConnection");
            FindRegistration(graph!, "GamePlayLoop");
            var player = EdgeResolver.ResolveByHint(consumerDocument, consumerCompilation, references!, "GamePlayerFactory");
            Require(player != null, "GamePlayerFactory root missing");
            FindRegistration(player!, "NetworkEntity");
        }

        private static void TestHarvestLocalFunctionOrder() {
            var compilation = Compile(ManifestLibSource, "InternalLib");
            var document = WalkDocument(compilation);
            var session = FindMethod(document, "AddSessionServices");
            Require(session.Registrations.Count >= 3, "harvested AddSessionServices registrations: " +
                                                      session.Registrations.Count + " ids=" +
                                                      string.Join(",", session.Registrations.ConvertAll(r => Short(r.ImplementationType))));
            Require(
                session.Registrations[0].ImplementationType.IndexOf("NetworkObjectsCollection", StringComparison.Ordinal) >= 0,
                "first harvested registration " + session.Registrations[0].ImplementationType);
            Require(
                session.Registrations[session.Registrations.Count - 1].ImplementationType.IndexOf("SessionConnection", StringComparison.Ordinal) >= 0,
                "last harvested registration " + session.Registrations[session.Registrations.Count - 1].ImplementationType);
            Require(session.Calls.Exists(c => c.IndexOf("AddNetworkConnection", StringComparison.Ordinal) >= 0),
                "inlined AddSessionServices must still call AddNetworkConnection; calls=" +
                string.Join(",", session.Calls));
        }

        private static void TestMarkerCollectionOrder() {
            var graph = ResolveRoot(MarkerSource, "MarkerRoot");
            RequireNo(graph, "CINGR003");
            var edge = RequireEdge(graph, "MarkerConsumer", "setups");
            Require(edge.Kind == "Collection", "setups kind " + edge.Kind);
            Require(edge.CollectionIndices.Count == 3, "setups count " + edge.CollectionIndices.Count);
            Require(
                graph.Registrations[edge.CollectionIndices[0]].ImplementationType.IndexOf("MarkerFirst", StringComparison.Ordinal) >= 0,
                "first marker must follow registration order");
            Require(
                graph.Registrations[edge.CollectionIndices[1]].ImplementationType.IndexOf("MarkerSecond", StringComparison.Ordinal) >= 0,
                "second marker must follow registration order");
            Require(
                graph.Registrations[edge.CollectionIndices[2]].ImplementationType.IndexOf("MarkerThird", StringComparison.Ordinal) >= 0,
                "third marker must follow registration order");
        }

        private static void TestEntityCardVariants() {
            var compilation = Compile(EntityCardSource, extraSource: EntityCardAssets);
            var document = WalkBound(compilation);
            var local = RequireRoot(document, "+Local");
            var remote = RequireRoot(document, "+Remote");
            Require(local.ViewType.IndexOf("CardLocalScopeEntity", StringComparison.Ordinal) >= 0, "local view " + local.ViewType);
            Require(remote.ViewType.IndexOf("CardRemoteScopeEntity", StringComparison.Ordinal) >= 0, "remote view " + remote.ViewType);
            Require(local.Calls.Exists(c => c.IndexOf("CardPointerHandler.Register", StringComparison.Ordinal) >= 0),
                "local must call CardPointerHandler.Register; calls=" + string.Join(",", local.Calls));
            Require(remote.Calls.Exists(c => c.IndexOf("CardPointerHandler.Register", StringComparison.Ordinal) >= 0) == false,
                "remote must not include CardPointerHandler");
            Require(remote.Calls.Exists(c => c.IndexOf("CardRevealView.Register", StringComparison.Ordinal) >= 0),
                "remote must call CardRevealView.Register; calls=" + string.Join(",", remote.Calls));

            var references = ReferenceSymbols.Create(compilation);
            var localGraph = EdgeResolver.ResolveRoot(document, local, compilation, references!);
            var remoteGraph = EdgeResolver.ResolveRoot(document, remote, compilation, references!);
            RequireNo(localGraph, "CINGR003");
            RequireNo(remoteGraph, "CINGR003");
            FindRegistration(localGraph, "LocalCard");
            FindRegistration(localGraph, "CardPointerHandler");
            FindRegistration(remoteGraph, "RemoteCard");
            FindRegistration(remoteGraph, "CardRevealView");
            Require(
                localGraph.Registrations.Exists(r => r.ImplementationType.IndexOf("RemoteCard", StringComparison.Ordinal) >= 0) == false,
                "local graph must not contain RemoteCard");
        }

        private static void TestEntityCardEmit() {
            var compilation = Compile(EntityCardSource, extraSource: EntityCardAssets);
            var document = WalkBound(compilation);
            var references = ReferenceSymbols.Create(compilation);
            Require(references != null, "references");
            var emitted = new List<string>();
            var graphs = EdgeResolver.Resolve(document, compilation, references!);
            var localClass = "";
            var remoteClass = "";
            for (var i = 0; i < graphs.Count; i++) {
                if (ScopeEmitter.TryEmit(graphs[i], document, compilation, references!, out _, out var source) == false)
                    continue;
                Require(source.IndexOf("IResolvePlan", StringComparison.Ordinal) < 0, "entity class used IResolvePlan");
                emitted.Add(source);
                if (source.IndexOf("CardFactoryBuildLocalContainer", StringComparison.Ordinal) >= 0)
                    localClass = source;
                if (source.IndexOf("CardFactoryBuildRemoteContainer", StringComparison.Ordinal) >= 0)
                    remoteClass = source;
            }

            Require(string.IsNullOrEmpty(localClass) == false, "missing CardFactoryBuildLocalContainer; got " + emitted.Count);
            Require(string.IsNullOrEmpty(remoteClass) == false, "missing CardFactoryBuildRemoteContainer");
            Require(localClass.IndexOf("CardPointerHandler", StringComparison.Ordinal) >= 0, "local class missing CardPointerHandler");
            Require(remoteClass.IndexOf("CardRevealView", StringComparison.Ordinal) >= 0, "remote class missing CardRevealView");
            Require(localClass.IndexOf("GeneratedScopes.Register", StringComparison.Ordinal) >= 0, "local must register");
            Require(localClass.IndexOf("+Local", StringComparison.Ordinal) >= 0, "local rootId must include variant");
            Require(remoteClass.IndexOf("+Remote", StringComparison.Ordinal) >= 0, "remote rootId must include variant");
        }

        private static void TestEntityPlayerStrippedBoard() {
            var compilation = Compile(EntityPlayerSource, extraSource: EntityPlayerAssets);
            var document = WalkBound(compilation);
            var local = RequireRoot(document, "+Local");
            Require(local.ViewType.IndexOf("LocalPlayerView", StringComparison.Ordinal) >= 0, "player view " + local.ViewType);
            Require(local.Calls.Exists(c => c.IndexOf("Board.Register", StringComparison.Ordinal) >= 0),
                "LocalPlayerView must bind stripped Board.Register; calls=" + string.Join(",", local.Calls));
            Require(local.Calls.Exists(c => c.IndexOf("HandView.Register", StringComparison.Ordinal) >= 0),
                "LocalPlayerView must bind HandView.Register");
            var references = ReferenceSymbols.Create(compilation);
            var graph = EdgeResolver.ResolveRoot(document, local, compilation, references!);
            FindRegistration(graph, "Board");
            FindRegistration(graph, "HandView");
        }

        private static void TestCingr006DuplicateView() {
            var compilation = Compile(Cingr006Source);
            var index = EntityAssetIndex.Read(compilation);
            var diagnostic = index.Diagnostics.Find(d => d.Descriptor.Id == "CINGR006");
            Require(diagnostic != null, "expected CINGR006; got " +
                                        string.Join(", ", index.Diagnostics.ConvertAll(d => d.Descriptor.Id)));
            var message = diagnostic!.ToDiagnostic().GetMessage();
            Require(message.IndexOf("Assets/A.prefab", StringComparison.Ordinal) >= 0, "CINGR006 first path: " + message);
            Require(message.IndexOf("Assets/B.prefab", StringComparison.Ordinal) >= 0, "CINGR006 second path: " + message);
            Require(message.IndexOf("subtype", StringComparison.OrdinalIgnoreCase) >= 0, "CINGR006 must say to create a subtype: " + message);
        }

        private static void TestCingr006BaseViewSkipped() {
            var compilation = Compile(Cingr006BaseSource);
            var index = EntityAssetIndex.Read(compilation);
            for (var i = 0; i < index.Diagnostics.Count; i++) {
                if (index.Diagnostics[i].Descriptor.Id == "CINGR006")
                    throw new Exception("base view type must not raise CINGR006: " +
                                        index.Diagnostics[i].ToDiagnostic().GetMessage());
            }
        }

        private static void TestCingr006SceneFactorySkipped() {
            var compilation = Compile(Cingr006SceneFactorySource);
            var index = EntityAssetIndex.Read(compilation);
            for (var i = 0; i < index.Diagnostics.Count; i++) {
                if (index.Diagnostics[i].Descriptor.Id == "CINGR006")
                    throw new Exception("SceneServicesFactory must not raise CINGR006: " +
                                        index.Diagnostics[i].ToDiagnostic().GetMessage());
            }
        }

        private static void TestCingr005Unenumerable() {
            var compilation = Compile(Cingr005Source);
            var document = WalkBound(compilation);
            var diagnostic = RequireDocumentDiagnostic(document, "CINGR005");
            var message = diagnostic.ToDiagnostic().GetMessage();
            Require(message.IndexOf("cardId", StringComparison.Ordinal) >= 0, "CINGR005 condition: " + message);
            Require(message.IndexOf("ContainerRuntimeScope", StringComparison.Ordinal) >= 0, "CINGR005 must mention attribute: " + message);
        }

        private static GraphDocument WalkBound(Compilation compilation) {
            var document = WalkDocument(compilation, harvest: false);
            var assets = EntityAssetIndex.Read(compilation);
            EntityAssets.Bind(document, assets);
            return document;
        }

        private static GraphMethod RequireRoot(GraphDocument document, string hint) {
            GraphMethod? match = null;
            for (var i = 0; i < document.Methods.Count; i++) {
                var method = document.Methods[i];
                if (method.IsRoot == false)
                    continue;
                if (method.Id.IndexOf(hint, StringComparison.Ordinal) < 0)
                    continue;
                if (match != null)
                    throw new Exception("ambiguous root " + hint + " " + match.Id + " vs " + method.Id);
                match = method;
            }

            if (match == null)
                throw new Exception("no root " + hint + "; roots=" + Roots(document));
            return match;
        }

        private static string EmitRoot(string source, string hint, out ScopeGraph graph) {
            var compilation = Compile(source);
            var references = ReferenceSymbols.Create(compilation);
            Require(references != null, "ReferenceSymbols.Create returned null");
            var document = new GraphWalker(compilation, references!).Walk();
            var resolved = EdgeResolver.ResolveByHint(document, compilation, references!, hint);
            Require(resolved != null, "no root matching " + hint + "; roots: " + Roots(document));
            graph = resolved!;
            Require(ScopeEmitter.TryEmit(graph, document, compilation, references!, out var hintName, out var emitted),
                "emit failed for " + hint + " diagnostics=" +
                string.Join(", ", graph.Diagnostics.Select(d => d.Descriptor.Id + " " + d.ToDiagnostic().GetMessage())));
            Require(string.IsNullOrEmpty(hintName) == false, "empty hint name");
            return emitted;
        }

        private static void RequireCompiles(string generated, string fixture) {
            var options = new CSharpParseOptions(LanguageVersion.Latest);
            var trees = new[] {
                CSharpSyntaxTree.ParseText(Stubs, options, "Stubs.cs"),
                CSharpSyntaxTree.ParseText(fixture, options, "Fixture.cs"),
                CSharpSyntaxTree.ParseText(generated, options, "Generated.cs"),
            };
            var compilation = CSharpCompilation.Create(
                "VerifyEmitAssembly",
                trees,
                MetadataRefs(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var failures = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString())
                .ToArray();
            Require(failures.Length == 0, "generated class does not compile:\n" + string.Join("\n", failures) +
                                          "\n--- generated ---\n" + generated);
        }

        private static string Head(string value) {
            if (value.Length <= 800)
                return value;
            return value.Substring(0, 800);
        }

        private static void DumpEmit(ScopeGraph graph, string emitted) {
            Dump(graph);
            var lines = emitted.Split('\n');
            var count = Math.Min(12, lines.Length);
            for (var i = 0; i < count; i++)
                Console.WriteLine("      | " + lines[i].TrimEnd());
        }

        private static ScopeGraph ResolveRoot(string source, string hint) {
            var compilation = Compile(source);
            var references = ReferenceSymbols.Create(compilation);
            Require(references != null, "ReferenceSymbols.Create returned null");
            var document = new GraphWalker(compilation, references!).Walk();
            var graph = EdgeResolver.ResolveByHint(document, compilation, references!, hint);
            Require(graph != null, "no root matching " + hint + "; roots: " + Roots(document));
            return graph!;
        }

        private static string Roots(GraphDocument document) {
            var ids = new List<string>();
            for (var i = 0; i < document.Methods.Count; i++) {
                if (document.Methods[i].IsRoot)
                    ids.Add(document.Methods[i].Id);
            }

            return string.Join(", ", ids);
        }

        private static MetadataReference EmitStubs(string assemblyName) {
            var options = new CSharpParseOptions(LanguageVersion.Latest);
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { CSharpSyntaxTree.ParseText(Stubs, options, "Stubs.cs") },
                MetadataRefs(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var failures = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString())
                .ToArray();
            Require(failures.Length == 0, assemblyName + " stubs do not compile:\n" + string.Join("\n", failures));
            var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Require(result.Success, assemblyName + " stubs emit failed");
            stream.Position = 0;
            return MetadataReference.CreateFromStream(stream);
        }

        private static Compilation Compile(
            string source,
            string assemblyName = "VerifyAssembly",
            IReadOnlyList<MetadataReference>? extra = null,
            string? extraSource = null,
            bool includeStubs = true) {
            var options = new CSharpParseOptions(LanguageVersion.Latest);
            var trees = new List<SyntaxTree>();
            if (includeStubs)
                trees.Add(CSharpSyntaxTree.ParseText(Stubs, options, "Stubs.cs"));
            trees.Add(CSharpSyntaxTree.ParseText(source, options, "Fixture.cs"));
            if (string.IsNullOrEmpty(extraSource) == false)
                trees.Add(CSharpSyntaxTree.ParseText(extraSource, options, "Extra.cs"));

            var refs = new List<MetadataReference>(MetadataRefs());
            if (extra != null)
                refs.AddRange(extra);

            var compilation = CSharpCompilation.Create(
                assemblyName,
                trees,
                refs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var failures = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString())
                .ToArray();
            Require(failures.Length == 0, assemblyName + " does not compile:\n" + string.Join("\n", failures));
            return compilation;
        }

        private static GraphDocument WalkDocument(Compilation compilation, bool harvest = true) {
            var references = ReferenceSymbols.Create(compilation);
            Require(references != null, "ReferenceSymbols.Create returned null");
            ManifestHarvest? harvested = null;
            var walkCompilation = compilation;
            var walkReferences = references!;
            if (harvest) {
                harvested = ManifestHarvest.TryBuild(compilation, references!);
                if (harvested != null && string.IsNullOrEmpty(harvested.Source) == false) {
                    walkCompilation = compilation.AddSyntaxTrees(
                        CSharpSyntaxTree.ParseText(harvested.Source, path: "ContainerInstallerHarvest.g.cs"));
                    walkReferences = ReferenceSymbols.Create(walkCompilation) ?? references!;
                }
            }

            var document = new GraphWalker(walkCompilation, walkReferences).Walk();
            if (harvest)
                ManifestHarvest.Remap(document, harvested);
            return document;
        }

        private static MetadataReference EmitAssembly(Compilation compilation, string generated, string assemblyName) {
            var trees = new List<SyntaxTree> {
                CSharpSyntaxTree.ParseText(generated, path: "ContainerGraph.g.cs"),
            };
            var attributeClass = GraphEmitter.EmitAttributeClass(compilation);
            if (attributeClass != null)
                trees.Add(CSharpSyntaxTree.ParseText(attributeClass, path: "ContainerInstallerAttribute.g.cs"));
            var withGenerated = compilation.AddSyntaxTrees(trees);
            var failures = withGenerated.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString())
                .ToArray();
            Require(failures.Length == 0, assemblyName + " generated source does not compile:\n" +
                                          string.Join("\n", failures) + "\n---\n" + Head(generated, 2000));
            var stream = new MemoryStream();
            var result = withGenerated.Emit(stream);
            Require(result.Success, assemblyName + " emit failed:\n" +
                                    string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            stream.Position = 0;
            return MetadataReference.CreateFromStream(stream);
        }

        private static GraphMethod FindMethod(GraphDocument document, string hint) {
            GraphMethod? match = null;
            for (var i = 0; i < document.Methods.Count; i++) {
                if (document.Methods[i].Id.IndexOf(hint, StringComparison.Ordinal) < 0)
                    continue;
                if (match != null)
                    throw new Exception("ambiguous method " + hint);
                match = document.Methods[i];
            }

            if (match == null)
                throw new Exception("no method " + hint + "; have " +
                                    string.Join(", ", document.Methods.ConvertAll(m => m.Id)));
            return match;
        }

        private static void RequireNoDocument(GraphDocument document, string id) {
            for (var i = 0; i < document.Diagnostics.Count; i++) {
                if (document.Diagnostics[i].Descriptor.Id == id)
                    throw new Exception("unexpected " + id + ": " + document.Diagnostics[i].ToDiagnostic().GetMessage());
            }
        }

        private static string Head(string value, int length) {
            if (value.Length <= length)
                return value;
            return value.Substring(0, length);
        }

        private static IReadOnlyList<MetadataReference> MetadataRefs() {
            var list = new List<MetadataReference>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Add(typeof(object).Assembly);
            Add(typeof(Console).Assembly);
            Add(typeof(List<>).Assembly);
            Add(typeof(Enumerable).Assembly);
            var runtime = Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location) ?? "", "System.Runtime.dll");
            if (File.Exists(runtime) && seen.Add(runtime))
                list.Add(MetadataReference.CreateFromFile(runtime));
            return list;

            void Add(Assembly assembly) {
                if (string.IsNullOrEmpty(assembly.Location))
                    return;
                if (seen.Add(assembly.Location) == false)
                    return;
                list.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        private static GraphEdge RequireEdge(ScopeGraph graph, string implementation, string parameter) {
            var registration = FindRegistration(graph, implementation);
            for (var i = 0; i < registration.Dependencies.Count; i++) {
                if (registration.Dependencies[i].ParameterName == parameter)
                    return registration.Dependencies[i];
            }

            var names = string.Join(", ", registration.Dependencies.Select(e => e.ParameterName + ":" + e.Kind));
            throw new Exception(implementation + " has no parameter '" + parameter + "'; have [" + names + "]");
        }

        private static GraphRegistration FindRegistration(ScopeGraph graph, string implementation) {
            for (var i = 0; i < graph.Registrations.Count; i++) {
                if (graph.Registrations[i].ImplementationType.IndexOf(implementation, StringComparison.Ordinal) >= 0)
                    return graph.Registrations[i];
            }

            throw new Exception("no registration " + implementation + " in " + graph.RootId);
        }

        private static DiagnosticInfo RequireDiagnostic(ScopeGraph graph, string id) {
            for (var i = 0; i < graph.Diagnostics.Count; i++) {
                if (graph.Diagnostics[i].Descriptor.Id == id)
                    return graph.Diagnostics[i];
            }

            throw new Exception("missing " + id + "; diagnostics: " +
                                string.Join(", ", graph.Diagnostics.Select(d => d.Descriptor.Id + " " + d.ToDiagnostic().GetMessage())));
        }

        private static DiagnosticInfo RequireDocumentDiagnostic(GraphDocument document, string id) {
            for (var i = 0; i < document.Diagnostics.Count; i++) {
                if (document.Diagnostics[i].Descriptor.Id == id)
                    return document.Diagnostics[i];
            }

            throw new Exception("missing " + id + "; diagnostics: " +
                                string.Join(", ", document.Diagnostics.Select(d => d.Descriptor.Id + " " + d.ToDiagnostic().GetMessage())));
        }

        private static void RequireNo(ScopeGraph graph, string id) {
            for (var i = 0; i < graph.Diagnostics.Count; i++) {
                if (graph.Diagnostics[i].Descriptor.Id == id)
                    throw new Exception("unexpected " + id + ": " + graph.Diagnostics[i].ToDiagnostic().GetMessage());
            }
        }

        private static void RequireOrder(ScopeGraph graph, string first, string second) {
            var a = graph.Registrations.FindIndex(r => r.ImplementationType.IndexOf(first, StringComparison.Ordinal) >= 0);
            var b = graph.Registrations.FindIndex(r => r.ImplementationType.IndexOf(second, StringComparison.Ordinal) >= 0);
            Require(a >= 0 && b >= 0, "order types missing " + first + "/" + second);
            var ia = graph.ConstructionOrder.IndexOf(a);
            var ib = graph.ConstructionOrder.IndexOf(b);
            Require(ia >= 0 && ib >= 0 && ia < ib, first + " must construct before " + second + " (" + ia + "," + ib + ")");
        }

        private static void Require(bool condition, string message) {
            if (condition == false)
                throw new Exception(message);
        }

        private static void Dump(ScopeGraph graph) {
            var edges = 0;
            for (var i = 0; i < graph.Registrations.Count; i++)
                edges += graph.Registrations[i].Dependencies.Count;
            Console.WriteLine("      root=" + graph.RootId + " regs=" + graph.Registrations.Count +
                              " edges=" + edges + " order=" + graph.ConstructionOrder.Count);
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                var deps = string.Join(", ", registration.Dependencies.Select(Describe));
                Console.WriteLine("      [" + i + "] " + Short(registration.ImplementationType) +
                                  " " + registration.Origin + (deps.Length == 0 ? "" : " <- " + deps));
            }
        }

        private static string Describe(GraphEdge edge) {
            var target = edge.Kind == "Collection"
                ? string.Join("|", edge.CollectionIndices)
                : edge.TargetIndex.ToString();
            return edge.ParameterName + ":" + edge.Kind + (edge.Kind == "Registration" || edge.Kind == "Collection" ? "#" + target : "");
        }

        private static string Short(string type) {
            if (string.IsNullOrEmpty(type))
                return "(none)";
            var last = type.LastIndexOf('.');
            return last < 0 ? type : type.Substring(last + 1);
        }

        private const string Stubs = @"
using System;
using System.Collections.Generic;

namespace Internal {
    public interface IInjector {}
    public interface IResolvePlan {}
    public interface IReadOnlyLifetime {}
    public interface ILifetime : IReadOnlyLifetime { void Terminate(); }
    public interface IServiceRegistration {
        System.Type ImplementationType { get; }
        ServiceLifetime Lifetime { get; }
        IServiceRegistration As(System.Type serviceType);
        IServiceRegistration AsSelf();
        IServiceRegistration WithParameter(System.Type type, object value);
    }
    public interface IContainerRegistry {
        IServiceRegistration Add(System.Type implementation, ServiceLifetime lifetime);
        IServiceRegistration AddInstance(System.Type serviceType, object instance);
        void AddInjection(object target);
        void AddSelfResolvable(IServiceRegistration registration);
    }
    public interface IContainerBuilderScope : IContainerRegistry {
        IContainer Build();
        void AddLoadedAsset(string label, string groupName);
    }
    public interface IContainerDiagnostics {
        string Name { get; }
        bool IsGenerated { get; }
        IContainerDiagnostics Parent { get; }
        System.Collections.Generic.IReadOnlyList<IContainerDiagnostics> Children { get; }
        System.Collections.Generic.IReadOnlyList<RegistrationInfo> Registrations { get; }
        System.Collections.Generic.IReadOnlyList<int> BuildOrder { get; }
        System.Collections.Generic.IReadOnlyList<LoadedAssetInfo> LoadedAssets { get; }
        bool IsHistoryEnabled { get; set; }
        System.Collections.Generic.IReadOnlyList<ResolveRecord> History { get; }
    }
    public readonly struct RegistrationInfo {}
    public readonly struct LoadedAssetInfo {}
    public readonly struct ResolveRecord {}
    public interface IContainer : System.IDisposable {
        IContainerDiagnostics Diagnostics { get; }
        IReadOnlyLifetime Lifetime { get; }
        object Resolve(System.Type type);
        T Resolve<T>();
        bool TryResolve(System.Type type, out object instance);
        System.Collections.Generic.IReadOnlyList<T> ResolveAll<T>();
        void Inject(object target);
        void InjectGameObject(UnityEngine.GameObject target);
        IContainerBuilderScope CreateChild();
    }
    public interface IBuilder {
        IReadOnlyLifetime Lifetime { get; }
    }
    public interface IScopeBuilder : IBuilder {
        ILifetime ScopeLifetime { get; }
    }
    public interface IEntityBuilder : IBuilder {
        ILifetime ScopeLifetime { get; }
    }
    public interface IRegistration {}
    public interface IEntityComponent { void Register(IEntityBuilder builder); }
    public interface ISceneService { void Create(IScopeBuilder builder); }
    public interface IScopeEntityView {}
    public interface IEntityScopeLoader {
        void Load(IReadOnlyLifetime lifetime, object parent, IScopeEntityView view, System.Action<IEntityBuilder> construct);
    }
    [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class ContainerGraphAssetAttribute : System.Attribute {
        public ContainerGraphAssetAttribute(string assetPath, string holderType, string[] componentTypes) {}
    }
    public enum ServiceLifetime { Transient, Scoped, Singleton }
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class ContainerRuntimeScopeAttribute : System.Attribute {}
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class ContainerGraphRootAttribute : System.Attribute {}
    public sealed class GeneratedScopeRequest {
        public ILifetime Lifetime { get; }
        public T Get<T>() { return default(T); }
    }
    public static class GeneratedScopes {
        public static void Register(string rootId, System.Func<GeneratedScopeRequest, IContainer> factory) {}
        public static bool IsRegistered(string rootId) { return false; }
    }
    public static class ScopeContainer {
        public static IContainerBuilderScope CreateChild(IContainer parent) { return null; }
    }

    public static class BuilderExtensions {
        public static IRegistration Register<T>(this IBuilder builder, ServiceLifetime lifetime = ServiceLifetime.Singleton) { return null; }
        public static IRegistration Register<TInterface, TImplementation>(this IBuilder builder, ServiceLifetime lifetime = ServiceLifetime.Singleton) { return null; }
        public static IRegistration RegisterInstance<T>(this IBuilder builder, T instance) { return null; }
        public static IRegistration RegisterComponent<T>(this IBuilder builder, T component, ServiceLifetime lifetime = ServiceLifetime.Singleton) { return null; }
        public static IRegistration As<T>(this IRegistration registration) { return registration; }
        public static IRegistration AsSelf(this IRegistration registration) { return registration; }
        public static IRegistration AsSelfResolvable(this IRegistration registration) { return registration; }
        public static IRegistration WithParameter<T>(this IRegistration registration, T value) { return registration; }
        public static IRegistration WithScopeLifetime(this IRegistration registration) { return registration; }
        public static void Inject<T>(this IBuilder builder, T target) {}
        public static T Instantiate<T>(this IScopeBuilder builder, T prefab) { return prefab; }
    }
}

namespace UnityEngine {
    public class Object {}
    public class MonoBehaviour : Object {}
    public class GameObject : Object {
        public T[] GetComponentsInChildren<T>(bool includeInactive) { return new T[0]; }
    }
}
";

        private const string LoadGlobalSource = @"
using Internal;

namespace Global {
    public static class GlobalPrefabs {
        public static Setup.Updater GlobalUpdater;
        public static Setup.GlobalCamera GlobalCamera;
        public static Setup.LoadingScreen LoadingScreen;
    }
}

namespace Global.Setup {
    public interface IUpdater {}
    public interface IDelayRunner {}
    public interface ICurrentCamera {}
    public interface ICameraUtils {}
    public interface IInputConstraintsStorage {}
    public interface IUIStateMachine {}
    public interface IItchLanguageAPI {}
    public interface ISystemLanguageProvider {}
    public interface IBackendGet {}
    public interface IBackendPost {}
    public interface IBackendMedia {}
    public interface IBackendClient {}

    public class Updater : IUpdater {}
    public class GlobalCamera {}
    public class DelayRunner : IDelayRunner {
        public DelayRunner(IUpdater updater) {}
    }
    public class CurrentCamera : ICurrentCamera {}
    public class CameraUtils : ICameraUtils {
        public CameraUtils(ICurrentCamera camera) {}
    }
    public class InputConstraintsStorage : IInputConstraintsStorage {}
    public class UIStateMachine : IUIStateMachine {
        public UIStateMachine(IInputConstraintsStorage constraintsStorage, Internal.IReadOnlyLifetime lifetime) {}
    }
    public class LoadingScreen {
        public void Construct(IUpdater updater) {}
    }
    public class ItchLanguageDebugAPI : IItchLanguageAPI {}
    public class ItchLanguageExternAPI : IItchLanguageAPI {}
    public class ItchLanguageProvider : ISystemLanguageProvider {
        public ItchLanguageProvider(IItchLanguageAPI api) {}
    }
    public class BackendGet : IBackendGet {}
    public class BackendPost : IBackendPost {}
    public class BackendMedia : IBackendMedia {}
    public class BackendOptions {}
    public class BackendClient : IBackendClient {
        public BackendClient(IBackendGet get, IBackendPost post, IBackendMedia media, BackendOptions options) {}
    }

    public static class GlobalScopeExtensions {
        public static void LoadGlobal(Internal.IScopeBuilder loader) {
            Construct(loader);

            void Construct(Internal.IScopeBuilder builder) {
                builder.AddUpdater();
                builder.AddCamera();
                builder.AddInput();
                builder.AddPublisher();
                builder.AddBackend();
                builder.AddUI();
            }
        }

        public static Internal.IScopeBuilder AddUpdater(this Internal.IScopeBuilder builder) {
            var updater = builder.Instantiate(Global.GlobalPrefabs.GlobalUpdater);
            builder.RegisterComponent(updater).As<IUpdater>().AsSelfResolvable();
            builder.Register<DelayRunner>().As<IDelayRunner>();
            return builder;
        }

        public static Internal.IScopeBuilder AddCamera(this Internal.IScopeBuilder builder) {
            builder.Register<CurrentCamera>().As<ICurrentCamera>();
            var camera = builder.Instantiate(Global.GlobalPrefabs.GlobalCamera);
            builder.RegisterComponent(camera);
            builder.Register<CameraUtils>().As<ICameraUtils>();
            return builder;
        }

        public static Internal.IScopeBuilder AddInput(this Internal.IScopeBuilder builder) {
            builder.Register<InputConstraintsStorage>().As<IInputConstraintsStorage>();
            return builder;
        }

        public static Internal.IScopeBuilder AddPublisher(this Internal.IScopeBuilder builder) {
            var isEditor = true;
            if (isEditor == true)
                builder.Register<ItchLanguageDebugAPI>().As<IItchLanguageAPI>();
            else
                builder.Register<ItchLanguageExternAPI>().As<IItchLanguageAPI>();
            builder.Register<ItchLanguageProvider>().As<ISystemLanguageProvider>();
            return builder;
        }

        public static Internal.IScopeBuilder AddBackend(this Internal.IScopeBuilder builder) {
            builder.Register<BackendGet>().As<IBackendGet>();
            builder.Register<BackendPost>().As<IBackendPost>();
            builder.Register<BackendMedia>().As<IBackendMedia>();
            builder.RegisterInstance(new BackendOptions());
            builder.Register<BackendClient>().As<IBackendClient>();
            return builder;
        }

        public static Internal.IScopeBuilder AddUI(this Internal.IScopeBuilder builder) {
            builder.Register<UIStateMachine>().WithScopeLifetime().As<IUIStateMachine>();
            var loadingScreen = builder.Instantiate(Global.GlobalPrefabs.LoadingScreen);
            builder.Inject(loadingScreen);
            return builder;
        }
    }
}
";

        private const string CardFactorySource = @"
using Internal;

namespace GamePlay.Cards {
    public interface IGameContext {}
    public interface IGamePlayer {}
    public interface IHand {}
    public interface ICard {}
    public interface ICardContext {}
    public interface ICardDropArea {}
    public interface ICardStateLifetime {}
    public interface ICardDropDetector {}
    public interface IHandEntryHandle {}
    public interface ICardConfig {}
    public interface ICardDefinition {}
    public interface IGameInput {}
    public interface IUpdater {}
    public class GameContext : IGameContext {}
    public class GamePlayer : IGamePlayer {}
    public class Hand : IHand {}
    public class CardConfig : ICardConfig {}
    public class CardDefinition : ICardDefinition {}
    public class GameInput : IGameInput {}
    public class Updater : IUpdater {}

    public class CardContext : ICardContext {
        public CardContext(IGameContext gameContext, ICardDefinition definition, ICardConfig config) {}
    }
    public class CardDropArea : ICardDropArea {
        public CardDropArea(IUpdater updater, IGameInput input, IGameContext gameContext, ICardContext context) {}
    }
    public class CardStateLifetime : ICardStateLifetime {
        public CardStateLifetime(Internal.IReadOnlyLifetime lifetime) {}
    }
    public class CardDropDetector : ICardDropDetector {
        public CardDropDetector(IGameInput input, IGameContext gameContext) {}
    }
    public class LocalCard : ICard {
        public LocalCard(System.Guid id, Internal.ILifetime containerLifetime, IHand hand, ICardDefinition definition) {}
    }
    public class HandEntryHandle : IHandEntryHandle {
        public HandEntryHandle(IHand hand, ICard card) {}
    }

    public static class CardComponentsExtensions {
        public static Internal.IEntityBuilder AddCardLocalComponents(this Internal.IEntityBuilder builder) {
            builder.Register<CardDropDetector>().As<ICardDropDetector>();
            builder.Register<CardStateLifetime>().WithParameter(builder.Lifetime).As<ICardStateLifetime>();
            builder.Register<CardDropArea>().As<ICardDropArea>();
            builder.Register<CardContext>().As<ICardContext>();
            return builder;
        }
    }

    public static class CardRootExtensions {
        public static Internal.IEntityBuilder AddCardLocalRoot(this Internal.IEntityBuilder builder) {
            builder.Register<LocalCard>().WithParameter(builder.ScopeLifetime).As<ICard>();
            return builder;
        }
    }

    public class CardFactory {
        public void Create(bool isLocal, System.Guid cardId) {
            Build(null);

            void Build(Internal.IEntityBuilder builder) {
                builder.RegisterInstance(cardId);
                builder.AddCardLocalComponents();
                builder.AddCardLocalRoot();
                builder.RegisterInstance(new CardConfig()).As<ICardConfig>();
                builder.RegisterInstance(new GameContext()).As<IGameContext>();
                builder.RegisterInstance(new GamePlayer()).As<IGamePlayer>();
                builder.RegisterInstance(new Hand()).As<IHand>();
                builder.RegisterInstance(new GameInput()).As<IGameInput>();
                builder.RegisterInstance(new Updater()).As<IUpdater>();
                builder.Register<HandEntryHandle>().As<IHandEntryHandle>();
                builder.RegisterInstance(new CardDefinition()).As<ICardDefinition>();
            }
        }
    }
}
";

        private const string MissingSource = @"
using Internal;

namespace Sample {
    public class UnregisteredDependency {}
    public class NeedsMissingCtor {
        public NeedsMissingCtor(UnregisteredDependency dependency) {}
    }
    public static class MissingRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<NeedsMissingCtor>();
        }
    }
}
";

        private const string CycleSource = @"
using Internal;

namespace Sample {
    public class CycleA { public void Construct(CycleB b) {} }
    public class CycleB { public void Construct(CycleC c) {} }
    public class CycleC { public void Construct(CycleA a) {} }
    public static class CycleRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<CycleA>();
            builder.Register<CycleB>();
            builder.Register<CycleC>();
        }
    }
}
";

        private const string ManifestLibSource = @"
using Internal;

namespace Internal {
    public interface INetworkConnection {}
    public interface INetworkCommandsCollection {}
    public interface INetworkObjectsCollection {}
    public interface INetworkEntityFactory {}
    public interface ISessionConnection {}
    public interface INetworkEntity {}
    public interface INetworkUser {}
    public class NetworkConnection : INetworkConnection {}
    public class NetworkCommandsCollection : INetworkCommandsCollection {}
    public class NetworkCommandsDispatcher {}
    public class NetworkObjectsCollection : INetworkObjectsCollection {}
    public class NetworkEntityFactory : INetworkEntityFactory {}
    public class SessionConnection : ISessionConnection {}
    public class NetworkEntity : INetworkEntity {
        public NetworkEntity(INetworkUser owner, int id) {}
    }
    public class RemoteEntityData {
        public INetworkUser Owner;
        public int Id;
    }

    public static class NetworkConnectionExtensions {
        public static IScopeBuilder AddNetworkConnection(this IScopeBuilder builder) {
            builder.Register<NetworkConnection>().As<INetworkConnection>().AsSelf();
            builder.Register<NetworkCommandsCollection>().As<INetworkCommandsCollection>();
            builder.Register<NetworkCommandsDispatcher>();
            return builder;
        }
    }

    public static class SessionServicesExtensions {
        public static IScopeBuilder AddSessionServices(this IScopeBuilder builder) {
            AddEntityServices();
            AddConnectionServices();
            return builder;

            void AddEntityServices() {
                builder.Register<NetworkObjectsCollection>().As<INetworkObjectsCollection>();
                builder.Register<NetworkEntityFactory>().As<INetworkEntityFactory>();
            }

            void AddConnectionServices() {
                builder.AddNetworkConnection();
                builder.Register<SessionConnection>().As<ISessionConnection>();
            }
        }
    }

    public static class NetworkEntityExtensions {
        public static IEntityBuilder AddRemoteEntity(this IEntityBuilder builder, RemoteEntityData data) {
            builder.Register<NetworkEntity>()
                   .WithParameter(data.Owner)
                   .WithParameter(data.Id)
                   .As<INetworkEntity>();
            return builder;
        }
    }
}
";

        private const string ManifestConsumerSource = @"
using Internal;

namespace GamePlay.Loop {
    public class GamePlayLoop {}
    public static class GamePlayScopeExtensions {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.AddSessionServices();
            builder.AddNetworkConnection();
            builder.Register<GamePlayLoop>();
        }
    }

    public static class GamePlayerFactory {
        public static void Build(Internal.IEntityBuilder builder, RemoteEntityData data) {
            builder.AddRemoteEntity(data);
        }
    }
}
";

        private const string TransientSource = @"
using Internal;

namespace Sample {
    public class CameraUtils {}
    public class Popup {
        public Popup(CameraUtils cameraUtils) {}
    }
    public static class TransientRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<CameraUtils>();
            builder.Register<Popup>(Internal.ServiceLifetime.Transient);
        }
    }
}
";

        private const string RuntimeScopeSource = @"
using Internal;

namespace Sample {
    public class RuntimeService {}
    public static class RuntimeRoot {
        [ContainerRuntimeScope]
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<RuntimeService>();
        }
    }
}
";

        private const string CollectionSource = @"
using Internal;

namespace Sample {
    public interface IAchievementTier {}
    public class BronzeTier : IAchievementTier {}
    public class GoldTier : IAchievementTier {}
    public class AchievementRow {
        public AchievementRow(System.Collections.Generic.IReadOnlyList<IAchievementTier> tiers) {}
    }
    public static class CollectionRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<BronzeTier>().As<IAchievementTier>();
            builder.Register<GoldTier>().As<IAchievementTier>();
            builder.Register<AchievementRow>();
        }
    }
}
";

        private const string UncoveredSource = @"
using Internal;

namespace Sample {
    public class UncoveredService {}
    public static class UncoveredRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            lock (builder) {
                builder.Register<UncoveredService>();
            }
        }
    }
}
";

        private const string MarkerSource = @"
using Internal;

namespace Sample {
    public interface IScopeSetupMarker {}
    public class MarkerFirst : IScopeSetupMarker {}
    public class MarkerSecond : IScopeSetupMarker {}
    public class MarkerThird : IScopeSetupMarker {}
    public class MarkerConsumer {
        public MarkerConsumer(System.Collections.Generic.IReadOnlyList<IScopeSetupMarker> setups) {}
    }
    public static class MarkerRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<MarkerFirst>().As<IScopeSetupMarker>();
            builder.Register<MarkerSecond>().As<IScopeSetupMarker>();
            builder.Register<MarkerThird>().As<IScopeSetupMarker>();
            builder.Register<MarkerConsumer>();
        }
    }
}
";

        private const string EntityCardSource = @"
using Internal;

namespace GamePlay.Cards {
    public class ScopeEntityView : IScopeEntityView {}
    public class CardLocalScopeEntity : ScopeEntityView {}
    public class CardRemoteScopeEntity : ScopeEntityView {}
    public class LocalCard {}
    public class RemoteCard {}
    public class CardPointerHandler : IEntityComponent {
        public void Register(IEntityBuilder builder) { builder.RegisterComponent(this); }
    }
    public class CardRevealView : IEntityComponent {
        public void Register(IEntityBuilder builder) { builder.RegisterComponent(this); }
    }
    public static class GamePlayPrefabs {
        public static CardLocalScopeEntity CardLocal;
        public static CardRemoteScopeEntity CardRemote;
    }
    public class CardFactory {
        private readonly IEntityScopeLoader _loader = null;
        public void Create(bool isLocal) {
            if (isLocal == true)
                _loader.Load(null, null, GamePlayPrefabs.CardLocal, Build);
            else
                _loader.Load(null, null, GamePlayPrefabs.CardRemote, Build);

            void Build(IEntityBuilder builder) {
                if (isLocal == true)
                    builder.Register<LocalCard>();
                else
                    builder.Register<RemoteCard>();
            }
        }
    }
}
";

        private const string EntityCardAssets = @"
[assembly: Internal.ContainerGraphAsset(
    ""Assets/Card_Local.prefab"",
    ""GamePlay.Cards.CardLocalScopeEntity"",
    new string[] { ""GamePlay.Cards.CardPointerHandler"" })]
[assembly: Internal.ContainerGraphAsset(
    ""Assets/Card_Remote.prefab"",
    ""GamePlay.Cards.CardRemoteScopeEntity"",
    new string[] { ""GamePlay.Cards.CardRevealView"" })]
";

        private const string EntityPlayerSource = @"
using Internal;

namespace GamePlay.Players {
    public class ScopeEntityView : IScopeEntityView {}
    public class LocalPlayerView : ScopeEntityView {}
    public class RemotePlayerView : ScopeEntityView {}
    public class NetworkUser { public bool IsLocal; }
    public class RemoteEntityData { public NetworkUser Owner; }
    public class GamePlayer {}
}
namespace GamePlay.Boards {
    public class Board : Internal.IEntityComponent {
        public void Register(Internal.IEntityBuilder builder) { builder.RegisterComponent(this); }
    }
}
namespace GamePlay.Cards {
    public class HandView : Internal.IEntityComponent {
        public void Register(Internal.IEntityBuilder builder) { builder.RegisterComponent(this); }
    }
}
namespace GamePlay.Players {
    public class GamePlayerFactory {
        private readonly Internal.IEntityScopeLoader _loader = null;
        private readonly LocalPlayerView _local = null;
        private readonly RemotePlayerView _remote = null;
        public void Create(RemoteEntityData data) {
            if (data.Owner.IsLocal == true)
                _loader.Load(null, null, _local, Build);
            else
                _loader.Load(null, null, _remote, Build);

            void Build(Internal.IEntityBuilder builder) {
                builder.Register<GamePlayer>();
            }
        }
    }
}
";

        private const string EntityPlayerAssets = @"
[assembly: Internal.ContainerGraphAsset(
    ""Assets/Game_Field.unity"",
    ""GamePlay.Players.LocalPlayerView"",
    new string[] { ""GamePlay.Boards.Board"", ""GamePlay.Cards.HandView"" })]
[assembly: Internal.ContainerGraphAsset(
    ""Assets/Game_Field.unity"",
    ""GamePlay.Players.RemotePlayerView"",
    new string[] { ""GamePlay.Boards.Board"", ""GamePlay.Cards.HandView"" })]
";

        private const string Cingr006Source = @"
using Internal;

[assembly: Internal.ContainerGraphAsset(""Assets/A.prefab"", ""Sample.LeafView"", new string[] {})]
[assembly: Internal.ContainerGraphAsset(""Assets/B.prefab"", ""Sample.LeafView"", new string[] {})]

namespace Sample {
    public class LeafView {}
}
";

        private const string Cingr006BaseSource = @"
using Internal;

[assembly: Internal.ContainerGraphAsset(""Assets/A.prefab"", ""Sample.BaseView"", new string[] {})]
[assembly: Internal.ContainerGraphAsset(""Assets/B.prefab"", ""Sample.BaseView"", new string[] {})]

namespace Sample {
    public class BaseView {}
    public class ChildView : BaseView {}
}
";

        private const string Cingr006SceneFactorySource = @"
using Internal;

[assembly: Internal.ContainerGraphAsset(""Assets/A.unity"", ""Internal.SceneServicesFactory"", new string[] {})]
[assembly: Internal.ContainerGraphAsset(""Assets/B.unity"", ""Internal.SceneServicesFactory"", new string[] {})]
";

        private const string Cingr005Source = @"
using Internal;

namespace GamePlay.Cards {
    public class ScopeEntityView : IScopeEntityView {}
    public class CardLocalScopeEntity : ScopeEntityView {}
    public class LocalCard {}
    public class RemoteCard {}
    public class CardFactory {
        private readonly IEntityScopeLoader _loader = null;
        private readonly CardLocalScopeEntity _view = null;
        public void Create(System.Guid cardId) {
            _loader.Load(null, null, _view, Build);
            void Build(IEntityBuilder builder) {
                if (cardId.ToString() == ""x"")
                    builder.Register<LocalCard>();
                else
                    builder.Register<RemoteCard>();
            }
        }
    }
}
";
    }
}
