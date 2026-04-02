using Benchmarks;
using Common.Extensions;
using Infrastructure.Startup;
using Microsoft.AspNetCore.Mvc;

namespace ConsoleGateway;

public static class BenchmarkEndpoints {
    public static IEndpointRouteBuilder AddBenchmarkEndpoints(this IEndpointRouteBuilder builder) {
        var group = builder.MapGroup("/api/benchmarks")
            .AddEndpointFilter<ClusterReadyFilter>();

        group.MapGet("/", ListAll);
        group.MapGet("/group/{group}", ListByGroup);
        group.MapPost("/{title}/run", RunSingle);
        group.MapPost("/{title}/cancel", CancelSingle);
        group.MapPost("/group/{group}/run", RunGroup);
        group.MapGet("/{title}/history", GetHistory);
        group.MapGet("/group/{group}/history", GetGroupHistory);

        return builder;
    }

    private class ClusterReadyFilter(IClusterParticipantContext participantContext) : IEndpointFilter {
        public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
            if (!participantContext.IsInitialized.Value)
                return ValueTask.FromResult<object?>(Results.StatusCode(503));

            return next(context);
        }
    }

    private static List<BenchmarkInfoDto> ListAll(
        [FromServices] IEnumerable<IClusterTest> tests,
        [FromServices] BenchmarkRunner runner) {
        return tests.Select(t => ToInfo(t, runner)).ToList();
    }

    private static List<BenchmarkInfoDto> ListByGroup(
        string group,
        [FromServices] IEnumerable<IClusterTest> tests,
        [FromServices] BenchmarkRunner runner) {
        return tests
            .Where(t => string.Equals(t.Group, group, StringComparison.OrdinalIgnoreCase))
            .Select(t => ToInfo(t, runner))
            .ToList();
    }

    private static IResult RunSingle(
        string title,
        [FromServices] IEnumerable<IClusterTest> tests,
        [FromServices] BenchmarkRunner runner) {
        var test = tests.FirstOrDefault(t => t.Title == title);
        if (test == null)
            return Results.NotFound($"Benchmark '{title}' not found");

        runner.Start(test);
        return Results.Ok(new BenchmarkRunResultDto {
            Title = test.Title,
            Success = true,
            MetricName = test.MetricName
        });
    }

    private static IResult CancelSingle(
        string title,
        [FromServices] BenchmarkRunner runner) {
        var cancelled = runner.Cancel(title);
        if (!cancelled)
            return Results.NotFound($"Benchmark '{title}' is not running");

        return Results.Ok();
    }

    private static List<BenchmarkRunResultDto> RunGroup(
        string group,
        [FromServices] IEnumerable<IClusterTest> tests,
        [FromServices] BenchmarkRunner runner) {
        var groupTests = tests
            .Where(t => string.Equals(t.Group, group, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var results = new List<BenchmarkRunResultDto>();
        foreach (var test in groupTests) {
            runner.Start(test);
            results.Add(new BenchmarkRunResultDto {
                Title = test.Title,
                Success = true,
                MetricName = test.MetricName
            });
        }

        return results;
    }

    private static async Task<IResult> GetHistory(
        string title,
        [FromServices] BenchmarkStorage storage) {
        var states = await storage.GetAll(title);
        var entries = states.Select(ToHistoryEntry).ToList();
        return Results.Ok(entries);
    }

    private static async Task<List<BenchmarkHistoryGroupDto>> GetGroupHistory(
        string group,
        [FromServices] IEnumerable<IClusterTest> tests,
        [FromServices] BenchmarkStorage storage) {
        var groupTests = tests
            .Where(t => string.Equals(t.Group, group, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var results = new List<BenchmarkHistoryGroupDto>();
        foreach (var test in groupTests) {
            var states = await storage.GetAll(test.Title);
            results.Add(new BenchmarkHistoryGroupDto {
                Title = test.Title,
                MetricName = test.MetricName,
                Entries = states.Select(ToHistoryEntry).ToList()
            });
        }

        return results;
    }

    private static BenchmarkInfoDto ToInfo(IClusterTest test, BenchmarkRunner runner) {
        return new BenchmarkInfoDto {
            Title = test.Title,
            Group = test.Group,
            MetricName = test.MetricName,
            IsRunning = runner.IsRunning(test.Title),
            LastMetricValue = test.LastResult?.MetricValue,
            LastSuccess = test.LastResult?.Success,
            LastDurationMs = test.LastResult?.DurationMs
        };
    }

    private static BenchmarkHistoryEntryDto ToHistoryEntry(BenchmarkState state) {
        var totalCount = state.Records.Sum(r => r.Count);
        var metricValue = state.Duration.TotalSeconds > 0
            ? totalCount / state.Duration.TotalSeconds
            : 0;

        return new BenchmarkHistoryEntryDto {
            Id = state.Id,
            Date = state.Date,
            DurationMs = (long)state.Duration.TotalMilliseconds,
            Success = state.Success,
            MetricValue = metricValue,
            TotalOperations = totalCount,
            Samples = state.Records.Select(r => new BenchmarkSampleDto {
                Count = r.Count,
                TimeMs = (long)r.Time.TotalMilliseconds
            }).ToList()
        };
    }
}

public record BenchmarkInfoDto {
    public required string Title { get; init; }
    public required string Group { get; init; }
    public required string MetricName { get; init; }
    public bool IsRunning { get; init; }
    public double? LastMetricValue { get; init; }
    public bool? LastSuccess { get; init; }
    public long? LastDurationMs { get; init; }
}

public record BenchmarkRunResultDto {
    public required string Title { get; init; }
    public required bool Success { get; init; }
    public double MetricValue { get; init; }
    public string MetricName { get; init; } = string.Empty;
    public long DurationMs { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
}

public record BenchmarkHistoryEntryDto {
    public required Guid Id { get; init; }
    public required DateTime Date { get; init; }
    public required long DurationMs { get; init; }
    public required bool Success { get; init; }
    public required double MetricValue { get; init; }
    public required int TotalOperations { get; init; }
    public required List<BenchmarkSampleDto> Samples { get; init; }
}

public record BenchmarkHistoryGroupDto {
    public required string Title { get; init; }
    public required string MetricName { get; init; }
    public required List<BenchmarkHistoryEntryDto> Entries { get; init; }
}

public record BenchmarkSampleDto {
    public required int Count { get; init; }
    public required long TimeMs { get; init; }
}
