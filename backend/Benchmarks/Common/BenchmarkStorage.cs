using System.Text.Json;
using Common.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Benchmarks;

public class BenchmarkStorage {
    public const string TableName = "benchmark_results";
    public const string SnapshotsTableName = "benchmark_snapshots";

    public BenchmarkStorage(IDbSource dbSource, ILogger<BenchmarkStorage> logger) {
        _dbSource = dbSource;
        _logger = logger;
    }

    private readonly IDbSource _dbSource;
    private readonly ILogger<BenchmarkStorage> _logger;

    public async Task Save(BenchmarkResult result) {
        try {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand($"""
                INSERT INTO {TableName} (id, benchmark_name, "group", metric_name, metric_value, duration_ms, payload_json, timestamp, success, error_message)
                VALUES (@id, @name, @group, @metric, @value, @duration, @payload, @ts, @success, @error)
                """, connection);

            cmd.Parameters.AddWithValue("id", result.Id);
            cmd.Parameters.AddWithValue("name", result.BenchmarkName);
            cmd.Parameters.AddWithValue("group", result.Group);
            cmd.Parameters.AddWithValue("metric", result.MetricName);
            cmd.Parameters.AddWithValue("value", result.MetricValue);
            cmd.Parameters.AddWithValue("duration", result.DurationMs);
            cmd.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, result.PayloadJson);
            cmd.Parameters.AddWithValue("ts", result.Timestamp);
            cmd.Parameters.AddWithValue("success", result.Success);
            cmd.Parameters.AddWithValue("error", result.ErrorMessage);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception e) {
            _logger.LogError(e, "[BenchmarkStorage] Failed to save benchmark result for {Name}", result.BenchmarkName);
        }
    }

    public async Task<IReadOnlyList<BenchmarkResult>> GetHistory(string benchmarkName, int limit = 20) {
        try {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand($"""
                SELECT id, benchmark_name, "group", metric_name, metric_value, duration_ms, payload_json, timestamp, success, error_message
                FROM {TableName}
                WHERE benchmark_name = @name
                ORDER BY timestamp DESC
                LIMIT @limit
                """, connection);

            cmd.Parameters.AddWithValue("name", benchmarkName);
            cmd.Parameters.AddWithValue("limit", limit);

            var results = new List<BenchmarkResult>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync()) {
                results.Add(new BenchmarkResult {
                    Id = reader.GetGuid(0),
                    BenchmarkName = reader.GetString(1),
                    Group = reader.GetString(2),
                    MetricName = reader.GetString(3),
                    MetricValue = reader.GetDouble(4),
                    DurationMs = reader.GetInt64(5),
                    PayloadJson = reader.GetString(6),
                    Timestamp = reader.GetDateTime(7),
                    Success = reader.GetBoolean(8),
                    ErrorMessage = reader.GetString(9)
                });
            }

            return results;
        }
        catch (Exception e) {
            _logger.LogError(e, "[BenchmarkStorage] Failed to get history for {Name}", benchmarkName);
            return Array.Empty<BenchmarkResult>();
        }
    }

    public async Task<BenchmarkResult?> GetPrevious(string benchmarkName) {
        var history = await GetHistory(benchmarkName, 1);
        return history.Count > 0 ? history[0] : null;
    }

    public async Task SaveSnapshots(Guid resultId, IReadOnlyList<BenchmarkSnapshot> snapshots) {
        if (snapshots.Count == 0) return;

        try {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var batch = new NpgsqlBatch(connection);

            foreach (var snapshot in snapshots) {
                var cmd = new NpgsqlBatchCommand($"""
                    INSERT INTO {SnapshotsTableName} (result_id, step_index, step_percent, metric_value, diff_value)
                    VALUES ($1, $2, $3, $4, $5)
                    """);
                cmd.Parameters.AddWithValue(resultId);
                cmd.Parameters.AddWithValue(snapshot.StepIndex);
                cmd.Parameters.AddWithValue(snapshot.StepPercent);
                cmd.Parameters.AddWithValue(snapshot.MetricValue);
                cmd.Parameters.AddWithValue(snapshot.DiffValue);
                batch.BatchCommands.Add(cmd);
            }

            await batch.ExecuteNonQueryAsync();
        }
        catch (Exception e) {
            _logger.LogError(e, "[BenchmarkStorage] Failed to save snapshots for result {ResultId}", resultId);
        }
    }

    public async Task<Dictionary<Guid, IReadOnlyList<BenchmarkSnapshot>>> GetSnapshots(string benchmarkName, int runLimit = 10) {
        try {
            await using var connection = await _dbSource.Value.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand($"""
                SELECT s.result_id, s.step_index, s.step_percent, s.metric_value, s.diff_value
                FROM {SnapshotsTableName} s
                INNER JOIN {TableName} r ON r.id = s.result_id
                WHERE r.benchmark_name = @name AND r.success = true
                ORDER BY r.timestamp DESC, s.step_index ASC
                """, connection);

            cmd.Parameters.AddWithValue("name", benchmarkName);

            var result = new Dictionary<Guid, List<BenchmarkSnapshot>>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync()) {
                var resultId = reader.GetGuid(0);

                if (!result.TryGetValue(resultId, out var list)) {
                    if (result.Count >= runLimit) continue;
                    list = new List<BenchmarkSnapshot>();
                    result[resultId] = list;
                }

                list.Add(new BenchmarkSnapshot {
                    ResultId = resultId,
                    StepIndex = reader.GetInt32(1),
                    StepPercent = reader.GetFloat(2),
                    MetricValue = reader.GetDouble(3),
                    DiffValue = reader.GetDouble(4)
                });
            }

            return result.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<BenchmarkSnapshot>)kv.Value);
        }
        catch (Exception e) {
            _logger.LogError(e, "[BenchmarkStorage] Failed to get snapshots for {Name}", benchmarkName);
            return new Dictionary<Guid, IReadOnlyList<BenchmarkSnapshot>>();
        }
    }
}
