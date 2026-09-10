using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Internal.Tests
{
    internal readonly struct BenchmarkSample
    {
        public readonly string Name;
        public readonly long Ticks;
        public readonly double Milliseconds;
        public readonly long AllocatedBytes;

        public BenchmarkSample(string name, long ticks, double milliseconds, long allocatedBytes)
        {
            Name = name;
            Ticks = ticks;
            Milliseconds = milliseconds;
            AllocatedBytes = allocatedBytes;
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
        public readonly BenchmarkSample FirstResolveAll;
        public readonly BenchmarkSample Singleton10k;
        public readonly BenchmarkSample Transient10k;

        public BenchmarkReport(
            string containerName,
            int phaseItemCount,
            BenchmarkSample build,
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
                "warmup: 1 discarded full pass (Build + 12 ResolveAll from deepest + " +
                $"{BenchmarkGraph.ResolveIterations} singleton + {BenchmarkGraph.ResolveIterations} transient + Dispose), " +
                "then GC.Collect + WaitForPendingFinalizers + GC.Collect. JIT/domain reload is not in the numbers.");
            builder.AppendLine(
                $"graph: {BenchmarkGraph.Registrations.Length} services, depth {BenchmarkGraph.ScopeDepth}, " +
                $"{BenchmarkGraph.Markers.Length} marker interfaces, " +
                $"singleton={BenchmarkGraph.SingletonResolveType.Name}, " +
                $"transient={BenchmarkGraph.TransientResolveType.Name}, " +
                $"first ResolveAll item count={PhaseItemCount}");
            builder.AppendLine();
            builder.AppendLine("| Metric | Time (ms) | Ticks | Allocated bytes |");
            builder.AppendLine("|---|---|---|---|");
            AppendRow(builder, Build, culture);
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
            builder.Append(sample.Milliseconds.ToString("G17", culture));
            builder.Append(" | ");
            builder.Append(sample.Ticks.ToString(culture));
            builder.Append(" | ");
            builder.Append(sample.AllocatedBytes.ToString(culture));
            builder.AppendLine(" |");
        }
    }

    internal interface IBenchmarkSession : IDisposable
    {
        int ResolveAllPhases();
        object ResolveSingleton();
        object ResolveTransient();
    }

    internal static class ContainerBenchmarkRunner
    {
        public static BenchmarkReport Run(string containerName, Func<IBenchmarkSession> open)
        {
            using (var warmup = open())
            {
                Keep(warmup.ResolveAllPhases());
                RunLoop(warmup.ResolveSingleton);
                RunLoop(warmup.ResolveTransient);
            }

            IBenchmarkSession session = null;
            var build = BenchmarkMeasure.Capture("Build", () => session = open());

            try
            {
                var phaseItemCount = 0;
                var firstResolveAll = BenchmarkMeasure.Capture(
                    "First ResolveAll of 12 marker phases",
                    () => phaseItemCount = session.ResolveAllPhases());
                var singleton = BenchmarkMeasure.Capture(
                    "10k Resolve singleton from deepest",
                    () => RunLoop(session.ResolveSingleton));
                var transient = BenchmarkMeasure.Capture(
                    "10k Transient",
                    () => RunLoop(session.ResolveTransient));

                return new BenchmarkReport(
                    containerName,
                    phaseItemCount,
                    build,
                    firstResolveAll,
                    singleton,
                    transient);
            }
            finally
            {
                session?.Dispose();
            }
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
        public static BenchmarkSample Capture(string name, Action action)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var start = StopwatchTimestamp();
            action();
            var ticks = StopwatchTimestamp() - start;
            var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            return new BenchmarkSample(
                name,
                ticks,
                ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency,
                allocatedAfter - allocatedBefore);
        }

        private static long StopwatchTimestamp()
        {
            return System.Diagnostics.Stopwatch.GetTimestamp();
        }
    }
}
