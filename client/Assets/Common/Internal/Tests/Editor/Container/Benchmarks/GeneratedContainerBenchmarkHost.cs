using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;

namespace Internal.Tests
{
    // Сгенерированные классы скоупов на том же графе, что у VContainer. Путь как в рантайме:
    // installer пишет в ContainerBuilder, контейнер строит ScopeContainer.Create.
    [NoAutoStaticsCleanup]
    internal static class GeneratedContainerBenchmarkHost
    {
        public const string ContainerName = "Generated";

        private static readonly Action<IBuilder> RootInstaller = BenchmarkRoots.Root;
        private static readonly Action<IBuilder> MatchInstaller = BenchmarkRoots.Match;
        private static readonly Action<IBuilder> CardInstaller = BenchmarkRoots.Card;
        private static readonly Action<IBuilder> Card50Installer = BenchmarkLargeRoots.Card50;

        private static readonly string RootId = GeneratedScopes.RootId(RootInstaller.Method);
        private static readonly string MatchId = GeneratedScopes.RootId(MatchInstaller.Method);
        private static readonly string CardId = GeneratedScopes.RootId(CardInstaller.Method);
        private static readonly string Card50Id = GeneratedScopes.RootId(Card50Installer.Method);

        public const int ManyScopesCount = 500;

        // Без диагностики: у VContainer в замере она тоже выключена, а в релизном билде её нет.
        public static BenchmarkReport Run()
        {
            using (RegisterScopes())
                using (WithoutDiagnostics())
                    return ContainerBenchmarkRunner.Run(ContainerName, Open);
        }

        public static IDisposable WithoutDiagnostics()
        {
            return new DiagnosticsSwitch(false);
        }

        public static IBenchmarkSession Open()
        {
            return OpenSession();
        }

        public static GeneratedBenchmarkSession OpenSession()
        {
            var lifetime = new Lifetime();
            var root = Create(RootInstaller, RootId, new ContainerBuilder(RootId, lifetime), lifetime);

            var match = Create(MatchInstaller, MatchId, new ContainerBuilder("Match", root, root.Lifetime),
                root.Lifetime);

            var card = Create(CardInstaller, CardId, new ContainerBuilder("Card", match, match.Lifetime),
                match.Lifetime);
            return new GeneratedBenchmarkSession(lifetime, root, match, card);
        }

        // RuntimeInitializeOnLoadMethod в edit mode не вызывается, поэтому регистрацию классов
        // запускаем сами. Уже зарегистрированные (плей-мод) не трогаем и потом не снимаем.
        public static IDisposable RegisterScopes()
        {
            var registered = new List<string>();
            Register(RootId, typeof(BenchmarkRootsRootContainer), registered);
            Register(MatchId, typeof(BenchmarkRootsMatchContainer), registered);
            Register(CardId, typeof(BenchmarkRootsCardContainer), registered);
            Register(Card50Id, typeof(BenchmarkLargeRootsCard50Container), registered);
            return new Registration(registered);
        }

