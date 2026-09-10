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
            Run("CINGR003 missing interface in own scope", TestMissingInterface);
            Run("parent export is typed hole not error", TestParentExportHole);
            Run("missing on self and parent is CINGR003", TestMissingOnSelfAndParent);
            Run("parent in other assembly via manifest", TestParentViaManifest);
            Run("generator driver Internal→Global→Menu", TestGeneratorDriverParentChain);
            Run("generator driver missing parent is CINGR007", TestGeneratorDriverMissingParent);
            Run("generator driver with source project references (IDE)", TestGeneratorDriverSourceReferences);
            Run("CINGR004 cycle path", TestCycle);
            Run("[Inject] picks method by attribute, not name", TestInjectAttribute);
            Run("IReadOnlyList FullyQualifiedFormat", TestCollectionTypeKey);
            Run("LoadGlobal emit compiles", TestLoadGlobalEmit);
            Run("Marker array order matches registration", TestMarkerOrderEmit);
            Run("Transient has method not field", TestTransientEmit);
            Run("Transient in marker list is created per ResolveAll", TestTransientMarkerEmit);
            Run("CINGR003 does not emit class", TestMissingDoesNotEmit);
            Run("CINGR001 uncovered syntax", TestUncoveredSyntax);
            Run("Marker collection order", TestMarkerCollectionOrder);
            Run("1b manifest uses assembly attributes", TestManifestEmitsAttributes);
            Run("registry AttachBuilder is not an installer", TestRegistryAttachIsNotInstaller);
            Run("1b cross-assembly installer no CINGR002", TestCrossAssemblyManifest);
            Run("1b harvested local functions keep order", TestHarvestLocalFunctionOrder);
            Run("harvest keeps repeated generic installer calls", TestHarvestRepeatedGenericInstaller);
            Run("1c entity Load splits CardFactory variants", TestEntityCardVariants);
            Run("1c entity emit two classes", TestEntityCardEmit);
            Run("1c separate local roots in one method get distinct classes", TestEntityCardSplitEmit);
            Run("1c GamePlayer variants bind stripped Board", TestEntityPlayerStrippedBoard);
            Run("1c CINGR006 duplicate view type names both paths", TestCingr006DuplicateView);
            Run("1c CINGR006 skips base view type", TestCingr006BaseViewSkipped);
            Run("1c CINGR006 skips SceneServicesFactory", TestCingr006SceneFactorySkipped);
            Run("1c CINGR005 non-enumerable variant condition", TestCingr005Unenumerable);
            Run("generic installer in same assembly", TestGenericInstallerSameAssembly);
            Run("generic installer via manifest", TestGenericInstallerViaManifest);
            Run(".As on installer return from other assembly", TestGenericInstallerReturnAs);
            Run("generic installer emit with nested type args", TestGenericInstallerNestedEmit);
            Run("generic RegisterCommand nested emit", TestGenericRegisterCommandEmit);
            Run("Injectable gets Inject case only", TestInjectableEmit);
            Run("Injectable CINGR008 and not resolvable", TestInjectableErrors);
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

        private static void TestMissingInterface() {
            var graph = ResolveRoot(MissingInterfaceSource, "MissingInterfaceRoot");
            var diagnostic = RequireDiagnostic(graph, "CINGR003");
            var message = diagnostic.ToDiagnostic().GetMessage();
            Require(message.IndexOf("missing", StringComparison.Ordinal) >= 0, "CINGR003 parameter: " + message);
            Require(message.IndexOf("IUnregisteredService", StringComparison.Ordinal) >= 0, "CINGR003 type: " + message);
        }

        private static void TestParentExportHole() {
            var graph = ResolveRoot(ParentHitSource, "ChildRoot");
            RequireNo(graph, "CINGR003");
            var edge = RequireEdge(graph, "NeedsParent", "dep");
            Require(edge.Kind == "Hole", "parent dep kind " + edge.Kind);
            Require(edge.ParentRootId.IndexOf("ParentRoot", StringComparison.Ordinal) >= 0,
                "parent source " + edge.ParentRootId);
        }

        private static void TestMissingOnSelfAndParent() {
            var graph = ResolveRoot(ParentMissSource, "ChildMissRoot");
            var diagnostic = RequireDiagnostic(graph, "CINGR003");
            var message = diagnostic.ToDiagnostic().GetMessage();
            Require(message.IndexOf("absent", StringComparison.Ordinal) >= 0, "CINGR003 parameter: " + message);
            Require(message.IndexOf("IAbsent", StringComparison.Ordinal) >= 0, "CINGR003 type: " + message);
        }

        private static void TestParentViaManifest() {
            var shared = EmitStubs("ParentHoleStubs");
            var parentCompilation = Compile(ParentManifestLibSource, "ParentLib", extra: new[] { shared }, includeStubs: false);
            var parentDocument = WalkDocument(parentCompilation, harvest: false);
            EdgeResolver.BindParents(parentDocument);
            var parentEmit = GraphEmitter.Emit(parentDocument, parentCompilation);
            var parentReference = EmitAssembly(parentCompilation, parentEmit, "ParentLib");

            var childCompilation = Compile(
                ParentManifestChildSource,
                "ChildLib",
                extra: new[] { shared, parentReference },
                includeStubs: false);
            var childDocument = WalkDocument(childCompilation, harvest: false);
            ManifestReader.Merge(childDocument, childCompilation);
            var references = ReferenceSymbols.Create(childCompilation);
            Require(references != null, "child ReferenceSymbols");
            var graph = EdgeResolver.ResolveByHint(childDocument, childCompilation, references!, "ChildManifestRoot");
            Require(graph != null, "ChildManifestRoot missing; roots=" + Roots(childDocument));
            RequireNo(graph!, "CINGR003");
            var edge = RequireEdge(graph!, "NeedsExported", "dep");
            Require(edge.Kind == "Hole", "manifest parent kind " + edge.Kind);
            Require(edge.ParentRootId.IndexOf("ParentManifestRoot", StringComparison.Ordinal) >= 0,
                "manifest parent source " + edge.ParentRootId);
        }

        private static void TestGeneratorDriverParentChain() {
            var internalCompilation = Compile(DriverInternalSource, "Internal");
            var internalRun = RunGenerator(internalCompilation, "Internal");
            RequireNoId(internalRun.Diagnostics, "CINGR001");
            RequireGeneratedContains(internalRun.Output, "[assembly: global::Internal.ContainerInstaller(");
            RequireGeneratedContains(internalRun.Output, "OptionsContainer");

            var globalCompilation = Compile(
                DriverGlobalSource,
                "Global",
                extra: new[] { internalRun.Reference },
                includeStubs: false);
            var globalRun = RunGenerator(globalCompilation, "Global");
            RequireNoId(globalRun.Diagnostics, "CINGR001");
            RequireNoId(globalRun.Diagnostics, "CINGR003");
            RequireNoId(globalRun.Diagnostics, "CINGR007");
            RequireGeneratedContains(internalRun.Output, "OptionsContainerRegisterContainer");
            RequireGeneratedContains(globalRun.Output, "Global.Setup.GlobalScopeExtensions.Construct");
            RequireGeneratedContains(globalRun.Output, "[assembly: global::Internal.ContainerInstaller(");
            RequireGeneratedMissing(globalRun.Output, "OptionsContainerRegisterContainer");

            var menuCompilation = Compile(
                DriverMenuSource,
                "Menu",
                extra: new[] { internalRun.Reference, globalRun.Reference },
                includeStubs: false);
            var menuRun = RunGenerator(menuCompilation, "Menu");
            RequireNoId(menuRun.Diagnostics, "CINGR001");
            RequireNoId(menuRun.Diagnostics, "CINGR003");
            RequireNoId(menuRun.Diagnostics, "CINGR007");
            RequireGeneratedContains(menuRun.Output, "MenuScopeExtensionsConstructContainer");
            Console.WriteLine("      Internal→Global→Menu through ContainerGraphGenerator");
        }

        // IDE (Rider) передаёт соседние проекты как компиляции: символы их инсталлеров приходят
        // с чужими syntax tree, а не как метаданные, как у Unity.
        private static void TestGeneratorDriverSourceReferences() {
            var internalRun = RunGenerator(Compile(DriverInternalSource, "Internal"), "Internal");
            var internalSource = internalRun.Output.ToMetadataReference();

            var globalCompilation = Compile(
                IdeGlobalSource,
                "Global",
                extra: new[] { internalSource },
                includeStubs: false);
            var globalRun = RunGenerator(globalCompilation, "Global");
            RequireNoId(globalRun.Diagnostics, "CINGR001");
            RequireGeneratedContains(globalRun.Output, "Global.Setup.GlobalScopeExtensions.Construct");

            var menuCompilation = Compile(
                IdeMenuSource,
                "Menu",
                extra: new[] { internalSource, globalRun.Output.ToMetadataReference() },
                includeStubs: false);
            var menuRun = RunGenerator(menuCompilation, "Menu");
            RequireNoId(menuRun.Diagnostics, "CINGR001");
            RequireNoId(menuRun.Diagnostics, "CINGR003");
            RequireNoId(menuRun.Diagnostics, "CINGR007");
            RequireGeneratedContains(menuRun.Output, "MenuScopeExtensionsConstructContainer");
            RequireGeneratedContains(menuRun.Output, "global::Global.Setup.Clock");
        }

        private static void TestGeneratorDriverMissingParent() {
            var internalCompilation = Compile(DriverInternalSource, "Internal");
            var internalRun = RunGenerator(internalCompilation, "Internal");
            var globalCompilation = Compile(
                DriverGlobalSource,
                "Global",
                extra: new[] { internalRun.Reference },
                includeStubs: false);
            var globalBare = EmitPe(globalCompilation, "GlobalBare");

            var menuCompilation = Compile(
                DriverMenuSource,
                "Menu",
                extra: new[] { internalRun.Reference, globalBare },
                includeStubs: false);
            var menuRun = RunGenerator(menuCompilation, "MenuMissingParent", requireEmit: false);
            var cingr007 = RequireId(menuRun.Diagnostics, "CINGR007");
            var message = cingr007.GetMessage();
            Require(message.IndexOf("Global.Setup.GlobalScopeExtensions.Construct", StringComparison.Ordinal) >= 0,
                "CINGR007 parent id: " + message);
            Require(message.IndexOf("Global", StringComparison.Ordinal) >= 0, "CINGR007 assembly: " + message);
            RequireNoId(menuRun.Diagnostics, "CINGR003");
            Console.WriteLine("      " + message);
        }

        private static void TestInjectAttribute() {
            var emitted = EmitRoot(InjectAttributeSource, "InjectRoot", out var graph);
            var plain = FindRegistration(graph, "PlainConstruct");
            Require(plain.Dependencies.Count == 0, "Construct without [Inject] must not bind: " + plain.Dependencies.Count);
            var named = FindRegistration(graph, "NamedInject");
            Require(named.Dependencies.Count == 1 && named.Dependencies[0].Source == "Construct", "[Inject] method must bind as Construct edge");
            Require(named.Dependencies[0].Method == "Setup", "edge must carry the [Inject] method name; got " + named.Dependencies[0].Method);
            Require(emitted.IndexOf(".Setup(", StringComparison.Ordinal) >= 0, "emit must call the [Inject] method by its name:\n" + Head(emitted));
            Require(emitted.IndexOf("plainConstruct.Construct(", StringComparison.Ordinal) < 0, "emit must not call un-attributed Construct");
        }

        private static void TestInjectableEmit() {
            var emitted = EmitRoot(InjectableSource, "InjectableRoot", out _);
            Require(emitted.IndexOf("if (target is global::Sample.PoolCard poolCard)", StringComparison.Ordinal) >= 0,
                "Injectable must get an Inject case:\n" + emitted);
            Require(emitted.IndexOf("poolCard.Setup(", StringComparison.Ordinal) >= 0, "Inject case must call the [Inject] method");
            Require(emitted.IndexOf("emptyCard.Init();", StringComparison.Ordinal) >= 0,
                "[Inject] without parameters must still be called:\n" + emitted);
            Require(emitted.IndexOf("global::Sample.PoolCard _", StringComparison.Ordinal) < 0, "Injectable must not have a field");
            Require(emitted.IndexOf("{ typeof(global::Sample.PoolCard),", StringComparison.Ordinal) < 0, "Injectable must not be exported");
            Require(emitted.IndexOf("new global::Sample.PoolCard(", StringComparison.Ordinal) < 0, "Injectable must not be constructed");
            RequireCompiles(emitted, InjectableSource);
        }

        private static void TestInjectableErrors() {
            var graph = ResolveRoot(InjectableErrorsSource, "InjectableErrorsRoot");
            var noInject = RequireDiagnostic(graph, "CINGR008");
            Require(noInject.ToDiagnostic().GetMessage().IndexOf("NoInjectCard", StringComparison.Ordinal) >= 0,
                "CINGR008 must name the type: " + noInject.ToDiagnostic().GetMessage());
            Require(graph.Diagnostics.Count(d => d.Descriptor.Id == "CINGR008") == 1, "PoolCard has [Inject] and must not get CINGR008");
            var missing = RequireDiagnostic(graph, "CINGR003");
            Require(missing.ToDiagnostic().GetMessage().IndexOf("Consumer", StringComparison.Ordinal) >= 0,
                "Injectable must not be resolvable: " + missing.ToDiagnostic().GetMessage());
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
            Require(emitted.IndexOf("Container", StringComparison.Ordinal) >= 0, "missing container class");
            Require(emitted.IndexOf("LoadGlobalContainer", StringComparison.Ordinal) >= 0,
                "class name must follow {RootType}{RootMethod}Container; got snippet:\n" + Head(emitted));
            Require(emitted.IndexOf("internal sealed class GlobalScopeExtensionsLoadGlobalContainer", StringComparison.Ordinal) >= 0,
                "expected GlobalScopeExtensionsLoadGlobalContainer:\n" + Head(emitted));
            Require(emitted.IndexOf("request.IsRegistered(typeof(", StringComparison.Ordinal) >= 0, "alternative must be chosen by the registered implementation");
            Require(emitted.IndexOf("ItchLanguageDebugAPI", StringComparison.Ordinal) >= 0, "alternative true branch missing");
            Require(emitted.IndexOf("ItchLanguageExternAPI", StringComparison.Ordinal) >= 0, "alternative false branch missing");
            Require(emitted.IndexOf("CreatePopup", StringComparison.Ordinal) < 0, "LoadGlobal has no transients");
            Require(emitted.IndexOf("GeneratedScopes.Register", StringComparison.Ordinal) >= 0,
                "generated class must register with GeneratedScopes:\n" + Head(emitted));
            Require(emitted.IndexOf("LoadGlobal+Construct", StringComparison.Ordinal) >= 0,
                "GeneratedScopes.Register rootId must contain LoadGlobal+Construct:\n" + Head(emitted, 2000));
            Require(emitted.IndexOf("_parent != null ? _parent.Diagnostics : null", StringComparison.Ordinal) >= 0,
                "diagnostics must link to the parent container:\n" + Head(emitted, 4000));
            Require(emitted.IndexOf("new global::Internal.RegistrationInfo(0, typeof(", StringComparison.Ordinal) >= 0,
                "diagnostics must list registrations with types:\n" + Head(emitted, 4000));
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
            RequireCompiles(emitted, CollectionSource);
        }

        private static void TestTransientEmit() {
            var emitted = EmitRoot(TransientSource, "TransientRoot", out _);
            Require(emitted.IndexOf("CreatePopup", StringComparison.Ordinal) >= 0, "missing CreatePopup method:\n" + Head(emitted));
            Require(emitted.IndexOf("private readonly global::Sample.Popup _popup", StringComparison.Ordinal) < 0,
                "Transient must not have a field");
            RequireCompiles(emitted, TransientSource);
        }

        private static void TestTransientMarkerEmit() {
            var emitted = EmitRoot(TransientMarkerSource, "TransientMarkerRoot", out _);
            Require(emitted.IndexOf("new global::Sample.IStep[] { _firstStep, CreateSecondStep(), _thirdStep }", StringComparison.Ordinal) >= 0,
                "IStep list must keep registration order and create the transient:\n" + emitted);
            Require(emitted.IndexOf("new global::Sample.ISolo[] { CreateSoloStep() }", StringComparison.Ordinal) >= 0,
                "single transient ISolo must still be listed:\n" + emitted);
            Require(emitted.IndexOf("global::Sample.IStep[] _", StringComparison.Ordinal) < 0,
                "list with a transient must not be a field:\n" + emitted);
            Require(Occurrences(emitted, "if (type == typeof(global::Sample.IStep))") == 0,
                "IStep resolves through the exported singleton:\n" + emitted);
            Require(Occurrences(emitted, "if (type == typeof(global::Sample.SecondStep))") == 2,
                "one SecondStep branch in Resolve and one in TryResolve:\n" + emitted);
            RequireCompiles(emitted, TransientMarkerSource);
        }

        private static int Occurrences(string text, string fragment) {
            var count = 0;
            for (var index = text.IndexOf(fragment, StringComparison.Ordinal); index >= 0;
                 index = text.IndexOf(fragment, index + fragment.Length, StringComparison.Ordinal))
                count++;
            return count;
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
            Require(
                diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error,
                "CINGR001 severity " + diagnostic.Descriptor.DefaultSeverity);
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
            Require(emitted.IndexOf("class ContainerInstallerAttribute", StringComparison.Ordinal) < 0,
                "ContainerInstallerAttribute must not be emitted:\n" + Head(emitted));
            Require(compilation.GetTypeByMetadataName("Internal.ContainerInstallerAttribute") != null,
                "ContainerInstallerAttribute must already exist in the compilation");
        }

        // IBuilder в параметрах делает метод installer'ом, но реестр контейнера сам ничего не регистрирует.
        private static void TestRegistryAttachIsNotInstaller() {
            var compilation = Compile(RegistryAttachSource, "InternalLib");
            var document = WalkDocument(compilation);
            foreach (var method in document.Methods) {
                Require(method.Id.IndexOf("AttachBuilder", StringComparison.Ordinal) < 0,
                    "IContainerRegistry.AttachBuilder must not be walked as installer: " + method.Id);
            }
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

        // AddSessionServices: локальные функции зовут RegisterCommand<T> несколько раз. Слияние harvest
        // сравнивало вызовы только по id и оставляло первый — остальные команды молча пропадали.
        private static void TestHarvestRepeatedGenericInstaller() {
            var shared = EmitStubs("HarvestGenericStubs");
            var compilation = Compile(HarvestGenericLibSource, "HarvestGenericLib", extra: new[] { shared }, includeStubs: false);
            var document = WalkDocument(compilation, harvest: true);
            var installer = FindMethod(document, "AddCommands");
            var calls = installer.Calls.FindAll(c => c.IndexOf("RegisterItem", StringComparison.Ordinal) >= 0).Count;
            Require(calls == 3, "harvest must keep every RegisterItem<T> call; got " + calls + " calls=" + string.Join(",", installer.Calls));
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
            for (var i = 0; i < document.Methods.Count; i++) {
                if (document.Methods[i].Id.IndexOf("CardPointerHandler.Register", StringComparison.Ordinal) < 0)
                    continue;
                Require(document.Methods[i].IsRoot == false,
                    "IEntityComponent.Register must not be a root: " + document.Methods[i].Id);
            }

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
            Require(localClass.IndexOf("GeneratedScopes.RegisterVariant(", StringComparison.Ordinal) >= 0, "local must register as a variant");
            Require(ViewTypeOf(localClass).IndexOf("Local", StringComparison.Ordinal) >= 0, "local variant must key on its view type: " + ViewTypeOf(localClass));
            Require(ViewTypeOf(remoteClass).IndexOf("Remote", StringComparison.Ordinal) >= 0, "remote variant must key on its view type: " + ViewTypeOf(remoteClass));
        }

        private static void TestEntityCardSplitEmit() {
            var compilation = Compile(EntityCardSplitSource, extraSource: EntityCardAssets);
            var document = WalkBound(compilation);
            var references = ReferenceSymbols.Create(compilation);
            Require(references != null, "references");
            var hints = new HashSet<string>(StringComparer.Ordinal);
            var classes = new List<string>();
            foreach (var graph in EdgeResolver.Resolve(document, compilation, references!)) {
                if (ScopeEmitter.TryEmit(graph, document, compilation, references!, out var hint, out _) == false)
                    continue;
                Require(hints.Add(hint), "duplicate hint " + hint);
                classes.Add(hint);
            }

            Require(hints.Contains("CardFactoryBuildLocalContainer.g.cs"), "missing BuildLocal class; got " + string.Join(", ", classes));
            Require(hints.Contains("CardFactoryBuildRemoteContainer.g.cs"), "missing BuildRemote class; got " + string.Join(", ", classes));
        }

        private static string ViewTypeOf(string source) {
            var anchor = source.IndexOf("RegisterVariant(", StringComparison.Ordinal);
            if (anchor < 0)
                return "";
            var start = source.IndexOf("typeof(global::", anchor, StringComparison.Ordinal);
            if (start < 0)
                return "";
            var end = source.IndexOf(')', start);
            return end < 0 ? "" : source.Substring(start, end - start);
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
            Require(message.IndexOf("Simplify the condition", StringComparison.Ordinal) >= 0, "CINGR005 must say what to do: " + message);
        }

        private static void TestGenericInstallerSameAssembly() {
            var graph = ResolveRoot(GenericLocalSource, "GenericLocalRoot");
            RequireNo(graph, "CINGR003");
            var widget = FindRegistration(graph, "Widget");
            Require(
                widget.ImplementationType.IndexOf("Widget<", StringComparison.Ordinal) >= 0 &&
                widget.ImplementationType.IndexOf("Alpha", StringComparison.Ordinal) >= 0,
                "generic installer must close Widget<Alpha>: " + widget.ImplementationType);
            Require(
                widget.ServiceTypes.Exists(s => s.IndexOf("IWidget", StringComparison.Ordinal) >= 0 &&
                                               s.IndexOf("Alpha", StringComparison.Ordinal) >= 0),
                "generic installer must register IWidget<Alpha>: " + string.Join(",", widget.ServiceTypes));
            FindRegistration(graph, "NeedsWidget");
        }

        private static void TestGenericInstallerViaManifest() {
            var shared = EmitStubs("GenericManifestStubs");
            var libCompilation = Compile(GenericInstallerLibSource, "GenericLib", extra: new[] { shared }, includeStubs: false);
            var libDocument = WalkDocument(libCompilation, harvest: false);
            var libEmit = GraphEmitter.Emit(libDocument, libCompilation);
            Require(libEmit.IndexOf("RegisterItem", StringComparison.Ordinal) >= 0,
                "manifest must export generic installer:\n" + Head(libEmit));
            var libReference = EmitAssembly(libCompilation, libEmit, "GenericLib");

            var consumerCompilation = Compile(
                GenericManifestConsumerSource,
                "GenericConsumer",
                extra: new[] { shared, libReference },
                includeStubs: false);
            var consumerDocument = WalkDocument(consumerCompilation, harvest: false);
            ManifestReader.Merge(consumerDocument, consumerCompilation);
            ManifestReader.ReportUnresolved(consumerDocument);
            RequireNoDocument(consumerDocument, "CINGR002");

            var references = ReferenceSymbols.Create(consumerCompilation);
            Require(references != null, "consumer ReferenceSymbols");
            var graph = EdgeResolver.ResolveByHint(consumerDocument, consumerCompilation, references!, "GenericManifestRoot");
            Require(graph != null, "GenericManifestRoot missing; roots=" + Roots(consumerDocument));
            RequireNo(graph!, "CINGR003");
            var box = FindRegistration(graph!, "Box");
            Require(
                box.ImplementationType.IndexOf("Cargo", StringComparison.Ordinal) >= 0,
                "manifest generic installer must close Box<Cargo>: " + box.ImplementationType);
            FindRegistration(graph!, "Cargo");

            var twoCompilation = Compile(
                GenericManifestTwoConsumerSource,
                "GenericTwoConsumer",
                extra: new[] { shared, libReference },
                includeStubs: false);
            var twoDocument = WalkDocument(twoCompilation, harvest: false);
            ManifestReader.Merge(twoDocument, twoCompilation);
            var twoGraph = EdgeResolver.ResolveByHint(twoDocument, twoCompilation, references!, "GenericTwoRoot");
            Require(twoGraph != null, "GenericTwoRoot missing");
            RequireNo(twoGraph!, "CINGR003");
            Require(
                ScopeEmitter.TryEmit(twoGraph!, twoDocument, twoCompilation, references!, out _, out var twoSource),
                "two instantiations must emit");
            Require(
                twoSource.IndexOf("Box<>", StringComparison.Ordinal) < 0,
                "manifest service types must close, not leave Box<>:\n" + Head(twoSource, 2500));
        }

        private static void TestGenericInstallerReturnAs() {
            var shared = EmitStubs("GenericAsStubs");
            var libCompilation = Compile(GenericInstallerLibSource, "GenericAsLib", extra: new[] { shared }, includeStubs: false);
            var libDocument = WalkDocument(libCompilation, harvest: false);
            var libReference = EmitAssembly(libCompilation, GraphEmitter.Emit(libDocument, libCompilation), "GenericAsLib");

            var consumerCompilation = Compile(
                GenericReturnAsConsumerSource,
                "GenericAsConsumer",
                extra: new[] { shared, libReference },
                includeStubs: false);
            var consumerDocument = WalkDocument(consumerCompilation, harvest: false);
            ManifestReader.Merge(consumerDocument, consumerCompilation);
            var references = ReferenceSymbols.Create(consumerCompilation);
            Require(references != null, "as-consumer ReferenceSymbols");
            var graph = EdgeResolver.ResolveByHint(consumerDocument, consumerCompilation, references!, "GenericAsRoot");
            Require(graph != null, "GenericAsRoot missing");
            RequireNo(graph!, "CINGR003");
            var cargo = graph!.Registrations.Find(r =>
                r.ImplementationType.IndexOf("Cargo", StringComparison.Ordinal) >= 0 &&
                r.ImplementationType.IndexOf("Box", StringComparison.Ordinal) < 0);
            Require(cargo != null, "returned Register<T> must close to Cargo; have " +
                                   string.Join(",", graph.Registrations.ConvertAll(r => r.ImplementationType)));
            Require(
                cargo!.ServiceTypes.Exists(s => s.IndexOf("ICargo", StringComparison.Ordinal) >= 0),
                ".As on returned installer registration must add ICargo; services=" +
                string.Join(",", cargo.ServiceTypes) + " impl=" + cargo.ImplementationType);
            FindRegistration(graph, "NeedsCargo");
        }

        private static void TestGenericInstallerNestedEmit() {
            var emitted = EmitRoot(GenericNestedSource, "NestedGenericRoot", out var graph);
            RequireNo(graph, "CINGR003");
            Require(
                emitted.IndexOf("<>", StringComparison.Ordinal) < 0,
                "generated class must not use unbound generics:\n" + Head(emitted, 4000));
            Require(
                emitted.IndexOf("BackendProjection<", StringComparison.Ordinal) >= 0 &&
                emitted.IndexOf("ProfileProjection", StringComparison.Ordinal) >= 0,
                "must close BackendProjection<ProfileProjection>:\n" + Head(emitted, 2000));
        }

        private static void TestGenericRegisterCommandEmit() {
            var emitted = EmitRoot(GenericRegisterCommandSource, "CommandRoot", out var graph);
            RequireNo(graph, "CINGR003");
            Console.WriteLine("      impls=" + string.Join(" | ", graph.Registrations.ConvertAll(r => r.ImplementationType)));
            Require(
                emitted.IndexOf("CommandResolver<", StringComparison.Ordinal) >= 0,
                "must emit CommandResolver:\n" + Head(emitted, 2500));
            Require(
                emitted.IndexOf("CommandResolver<>", StringComparison.Ordinal) < 0,
                "must not emit unbound CommandResolver<>:\n" + Head(emitted, 2500));
            RequireCompiles(emitted, GenericRegisterCommandSource);
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

        private sealed class GeneratorRun {
            public MetadataReference Reference = null!;
            public Compilation Output = null!;
            public Diagnostic[] Diagnostics = Array.Empty<Diagnostic>();
        }

        private static GeneratorRun RunGenerator(
            Compilation compilation,
            string assemblyName,
            bool requireEmit = true) {
            var parse = compilation.SyntaxTrees.First().Options as CSharpParseOptions
                        ?? new CSharpParseOptions(LanguageVersion.Latest);
            var driver = CSharpGeneratorDriver.Create(
                new[] { new ContainerGraphGenerator().AsSourceGenerator() },
                parseOptions: parse);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);
            var diagnostics = generatorDiagnostics
                .Concat(output.GetDiagnostics())
                .ToArray();
            var stream = new MemoryStream();
            var emit = output.Emit(stream);
            if (requireEmit) {
                var failures = emit.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString())
                    .Concat(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))
                    .ToArray();
                Require(emit.Success, assemblyName + " generator emit failed:\n" + string.Join("\n", failures));
            }

            return new GeneratorRun {
                Reference = MetadataReference.CreateFromImage(stream.ToArray()),
                Output = output,
                Diagnostics = diagnostics,
            };
        }

        private static MetadataReference EmitPe(Compilation compilation, string assemblyName) {
            var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Require(result.Success, assemblyName + " emit failed:\n" +
                                    string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            return MetadataReference.CreateFromImage(stream.ToArray());
        }

        private static void RequireGeneratedMissing(Compilation compilation, string fragment) {
            foreach (var tree in compilation.SyntaxTrees) {
                if (tree.FilePath != null && tree.FilePath.IndexOf("ContainerGraph", StringComparison.Ordinal) < 0 &&
                    tree.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) == false)
                    continue;
                var text = tree.GetText().ToString();
                if (text.IndexOf(fragment, StringComparison.Ordinal) >= 0)
                    throw new Exception("generated source must not contain '" + fragment + "'; tree=" + tree.FilePath);
            }
        }

        private static void RequireGeneratedContains(Compilation compilation, string fragment) {
            foreach (var tree in compilation.SyntaxTrees) {
                if (tree.FilePath != null && tree.FilePath.IndexOf("ContainerGraph", StringComparison.Ordinal) < 0 &&
                    tree.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) == false)
                    continue;
                if (tree.GetText().ToString().IndexOf(fragment, StringComparison.Ordinal) >= 0)
                    return;
            }

            var names = string.Join(", ", compilation.SyntaxTrees.Select(t => t.FilePath));
            throw new Exception("generated source missing '" + fragment + "'; trees=" + names);
        }

        private static void RequireNoId(IEnumerable<Diagnostic> diagnostics, string id) {
            foreach (var diagnostic in diagnostics) {
                if (diagnostic.Id == id)
                    throw new Exception("unexpected " + id + ": " + diagnostic.GetMessage());
            }
        }

        private static Diagnostic RequireId(IEnumerable<Diagnostic> diagnostics, string id) {
            foreach (var diagnostic in diagnostics) {
                if (diagnostic.Id == id)
                    return diagnostic;
            }

            throw new Exception("missing " + id + "; diagnostics: " +
                                string.Join(", ", diagnostics.Select(d => d.Id + " " + d.GetMessage())));
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
                    var parse = compilation.SyntaxTrees.First().Options as CSharpParseOptions
                                ?? new CSharpParseOptions(LanguageVersion.Latest);
                    walkCompilation = compilation.AddSyntaxTrees(
                        CSharpSyntaxTree.ParseText(harvested.Source, parse, path: "ContainerInstallerHarvest.g.cs"));
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
    public interface IReadOnlyLifetime {}
    public interface ILifetime : IReadOnlyLifetime { void Terminate(); }
    public interface IServiceRegistration {
        IBuilder Builder { get; }
        System.Type ImplementationType { get; }
        IServiceRegistration AddServiceType(System.Type serviceType);
        IServiceRegistration SetParameter(System.Type type, object value);
    }
    public interface IContainerRegistry {
        IServiceRegistration Add(System.Type implementation, ServiceLifetime lifetime);
        IServiceRegistration AddInstance(System.Type serviceType, object instance);
        void AddInjection(object target);
    }
    public interface IContainerDiagnostics {
        string Name { get; }
        IContainerDiagnostics Parent { get; }
        System.Collections.Generic.IReadOnlyList<IContainerDiagnostics> Children { get; }
        System.Collections.Generic.IReadOnlyList<RegistrationInfo> Registrations { get; }
        System.Collections.Generic.IReadOnlyList<int> BuildOrder { get; }
        System.Collections.Generic.IReadOnlyList<LoadedAssetInfo> LoadedAssets { get; }
        bool IsHistoryEnabled { get; set; }
        System.Collections.Generic.IReadOnlyList<ResolveRecord> History { get; }
    }
    public readonly struct RegistrationInfo {
        public RegistrationInfo(int slot, System.Type implementationType, System.Collections.Generic.IReadOnlyList<System.Type> serviceTypes, ServiceLifetime lifetime, System.Collections.Generic.IReadOnlyList<int> dependencies, bool isInstantiated, bool isExternal) {}
    }
    public readonly struct LoadedAssetInfo {}
    public sealed class ContainerDiagnostics : IContainerDiagnostics {
        public ContainerDiagnostics(string name, IContainerDiagnostics parent, System.Collections.Generic.IReadOnlyList<RegistrationInfo> registrations, System.Collections.Generic.IReadOnlyList<int> buildOrder) {}
        public string Name => null;
        public IContainerDiagnostics Parent => null;
        public System.Collections.Generic.IReadOnlyList<IContainerDiagnostics> Children => null;
        public System.Collections.Generic.IReadOnlyList<RegistrationInfo> Registrations => null;
        public System.Collections.Generic.IReadOnlyList<int> BuildOrder => null;
        public System.Collections.Generic.IReadOnlyList<LoadedAssetInfo> LoadedAssets => null;
        public bool IsHistoryEnabled { get; set; }
        public System.Collections.Generic.IReadOnlyList<ResolveRecord> History => null;
    }
    public readonly struct ResolveRecord {}
    public static class ContainerRegistryDebug {
        public static bool IsEnabled { get; set; } = true;
    }
    public interface IContainer : System.IDisposable {
        IContainerDiagnostics Diagnostics { get; }
        IReadOnlyLifetime Lifetime { get; }
        object Resolve(System.Type type);
        T Resolve<T>();
        bool TryResolve(System.Type type, out object instance);
        System.Collections.Generic.IReadOnlyList<T> ResolveAll<T>();
        void Inject(object target);
        void InjectGameObject(UnityEngine.GameObject target);
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
    public sealed class ContainerGraphRootAttribute : System.Attribute {}
    [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class ContainerInstallerAttribute : System.Attribute {
        public ContainerInstallerAttribute(
            string methodId,
            bool isRoot,
            string file,
            int line,
            System.Type[] implementations,
            System.Type[] services,
            string[] calls,
            string blob) {}
    }
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class ContainerScopeParentAttribute : System.Attribute {
        public ContainerScopeParentAttribute(System.Type rootType, string rootMethod) {}
    }
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class InjectAttribute : System.Attribute {}
    public interface IEventLoop {}
    public class EventLoop : IEventLoop {}
    public interface IViewInjector {}
    public class GeneratedViewInjector {
        public GeneratedViewInjector(IContainer container) {}
    }
    public sealed class GeneratedScopeRequest {
        public ILifetime Lifetime { get; }
        public IContainer Parent { get; }
        public bool IsRegistered(System.Type implementation) { return false; }
        public T Get<T>() { return default(T); }
    }
    public static class GeneratedScopes {
        public static void Register(string rootId, System.Func<GeneratedScopeRequest, IContainer> factory) {}
        public static void RegisterVariant(string rootId, System.Type viewType, System.Func<GeneratedScopeRequest, IContainer> factory) {}
        public static bool IsRegistered(string rootId) { return false; }
    }

    public static class BuilderExtensions {
        public static IServiceRegistration Register<T>(this IBuilder builder, ServiceLifetime lifetime = ServiceLifetime.Singleton) { return null; }
        public static IServiceRegistration Register<TInterface, TImplementation>(this IBuilder builder, ServiceLifetime lifetime = ServiceLifetime.Singleton) { return null; }
        public static IServiceRegistration RegisterInstance<T>(this IBuilder builder, T instance) { return null; }
        public static IServiceRegistration RegisterComponent<T>(this IBuilder builder, T component, ServiceLifetime lifetime = ServiceLifetime.Singleton) { return null; }
        public static IServiceRegistration As<T>(this IServiceRegistration registration) { return registration; }
        public static IServiceRegistration AsSelf(this IServiceRegistration registration) { return registration; }
        public static IServiceRegistration AsSelfResolvable(this IServiceRegistration registration) { return registration; }
        public static IServiceRegistration WithParameter<T>(this IServiceRegistration registration, T value) { return registration; }
        public static IServiceRegistration WithScopeLifetime(this IServiceRegistration registration) { return registration; }
        public static void Inject<T>(this IBuilder builder, T target) {}
        public static void Injectable<T>(this IBuilder builder) where T : class {}
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
        [Internal.Inject]
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

        private const string MissingInterfaceSource = @"
using Internal;

namespace Sample {
    public interface IUnregisteredService {}
    public class NeedsMissingInterface {
        public NeedsMissingInterface(IUnregisteredService missing) {}
    }
    public static class MissingInterfaceRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<NeedsMissingInterface>();
        }
    }
}
";

        private const string ParentHitSource = @"
using Internal;

namespace Sample {
    public interface IParentDep {}
    public class ParentDep : IParentDep {}
    public class NeedsParent {
        public NeedsParent(IParentDep dep) {}
    }
    public static class ParentRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<ParentDep>().As<IParentDep>();
        }
    }
    public static class ChildRoot {
        [ContainerScopeParent(typeof(ParentRoot), nameof(ParentRoot.Construct))]
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<NeedsParent>();
        }
    }
}
";

        private const string ParentMissSource = @"
