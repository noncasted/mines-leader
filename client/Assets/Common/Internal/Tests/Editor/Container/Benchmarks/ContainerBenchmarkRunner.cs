using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace Internal.Tests
{
    internal readonly struct BenchmarkRun
    {
        public readonly long Ticks;
        public readonly long AllocatedBytes;

        public BenchmarkRun(long ticks, long allocatedBytes)
        {
            Ticks = ticks;
            AllocatedBytes = allocatedBytes;
        }
    }

    internal readonly struct BenchmarkSample
    {
        public readonly string Name;
        public readonly int Runs;
        public readonly long Ticks;
        public readonly double Milliseconds;
        public readonly double MinMilliseconds;
        public readonly double MaxMilliseconds;
        public readonly long AllocatedBytes;
        public readonly long MaxAllocatedBytes;

        public BenchmarkSample(
            string name,
            int runs,
            long ticks,
            long minTicks,
            long maxTicks,
            long allocatedBytes,
            long maxAllocatedBytes)
        {
            Name = name;
            Runs = runs;
            Ticks = ticks;
            Milliseconds = ToMilliseconds(ticks);
            MinMilliseconds = ToMilliseconds(minTicks);
            MaxMilliseconds = ToMilliseconds(maxTicks);
            AllocatedBytes = allocatedBytes;
            MaxAllocatedBytes = maxAllocatedBytes;
        }

        private static double ToMilliseconds(long ticks)
        {
            return ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }
    }

    // Время — медиана прогонов, байты — минимум: счётчик видит и чужие потоки редактора,
    // а лишние байты могут только прибавиться.
    internal sealed class BenchmarkSeries
    {
        public BenchmarkSeries(string name)
        {
            _name = name;
        }

        private readonly string _name;
        private readonly List<BenchmarkRun> _runs = new List<BenchmarkRun>();

        public void Add(Action action)
        {
            _runs.Add(BenchmarkMeasure.Capture(action));
        }

        public BenchmarkSample ToSample()
        {
            if (_runs.Count == 0)
                throw new InvalidOperationException(_name + " has no runs");

            var ticks = new long[_runs.Count];
            var minBytes = long.MaxValue;
            var maxBytes = long.MinValue;
            for (var i = 0; i < _runs.Count; i++)
            {
                ticks[i] = _runs[i].Ticks;
                minBytes = Math.Min(minBytes, _runs[i].AllocatedBytes);
                maxBytes = Math.Max(maxBytes, _runs[i].AllocatedBytes);
            }

            Array.Sort(ticks);
            return new BenchmarkSample(
                _name,
                ticks.Length,
                ticks[ticks.Length / 2],
                ticks[0],
                ticks[ticks.Length - 1],
                minBytes,
                maxBytes);
        }
    }

    internal sealed class BenchmarkReport
    {
        public readonly string ContainerName;
        public readonly DateTime UtcDate;
        public readonly string UnityVersion;
        public readonly string Hardware;
        public readonly int PhaseItemCount;
        public readonly BenchmarkSample Build;
        public readonly BenchmarkSample BuildResolved;
        public readonly BenchmarkSample FirstResolveAll;
        public readonly BenchmarkSample Singleton10k;
        public readonly BenchmarkSample Transient10k;

        public BenchmarkReport(
            string containerName,
            int phaseItemCount,
            BenchmarkSample build,
            BenchmarkSample buildResolved,
            BenchmarkSample firstResolveAll,
            BenchmarkSample singleton10k,
            BenchmarkSample transient10k)
        {
            ContainerName = containerName;
            UtcDate = DateTime.UtcNow;
            UnityVersion = Application.unityVersion;
            Hardware =
                $"{SystemInfo.processorType} ({SystemInfo.processorCount} cores), " +
                $"{SystemInfo.systemMemorySize} MB, {SystemInfo.operatingSystem}";
            PhaseItemCount = phaseItemCount;
            Build = build;
            BuildResolved = buildResolved;
            FirstResolveAll = firstResolveAll;
            Singleton10k = singleton10k;
            Transient10k = transient10k;
        }

        public string Format()
        {
            var culture = CultureInfo.InvariantCulture;
            var builder = new StringBuilder();
            builder.AppendLine($"container: {ContainerName}");
            builder.AppendLine($"date: {UtcDate.ToString("yyyy-MM-dd HH:mm:ss", culture)} UTC");
            builder.AppendLine($"unity: {UnityVersion}");
            builder.AppendLine($"hardware: {Hardware}");
            builder.AppendLine(
                "warmup: 1 discarded full pass (Build + resolve every service + 12 ResolveAll from deepest + " +
                $"{BenchmarkGraph.ResolveIterations} singleton + {BenchmarkGraph.ResolveIterations} transient + Dispose). " +
                "JIT/domain reload is not in the numbers.");
            builder.AppendLine(
                $"runs: {Build.Runs} measured passes, fresh sessions each; GC.Collect + WaitForPendingFinalizers + " +
                "GC.Collect before every metric. Time = median (min-max), " +
                $"allocated = ProfilerRecorder \"{BenchmarkMeasure.AllocatedCounter}\" delta, min (max) over runs.");
            builder.AppendLine(
                $"graph: {BenchmarkGraph.Registrations.Length} services, depth {BenchmarkGraph.ScopeDepth}, " +
                $"{BenchmarkGraph.Markers.Length} marker interfaces, " +
                $"singleton={BenchmarkGraph.SingletonResolveType.Name}, " +
                $"transient={BenchmarkGraph.TransientResolveType.Name}, " +
                $"first ResolveAll item count={PhaseItemCount}");
            builder.AppendLine();
            builder.AppendLine("| Metric | Median (ms) | Min-max (ms) | Median ticks | Allocated bytes (min / max) |");
            builder.AppendLine("|---|---|---|---|---|");
            AppendRow(builder, Build, culture);
            AppendRow(builder, BuildResolved, culture);
            AppendRow(builder, FirstResolveAll, culture);
            AppendRow(builder, Singleton10k, culture);
            AppendRow(builder, Transient10k, culture);
            return builder.ToString();
        }

        private static void AppendRow(StringBuilder builder, BenchmarkSample sample, CultureInfo culture)
        {
            builder.Append("| ");
            builder.Append(sample.Name);
            builder.Append(" | ");
            builder.Append(sample.Milliseconds.ToString("0.0000", culture));
            builder.Append(" | ");
            builder.Append(sample.MinMilliseconds.ToString("0.0000", culture));
            builder.Append(" - ");
            builder.Append(sample.MaxMilliseconds.ToString("0.0000", culture));
            builder.Append(" | ");
            builder.Append(sample.Ticks.ToString(culture));
            builder.Append(" | ");
            builder.Append(sample.AllocatedBytes.ToString(culture));
            builder.Append(" / ");
            builder.Append(sample.MaxAllocatedBytes.ToString(culture));
            builder.AppendLine(" |");
        }
    }

    internal interface IBenchmarkSession : IDisposable
    {
        int ResolveAllPhases();
        object ResolveSingleton();
        object ResolveTransient();
        object Resolve(BenchmarkScopeLevel level, Type type);
    }

    internal static class ContainerBenchmarkRunner
    {
        // Нечётное число: медиана — один из реальных замеров.
        public const int Runs = 11;

        public static BenchmarkReport Run(string containerName, Func<IBenchmarkSession> open)
        {
            using (var warmup = open())
            {
                Keep(ResolveGraph(warmup));
                Keep(warmup.ResolveAllPhases());
                RunLoop(warmup.ResolveSingleton);
                RunLoop(warmup.ResolveTransient);
            }

            var build = new BenchmarkSeries("Build");
            var buildResolved = new BenchmarkSeries(
                $"Build + resolve all {BenchmarkGraph.Registrations.Length} services");
            var firstResolveAll = new BenchmarkSeries("First ResolveAll of 12 marker phases");
            var singleton = new BenchmarkSeries("10k Resolve singleton from deepest");
            var transient = new BenchmarkSeries("10k Transient");
            var phaseItemCount = 0;

            for (var run = 0; run < Runs; run++)
            {
                // Отдельная сессия: после полного резолва у VContainer все экземпляры уже созданы,
                // и остальные замеры перестали бы сравниваться с прошлыми прогонами.
                IBenchmarkSession resolved = null;
                buildResolved.Add(() =>
                {
                    resolved = open();
                    Keep(ResolveGraph(resolved));
                });
                resolved?.Dispose();

                IBenchmarkSession session = null;
                build.Add(() => session = open());

                try
                {
                    // Делегаты создаются до замера, иначе их байты попадут в метрику.
                    Func<object> resolveSingleton = session.ResolveSingleton;
                    Func<object> resolveTransient = session.ResolveTransient;
                    firstResolveAll.Add(() => phaseItemCount = session.ResolveAllPhases());
                    singleton.Add(() => RunLoop(resolveSingleton));
                    transient.Add(() => RunLoop(resolveTransient));
                }
                finally
                {
                    session?.Dispose();
                }
            }

            return new BenchmarkReport(
                containerName,
                phaseItemCount,
                build.ToSample(),
                buildResolved.ToSample(),
                firstResolveAll.ToSample(),
                singleton.ToSample(),
                transient.ToSample());
        }

        // Каждый сервис со своего уровня. VContainer создаёт экземпляры лениво, при резолве;
        // сгенерированный класс создаёт singleton/scoped уже в конструкторе скоупа.
        private static int ResolveGraph(IBenchmarkSession session)
        {
            var registrations = BenchmarkGraph.Registrations;
            for (var i = 0; i < registrations.Length; i++)
            {
                var registration = registrations[i];
                if (session.Resolve(registration.Scope, registration.Implementation) == null)
                    throw new InvalidOperationException("Resolve returned null for " + registration.Implementation.Name);
            }

            return registrations.Length;
        }

        private static void RunLoop(Func<object> resolve)
        {
            object sink = null;
            for (var i = 0; i < BenchmarkGraph.ResolveIterations; i++)
                sink = resolve();

            if (sink == null)
                throw new InvalidOperationException("Resolve returned null");
        }

        private static void Keep(int count)
        {
            if (count < 0)
                throw new InvalidOperationException("ResolveAll returned a negative count");
        }
    }

    internal static class BenchmarkMeasure
    {
        // GC.GetAllocatedBytesForCurrentThread в Unity Mono — заглушка и всегда 0, GC.GetTotalMemory
        // шагает страницами по 4 КБ. Счётчик профайлера считает каждую managed-аллокацию
        // (16 байт на object) без открытого окна Profiler, но сбрасывается в конце кадра.
        public const string AllocatedCounter = "GC Allocated In Frame";

        public static BenchmarkRun Capture(Action action)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, AllocatedCounter))
            {
                if (recorder.Valid == false)
                    throw new InvalidOperationException($"Profiler counter \"{AllocatedCounter}\" is not available");

                var allocatedBefore = recorder.CurrentValue;
                var start = System.Diagnostics.Stopwatch.GetTimestamp();
                action();
                var ticks = System.Diagnostics.Stopwatch.GetTimestamp() - start;
                var allocatedAfter = recorder.CurrentValue;

                // Count растёт, когда профайлер закрывает кадр: тогда счётчик сброшен и дельта ложная.
                if (recorder.Count > 0 || allocatedAfter < allocatedBefore)
                    throw new InvalidOperationException("Profiler frame ended during measurement, allocated bytes are invalid");

                return new BenchmarkRun(ticks, allocatedAfter - allocatedBefore);
            }
        }
    }
}