        private static void Register(string rootId, Type container, List<string> registered)
        {
            if (GeneratedScopes.IsRegistered(rootId) == true)
                return;

            var method = container.GetMethod("RegisterGenerated", BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
                throw new InvalidOperationException(container.FullName + " has no RegisterGenerated.");

            method.Invoke(null, null);
            registered.Add(rootId);
        }

        private static IContainer Create(
            Action<IBuilder> installer,
            string rootId,
            ContainerBuilder containerBuilder,
            IReadOnlyLifetime lifetime)
        {
            var builder = new RootBuilder(containerBuilder, new EventLoop(), lifetime);
            installer.Invoke(builder);
            builder.RegisterInstance(builder.Events);

            var container = ScopeContainer.Create(rootId, containerBuilder);
            builder.Events.Bind(container);
            return container;
        }

        // Байты одного Build по шагам. Повторяет OpenSession/Create без обёрток, иначе в шаги
        // попали бы делегаты самого замера. На шаг — минимум из runs прогонов.
        public static string BuildAllocations(int runs)
        {
            var steps = new AllocationSteps();

            for (var run = 0; run < runs; run++)
            {
                steps.BeginRun();
                ILifetime lifetime = null;
                steps.Add("Session: Lifetime", () => lifetime = new Lifetime());

                var root = MeasureScope(steps, "Root", RootInstaller, RootId, null, lifetime);
                var match = MeasureScope(steps, "Match", MatchInstaller, MatchId, root, lifetime);
                var card = MeasureScope(steps, "Card", CardInstaller, CardId, match, lifetime);

                steps.Add("Session: Dispose", () => {
                    card.Dispose();
                    match.Dispose();
                    root.Dispose();
                    lifetime.Terminate();
                });
            }

            return steps.Format(runs);
        }

        private static IContainer MeasureScope(
            AllocationSteps steps,
            string name,
            Action<IBuilder> installer,
            string rootId,
            IContainer parent,
            IReadOnlyLifetime sessionLifetime)
        {
            var level = (BenchmarkScopeLevel)Enum.Parse(typeof(BenchmarkScopeLevel), name);
            var builderLifetime = parent == null ? sessionLifetime : parent.Lifetime;
            ContainerBuilder containerBuilder = null;
            RootBuilder builder = null;
            IContainer container = null;

            steps.Add(name + ": ContainerBuilder", () => containerBuilder = parent == null
                ? new ContainerBuilder(rootId, sessionLifetime)
                : new ContainerBuilder(name, parent, parent.Lifetime));

            steps.Add(name + ": RootBuilder + Registry + EventLoop",
                () => builder = new RootBuilder(containerBuilder, new EventLoop(), builderLifetime));

            steps.Add($"{name}: installer ({BenchmarkGraph.Count(level)} registrations)",
                () => installer.Invoke(builder));
            steps.Add(name + ": RegisterInstance(Events)", () => builder.RegisterInstance(builder.Events));

            steps.Add(name + ": ScopeContainer.Create",
                () => container = ScopeContainer.Create(rootId, containerBuilder));

            builder.Events.Bind(container);
            return container;
        }

        // count скоупов сущности под одним Match подряд, в одном кадре: цена массового создания.
        // Живую память не мерим: GC.GetTotalMemory в Unity Mono шагает кусками кучи и даёт даже отрицательные дельты.
        public static string ManyScopes(int count, int runs)
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine($"Generated: {count} entity scopes under one Match, time = median, bytes = min over {runs} runs");
            text.AppendLine();
            text.AppendLine("| Scope | Build (ms) | Per scope (ms) | Build allocated (bytes) | Per scope (bytes) | Dispose (ms) | Dispose allocated (bytes) |");
            text.AppendLine("|---|---|---|---|---|---|---|");

            using (var session = OpenSession())
            {
                var match = session.Scope(BenchmarkScopeLevel.Match);
                AppendManyScopes(text, "Card (8 registrations)", CardInstaller, CardId, match, count, runs);
                AppendManyScopes(text, "Card50 (50 registrations)", Card50Installer, Card50Id, match, count, runs);
            }

            return text.ToString();
        }

        private static void AppendManyScopes(
            System.Text.StringBuilder text,
            string name,
            Action<IBuilder> installer,
            string rootId,
            IContainer parent,
            int count,
            int runs)
        {
            var containers = new IContainer[count];

            Action build = () =>
            {
                for (var i = 0; i < count; i++)
                    containers[i] = Create(installer, rootId, new ContainerBuilder("Card", parent, parent.Lifetime), parent.Lifetime);
            };

            Action dispose = () =>
            {
                for (var i = 0; i < count; i++)
                {
                    containers[i].Dispose();
                    containers[i] = null;
                }
            };

            build();
            dispose();

            var buildSeries = new BenchmarkSeries(name + " build");
            var disposeSeries = new BenchmarkSeries(name + " dispose");

            for (var run = 0; run < runs; run++)
            {
                buildSeries.Add(build);
                disposeSeries.Add(dispose);
            }

            var built = buildSeries.ToSample();
            var disposed = disposeSeries.ToSample();
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            text.AppendLine(
                $"| {name} " +
                $"| {built.Milliseconds.ToString("F2", culture)} " +
                $"| {(built.Milliseconds / count).ToString("F4", culture)} " +
                $"| {built.AllocatedBytes} " +
                $"| {built.AllocatedBytes / count} " +
                $"| {disposed.Milliseconds.ToString("F2", culture)} " +
                $"| {disposed.AllocatedBytes} |");
        }