using Internal;

namespace Sample {
    public interface IAbsent {}
    public class NeedsAbsent {
        public NeedsAbsent(IAbsent absent) {}
    }
    public static class EmptyParentRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
        }
    }
    public static class ChildMissRoot {
        [ContainerScopeParent(typeof(EmptyParentRoot), nameof(EmptyParentRoot.Construct))]
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<NeedsAbsent>();
        }
    }
}
";

        private const string ParentManifestLibSource = @"
using Internal;

namespace ParentLib {
    public interface IExportedDep {}
    public class ExportedDep : IExportedDep {}
    public static class ParentManifestRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<ExportedDep>().As<IExportedDep>();
        }
    }
}
";

        private const string ParentManifestChildSource = @"
using Internal;
using ParentLib;

namespace ChildLib {
    public class NeedsExported {
        public NeedsExported(IExportedDep dep) {}
    }
    public static class ChildManifestRoot {
        [ContainerScopeParent(typeof(ParentManifestRoot), nameof(ParentManifestRoot.Construct))]
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<NeedsExported>();
        }
    }
}
";

        private const string DriverInternalSource = @"
namespace Internal {
    public class BackendOptions {}
    public class OptionsContainer {
        [ContainerGraphRoot]
        public void Register(IScopeBuilder builder) {
            builder.RegisterInstance(new BackendOptions());
        }
    }
}
";

        private const string DriverGlobalSource = @"
