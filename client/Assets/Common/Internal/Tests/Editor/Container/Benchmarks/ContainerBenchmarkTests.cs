using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Internal.Tests
{
    [Category("Container")]
    public class ContainerBenchmarkTests
    {
        [Test]
        public void Graph_MatchesProductionShape()
        {
            Assert.AreEqual(3, BenchmarkGraph.ScopeDepth);
            Assert.AreEqual(12, BenchmarkGraph.Markers.Length);
            Assert.AreEqual(32, BenchmarkGraph.Registrations.Length);
            Assert.AreEqual(18, BenchmarkGraph.Count(ServiceLifetime.Singleton));
            Assert.AreEqual(9, BenchmarkGraph.Count(ServiceLifetime.Scoped));
            Assert.AreEqual(5, BenchmarkGraph.Count(ServiceLifetime.Transient));
            Assert.AreEqual(12, BenchmarkGraph.Count(BenchmarkScopeLevel.Root));
            Assert.AreEqual(12, BenchmarkGraph.Count(BenchmarkScopeLevel.Match));
            Assert.AreEqual(8, BenchmarkGraph.Count(BenchmarkScopeLevel.Card));
            Assert.AreEqual(typeof(BenchRootTime), BenchmarkGraph.SingletonResolveType);
            Assert.AreEqual(typeof(BenchCardAction), BenchmarkGraph.TransientResolveType);
        }

        // Счётчик аллокаций уже подводил (GetAllocatedBytesForCurrentThread = 0 в Unity Mono):
        // сверяем с известным размером. Минимум из нескольких замеров — чужие потоки редактора.
        [Test]
        public void Measure_CountsManagedAllocations()
        {
            object sink = null;
            var empty = long.MaxValue;
            var array = long.MaxValue;
            for (var i = 0; i < 5; i++)
            {
                empty = Math.Min(empty, BenchmarkMeasure.Capture(() => { }).AllocatedBytes);
                array = Math.Min(array, BenchmarkMeasure.Capture(() => sink = new byte[1000]).AllocatedBytes);
            }

            Assert.IsNotNull(sink);
            Assert.AreEqual(0, empty);
            Assert.GreaterOrEqual(array, 1000);
            Assert.Less(array, 1100);
        }

        [Test]
        [Explicit("Opt-in benchmark. Select and Run Selected in Test Runner.")]
        public void VContainer_SyntheticGraph_RecordsMetrics()
        {
            var report = VContainerBenchmarkHost.Run();
            Log(report);
            AssertReport(report, "VContainer");
        }

        [Test]
        [Explicit("Opt-in benchmark. Select and Run Selected in Test Runner.")]
        public void GeneratedContainer_SyntheticGraph_RecordsMetrics()
        {
            var report = GeneratedContainerBenchmarkHost.Run();
            Log(report);
            AssertReport(report, GeneratedContainerBenchmarkHost.ContainerName);
        }

        [Test]
        [Explicit("Opt-in allocation breakdown. Select and Run Selected in Test Runner.")]
        public void GeneratedContainer_BuildAllocations_ByStep()
        {
            using (GeneratedContainerBenchmarkHost.RegisterScopes())
            {
                GeneratedContainerBenchmarkHost.OpenSession().Dispose();
                Log("Diagnostics on\n" + GeneratedContainerBenchmarkHost.BuildAllocations(ContainerBenchmarkRunner.Runs));

                using (GeneratedContainerBenchmarkHost.WithoutDiagnostics())
                    Log("Diagnostics off\n" + GeneratedContainerBenchmarkHost.BuildAllocations(ContainerBenchmarkRunner.Runs));
            }
        }

        // BenchmarkRoots дублирует таблицу статическими вызовами: сверяем, что граф тот же.
        [Test]
        public void GeneratedContainer_Roots_MatchTable()
        {
            using (GeneratedContainerBenchmarkHost.RegisterScopes())
            using (var session = GeneratedContainerBenchmarkHost.OpenSession())
            {
                foreach (var registration in BenchmarkGraph.Registrations)
                {
                    var scope = session.Scope(registration.Scope);
                    Assert.IsInstanceOf(
                        registration.Implementation,
                        scope.Resolve(registration.Implementation),
                        $"{registration.Implementation.Name} at {registration.Scope}");
                }

                foreach (BenchmarkScopeLevel level in Enum.GetValues(typeof(BenchmarkScopeLevel)))
                {
                    foreach (var marker in BenchmarkGraph.Markers)
                    {
                        Assert.AreEqual(
                            CountMarker(level, marker),
                            ResolveAllCount(session.Scope(level), marker),
                            $"{marker.Name} at {level}");
                    }
                }
            }
        }

        private static int CountMarker(BenchmarkScopeLevel level, Type marker)
        {
            var count = 0;
            foreach (var registration in BenchmarkGraph.Registrations)
            {
                if (registration.Scope == level && Array.IndexOf(registration.Markers, marker) >= 0)
                    count++;
            }

            return count;
        }

        private static int ResolveAllCount(IContainer container, Type marker)
        {
            var method = typeof(IContainer).GetMethod(nameof(IContainer.ResolveAll)).MakeGenericMethod(marker);
            return ((System.Collections.ICollection)method.Invoke(container, null)).Count;
        }

        [MenuItem("Tools/Container/Run Benchmark")]
        private static void RunFromMenu()
        {
            var vcontainer = VContainerBenchmarkHost.Run();
            Debug.Log(vcontainer.Format());

            try
            {
                var generated = GeneratedContainerBenchmarkHost.Run();
                Debug.Log(generated.Format());
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Generated container benchmark threw (do not invent numbers): " +
                    $"{exception.GetType().Name}: {exception.Message}");
            }
        }

        private static void Log(BenchmarkReport report)
        {
            Log(report.Format());
        }

        private static void Log(string text)
        {
            Debug.Log(text);
            TestContext.WriteLine(text);
        }

        private static void AssertReport(BenchmarkReport report, string containerName)
        {
            Assert.AreEqual(containerName, report.ContainerName);
            Assert.GreaterOrEqual(report.Build.Ticks, 0);
            Assert.Greater(report.BuildResolved.Ticks, 0);
            Assert.GreaterOrEqual(report.FirstResolveAll.Ticks, 0);
            Assert.GreaterOrEqual(report.Singleton10k.Ticks, 0);
            Assert.GreaterOrEqual(report.Transient10k.Ticks, 0);
            Assert.Greater(report.Build.AllocatedBytes, 0);
            Assert.Greater(report.BuildResolved.AllocatedBytes, 0);
            Assert.GreaterOrEqual(report.FirstResolveAll.AllocatedBytes, 0);
            Assert.GreaterOrEqual(report.Singleton10k.AllocatedBytes, 0);
            Assert.Greater(report.Transient10k.AllocatedBytes, 0);
            Assert.GreaterOrEqual(report.PhaseItemCount, 0);
        }
    }
}
