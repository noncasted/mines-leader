using System.Diagnostics.CodeAnalysis;
using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

public class RuntimePipeRetryStressTest
{
    [GenerateSerializer]
    public class PipeRequest
    {
        [Id(0)] public int Index { get; set; }
    }

    [GenerateSerializer]
    public class PipeResponse
    {
        [Id(0)] public int Index { get; set; }
        [Id(1)] public int Attempt { get; set; }
    }

    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
    {
        [Id(0)]
        public int RequestCount { get; set; } = 5000;

        [Id(1)]
        public double FailureRate { get; set; } = 0.3;

        [Id(2)]
        public int Concurrency { get; set; } = 50;
    }

    public static string TestName => "runtime-pipe-retry-stress";

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        public override string Group => TestGroups.Messaging;
        public override string Subgroup => TestGroups.Subgroups.RuntimePipe;
        public override string Title => "Retry stress (intermittent failures)";
        public override string MetricName => "req/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var pipeId = new RuntimePipeId(TestName);
            var totalRequests = payload.RequestCount;
            var failureRate = payload.FailureRate;
            var successCount = 0;
            var retryCount = 0;

            // Handler that fails intermittently
            await Messaging.AddPipeRequestHandler<PipeRequest, PipeResponse>(handle.Lifetime, pipeId, req => {
                if (Random.Shared.NextDouble() < failureRate)
                {
                    Interlocked.Increment(ref retryCount);
                    throw new Exception("Transient failure");
                }

                return Task.FromResult(new PipeResponse { Index = req.Index, Attempt = 1 });
            });

            handle.Progress.Log(
                $"Handler ready (failure rate: {failureRate:P0}). Sending {totalRequests} requests (concurrency: {payload.Concurrency})...");

            var semaphore = new SemaphoreSlim(payload.Concurrency);
            var completedCount = 0;

            var tasks = Enumerable.Range(0, totalRequests).Select(async i => {
                await semaphore.WaitAsync(handle.CancellationToken);

                try
                {
                    await Messaging.SendPipe<PipeResponse>(pipeId, new PipeRequest { Index = i });
                    Interlocked.Increment(ref successCount);
                    handle.Metrics.Inc();
                }
                catch
                {
                    // All retries exhausted for this request
                }
                finally
                {
                    semaphore.Release();

                    var completed = Interlocked.Increment(ref completedCount);

                    if (completed % 500 == 0)
                    {
                        handle.Progress.SetProgress((float)completed / totalRequests);

                        handle.Progress.Log(
                            $"Progress: {completed}/{totalRequests}, success: {successCount}, handler failures: {retryCount}");
                    }
                }
            });

            await Task.WhenAll(tasks);

            handle.Progress.Log($"Done. Success: {successCount}/{totalRequests}, handler failures: {retryCount}");
            handle.Progress.SetProgress(1f);
        }
    }
}