using Internal;

namespace Global.Setup {
    public interface IUpdater {}
    public class Updater : IUpdater {}
    public static class GlobalScopeExtensions {
        [ContainerScopeParent(typeof(OptionsContainer), nameof(OptionsContainer.Register))]
        public static void Construct(IScopeBuilder builder) {
            builder.Register<Updater>().As<IUpdater>();
        }
    }
}
";

        private const string DriverMenuSource = @"
using Internal;
using Global.Setup;

namespace Menu.Common {
    public class NeedsUpdater {
        public NeedsUpdater(IUpdater updater) {}
    }
    public static class MenuScopeExtensions {
        [ContainerScopeParent(typeof(GlobalScopeExtensions), nameof(GlobalScopeExtensions.Construct))]
        public static void Construct(IScopeBuilder builder) {
            builder.Register<NeedsUpdater>();
        }
    }
}
";

        private const string IdeGlobalSource = DriverGlobalSource + @"
namespace Global.Setup {
    public interface IClock {}
    public class Clock : IClock {}
    public static class GlobalInstallers {
        public static IScopeBuilder AddClock(this IScopeBuilder builder) {
            builder.Register<Clock>().As<IClock>();
            return builder;
        }
    }
}
";

        private const string IdeMenuSource = @"
using Internal;
using Global.Setup;