        private sealed class AllocationSteps
        {
            private readonly List<string> _names = new List<string>();
            private readonly List<long> _bytes = new List<long>();
            private int _index;

            public void BeginRun()
            {
                _index = 0;
            }

            public void Add(string name, Action action)
            {
                var allocated = BenchmarkMeasure.Capture(action).AllocatedBytes;

                if (_index == _names.Count)
                {
                    _names.Add(name);
                    _bytes.Add(allocated);
                }
                else
                {
                    _bytes[_index] = Math.Min(_bytes[_index], allocated);
                }

                _index++;
            }

            public string Format(int runs)
            {
                var builder = new System.Text.StringBuilder();
                builder.AppendLine($"Generated Build allocations by step, min over {runs} runs");
                builder.AppendLine();
                builder.AppendLine("| Step | Allocated bytes |");
                builder.AppendLine("|---|---|");

                var total = 0L;
                var build = 0L;

                for (var i = 0; i < _names.Count; i++)
                {
                    builder.AppendLine($"| {_names[i]} | {_bytes[i]} |");
                    total += _bytes[i];

                    if (_names[i] != "Session: Dispose")
                        build += _bytes[i];
                }

                builder.AppendLine($"| Build (sum without Dispose) | {build} |");
                builder.AppendLine($"| Total | {total} |");
                return builder.ToString();
            }
        }

        private sealed class DiagnosticsSwitch : IDisposable
        {
            public DiagnosticsSwitch(bool enabled)
            {
                _previous = ContainerRegistryDebug.IsEnabled;
                ContainerRegistryDebug.IsEnabled = enabled;
            }

            private readonly bool _previous;

            public void Dispose()
            {
                ContainerRegistryDebug.IsEnabled = _previous;
            }
        }

        private sealed class Registration : IDisposable
        {
            public Registration(List<string> rootIds)
            {
                _rootIds = rootIds;
            }

            private readonly List<string> _rootIds;

            public void Dispose()
            {
                for (var i = 0; i < _rootIds.Count; i++)
                    GeneratedScopes.Unregister(_rootIds[i]);
            }
        }
    }

    internal sealed class GeneratedBenchmarkSession : IBenchmarkSession
    {
        public GeneratedBenchmarkSession(ILifetime lifetime, IContainer root, IContainer match, IContainer card)
        {
            _lifetime = lifetime;
            _root = root;
            _match = match;
            _card = card;
        }

        private readonly ILifetime _lifetime;
        private readonly IContainer _root;
        private readonly IContainer _match;
        private readonly IContainer _card;

        public IContainer Scope(BenchmarkScopeLevel level)
        {
            switch (level)
            {
                case BenchmarkScopeLevel.Root:
                    return _root;
                case BenchmarkScopeLevel.Match:
                    return _match;
                default:
                    return _card;
            }
        }

        public int ResolveAllPhases()
        {
            // Same consume path as EventLoop.ResolveList<T>.
            var count = 0;
            count += _card.ResolveAll<IBenchBaseSetup>().Count;
            count += _card.ResolveAll<IBenchBaseSetupAsync>().Count;
            count += _card.ResolveAll<IBenchSetup>().Count;
            count += _card.ResolveAll<IBenchSetupAsync>().Count;
            count += _card.ResolveAll<IBenchSetupCompletion>().Count;
            count += _card.ResolveAll<IBenchSetupCompletionAsync>().Count;
            count += _card.ResolveAll<IBenchLoaded>().Count;
            count += _card.ResolveAll<IBenchLoadedAsync>().Count;
            count += _card.ResolveAll<IBenchDispose>().Count;
            count += _card.ResolveAll<IBenchDisposeAsync>().Count;
            count += _card.ResolveAll<IBenchSceneService>().Count;
            count += _card.ResolveAll<IBenchEntityComponent>().Count;
            return count;
        }

        public object ResolveSingleton()
        {
            return _card.Resolve<BenchRootTime>();
        }

        public object ResolveTransient()
        {
            return _card.Resolve<BenchCardAction>();
        }

        public object Resolve(BenchmarkScopeLevel level, Type type)
        {
            return Scope(level).Resolve(type);
        }

        public void Dispose()
        {
            _card.Dispose();
            _match.Dispose();
            _root.Dispose();
            _lifetime.Terminate();
        }
    }
}