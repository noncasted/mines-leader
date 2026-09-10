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

        [Test]
        [Explicit("Opt-in benchmark. Select and Run Selected in Test Runner.")]
        public void VContainer_SyntheticGraph_RecordsMetrics()
        {
            var report = VContainerBenchmarkHost.Run();
            Log(report);
            AssertReport(report, "VContainer");
        }

        [Test]
        [Explicit("Opt-in benchmark. Own container may throw until track A is complete.")]
        public void OwnContainer_SyntheticGraph_RecordsMetrics()
        {
            var report = OwnContainerBenchmarkHost.Run();
            Log(report);
            AssertReport(report, "Own");
        }

        [Test]
        [Explicit("Opt-in benchmark. Generated class waits steps 1, 1c and 2.")]
        public void GeneratedContainer_SyntheticGraph_RecordsMetrics()
        {
            var report = GeneratedContainerBenchmarkHost.Run();
            Log(report);
            AssertReport(report, GeneratedContainerBenchmarkHost.ContainerName);
        }

        [Test]
        public void GeneratedHost_IsThirdColumn()
        {
            Assert.AreEqual("Generated", GeneratedContainerBenchmarkHost.ContainerName);
        }

        [MenuItem("Tools/Container/Run Benchmark")]
        private static void RunFromMenu()
        {
            var vcontainer = VContainerBenchmarkHost.Run();
            Debug.Log(vcontainer.Format());

            try
            {
                var own = OwnContainerBenchmarkHost.Run();
                Debug.Log(own.Format());
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Runtime-plan benchmark threw (do not invent numbers): " +
                    $"{exception.GetType().Name}: {exception.Message}");
            }

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
            var text = report.Format();
            Debug.Log(text);
            TestContext.WriteLine(text);
        }

        private static void AssertReport(BenchmarkReport report, string containerName)
        {
            Assert.AreEqual(containerName, report.ContainerName);
            Assert.GreaterOrEqual(report.Build.Ticks, 0);
            Assert.GreaterOrEqual(report.FirstResolveAll.Ticks, 0);
            Assert.GreaterOrEqual(report.Singleton10k.Ticks, 0);
            Assert.GreaterOrEqual(report.Transient10k.Ticks, 0);
            Assert.GreaterOrEqual(report.Build.AllocatedBytes, 0);
            Assert.GreaterOrEqual(report.FirstResolveAll.AllocatedBytes, 0);
            Assert.GreaterOrEqual(report.Singleton10k.AllocatedBytes, 0);
            Assert.GreaterOrEqual(report.Transient10k.AllocatedBytes, 0);
            Assert.GreaterOrEqual(report.PhaseItemCount, 0);
        }
    }
}