namespace Menu.Common {
    public class NeedsClock {
        public NeedsClock(IClock clock, IUpdater updater) {}
    }
    public static class MenuScopeExtensions {
        [ContainerScopeParent(typeof(GlobalScopeExtensions), nameof(GlobalScopeExtensions.Construct))]
        public static void Construct(IScopeBuilder builder) {
            builder.AddClock();
            builder.Register<NeedsClock>();
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

        private const string InjectAttributeSource = @"
using Internal;

namespace Sample {
    public interface IDep {}
    public class Dep : IDep {}
    public class PlainConstruct { public void Construct(IDep dep) {} }
    public class NamedInject { [Inject] internal void Setup(IDep dep) {} }
    public static class InjectRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<Dep>().As<IDep>();
            builder.Register<PlainConstruct>();
            builder.Register<NamedInject>();
        }
    }
}
";

        private const string InjectableSource = @"
using Internal;

namespace Sample {
    public interface IDep {}
    public class Dep : IDep {}
    public class PoolCard { [Inject] internal void Setup(IDep dep, IReadOnlyLifetime lifetime) {} }
    public class EmptyCard { [Inject] internal void Init() {} }
    public static class InjectableRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<Dep>().As<IDep>();
            builder.Injectable<PoolCard>();
            builder.Injectable<EmptyCard>();
        }
    }
}
";

        private const string InjectableErrorsSource = @"
using Internal;

namespace Sample {
    public class NoInjectCard { public void Construct() {} }
    public class PoolCard { [Inject] internal void Setup() {} }
    public class Consumer { public Consumer(PoolCard card) {} }
    public static class InjectableErrorsRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Injectable<NoInjectCard>();
            builder.Injectable<PoolCard>();
            builder.Register<Consumer>();
        }
    }
}
";

        private const string CycleSource = @"
using Internal;

namespace Sample {
    public class CycleA { [Internal.Inject] public void Construct(CycleB b) {} }
    public class CycleB { [Internal.Inject] public void Construct(CycleC c) {} }
    public class CycleC { [Internal.Inject] public void Construct(CycleA a) {} }
    public static class CycleRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<CycleA>();
            builder.Register<CycleB>();
            builder.Register<CycleC>();
        }
    }
}
";

        private const string RegistryAttachSource = @"
using Internal;

namespace Sample {
    public sealed class Registry : IContainerRegistry {
        public IServiceRegistration Add(System.Type implementation, ServiceLifetime lifetime) { return null; }
        public IServiceRegistration AddInstance(System.Type serviceType, object instance) { return null; }
        public void AddInjection(object target) {}
        internal void AttachBuilder(IBuilder builder) {}
    }
}
";

        private const string GenericRegisterCommandSource = @"
using Internal;

namespace Internal {
    public interface INetworkCommandsCollection { void Add(object command); }
    public class NetworkCommandsCollection : INetworkCommandsCollection {
        public void Add(object command) {}
    }
    public class CommandHost {
        public static IServiceRegistration RegisterCommand<T>(IScopeBuilder builder) {
            builder.Register<CommandResolver<T>>();
            return builder.Register<T>();
        }
        public class CommandResolver<T> {
            public CommandResolver(T command, INetworkCommandsCollection collection) {}
        }
    }
}

namespace Meta {
    public interface IMetaConnectionAwaiter {}
    public class ConnectionCompletedCommand {}
    public static class CommandRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<NetworkCommandsCollection>().As<INetworkCommandsCollection>();
            CommandHost.RegisterCommand<ConnectionCompletedCommand>(builder).As<IMetaConnectionAwaiter>();
        }
    }
}
";

        private const string GenericNestedSource = @"
using Internal;

namespace Shared {
    public class SharedBackendUser {
        public class ProfileProjection {}
        public class StatsProjection {}
    }
}

namespace Meta {
    public class BackendProjection<T> {}
    public interface IBackendProjection<T> {}
    public interface IBackendProjection {}
    public class NeedsProjection {
        public NeedsProjection(IBackendProjection<Shared.SharedBackendUser.ProfileProjection> projection) {}
    }
    public static class BackendProjectionExtensions {
        public static IScopeBuilder RegisterBackendProjection<T>(this IScopeBuilder builder) {
            builder.Register<BackendProjection<T>>()
                   .As<IBackendProjection<T>>()
                   .As<IBackendProjection>();
            return builder;
        }
    }
    public static class NestedGenericRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.RegisterBackendProjection<Shared.SharedBackendUser.ProfileProjection>();
            builder.RegisterBackendProjection<Shared.SharedBackendUser.StatsProjection>();
            builder.Register<NeedsProjection>();
        }
    }
}
";

        private const string GenericLocalSource = @"
using Internal;

namespace Sample {
    public class Alpha {}
    public class Widget<T> {}
    public interface IWidget<T> {}
    public class NeedsWidget {
        public NeedsWidget(IWidget<Alpha> widget) {}
    }
    public static class WidgetInstaller {
        public static IScopeBuilder RegisterWidget<T>(this IScopeBuilder builder) {
            builder.Register<Widget<T>>().As<IWidget<T>>();
            return builder;
        }
    }
    public static class GenericLocalRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.RegisterWidget<Alpha>();
            builder.Register<NeedsWidget>();
        }
    }
}
";

        private const string HarvestGenericLibSource = @"
using Internal;

namespace HarvestGeneric {
    public class Box<T> {
        public Box(T item) {}
    }
    public class A {}
    public class B {}
    public class C {}
    public static class ItemInstaller {
        public static IServiceRegistration RegisterItem<T>(this IScopeBuilder builder) {
            builder.Register<Box<T>>();
            return builder.Register<T>();
        }
    }
    public static class CommandsInstaller {
        public static IScopeBuilder AddCommands(this IScopeBuilder builder) {
            AddFirst();
            AddRest();
            return builder;

            void AddFirst() {
                builder.RegisterItem<A>();
            }

            void AddRest() {
                builder.RegisterItem<B>();
                builder.RegisterItem<C>();
            }
        }
    }
}
";

        private const string GenericInstallerLibSource = @"
using Internal;

namespace GenericLib {
    public class Box<T> {
        public Box(T item) {}
    }
    public static class ItemInstaller {
        public static IServiceRegistration RegisterItem<T>(this IScopeBuilder builder) {
            builder.Register<Box<T>>();
            return builder.Register<T>();
        }
    }
}
";

        private const string GenericManifestTwoConsumerSource = @"
using Internal;
using GenericLib;

namespace Sample {
    public class Cargo {}
    public class Other {}
    public static class GenericTwoRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.RegisterItem<Cargo>();
            builder.RegisterItem<Other>();
        }
    }
}
";

        private const string GenericManifestConsumerSource = @"
using Internal;
using GenericLib;

namespace Sample {
    public class Cargo {}
    public static class GenericManifestRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.RegisterItem<Cargo>();
        }
    }
}
";

        private const string GenericReturnAsConsumerSource = @"
using Internal;
using GenericLib;

namespace Sample {
    public interface ICargo {}
    public class Cargo : ICargo {}
    public class NeedsCargo {
        public NeedsCargo(ICargo cargo) {}
    }
    public static class GenericAsRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.RegisterItem<Cargo>().As<ICargo>();
            builder.Register<NeedsCargo>();
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

        private const string TransientMarkerSource = @"
using Internal;

namespace Sample {
    public interface IStep {}
    public interface ISolo {}
    public class FirstStep : IStep {}
    public class SecondStep : IStep {}
    public class ThirdStep : IStep {}
    public class SoloStep : ISolo {}
    public static class TransientMarkerRoot {
        public static void Construct(Internal.IScopeBuilder builder) {
            builder.Register<FirstStep>().As<IStep>();
            builder.Register<SecondStep>(Internal.ServiceLifetime.Transient).As<IStep>();
            builder.Register<ThirdStep>().As<IStep>();
            builder.Register<SoloStep>(Internal.ServiceLifetime.Transient).As<ISolo>();
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

        private const string EntityCardSplitSource = @"
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
                _loader.Load(null, null, GamePlayPrefabs.CardLocal, BuildLocal);
            else
                _loader.Load(null, null, GamePlayPrefabs.CardRemote, BuildRemote);

            void BuildLocal(IEntityBuilder builder) {
                builder.Register<LocalCard>();
            }

            void BuildRemote(IEntityBuilder builder) {
                builder.Register<RemoteCard>();
            }
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
