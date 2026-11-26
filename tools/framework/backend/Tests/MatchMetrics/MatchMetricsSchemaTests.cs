using FluentAssertions;
using Game.MatchMetrics;
using Xunit;

namespace Framework.Tests.MatchMetrics;

// The tables the deploy setup creates: a second run changes nothing, the columns and keys are the ones the storage writes.
public class MatchMetricsSchemaTests : IClassFixture<MatchMetricsDatabase>
{
    public MatchMetricsSchemaTests(MatchMetricsDatabase database)
    {
        _database = database;
    }

    private readonly MatchMetricsDatabase _database;

    [Fact]
    public async Task Create_again_keeps_the_tables()
    {
        await using var connection = await _database.Value.OpenConnectionAsync();
        await MatchMetricsSchema.Create(connection);

        foreach (var (table, _) in MatchMetricsSchema.Tables)
            (await _database.Scalar<bool>("SELECT to_regclass(@t) IS NOT NULL", ("t", table))).Should().BeTrue(table);
    }

    [Theory]
    [InlineData(MatchMetricsSchema.Series, "id:integer,name:text,kind:smallint,tags:jsonb")]
    [InlineData(MatchMetricsSchema.Streams, "id:uuid,match_id:uuid,user_id:uuid,writer_id:uuid,kind:smallint,opened_at:timestamp with time zone,last_seq:integer,last_at:timestamp with time zone")]
    [InlineData(MatchMetricsSchema.Slots, "stream_id:uuid,slot:smallint,series_id:integer")]
    [InlineData(MatchMetricsSchema.Chunks, "stream_id:uuid,seq:integer,series_id:integer,start:integer,at:timestamp with time zone,values:ARRAY")]
    [InlineData(MatchMetricsSchema.Totals, "stream_id:uuid,series_id:integer,sum:double precision,count:integer,max:real")]
    [InlineData(MatchMetricsSchema.Events, "stream_id:uuid,seq:integer,idx:smallint,time:real,at:timestamp with time zone,name:text,fields:jsonb")]
    public async Task A_table_has_its_columns(string table, string columns)
    {
        const string sql = """
                           SELECT string_agg(column_name || ':' || data_type, ',' ORDER BY ordinal_position)
                           FROM information_schema.columns WHERE table_name = @t
                           """;

        (await _database.Scalar<string>(sql, ("t", table))).Should().Be(columns);
    }

    [Theory]
    [InlineData(MatchMetricsSchema.Streams, "PRIMARY KEY (id)|UNIQUE (match_id, user_id, writer_id)")]
    [InlineData(MatchMetricsSchema.Chunks, "PRIMARY KEY (stream_id, seq, series_id, start)")]
    [InlineData(MatchMetricsSchema.Totals, "PRIMARY KEY (stream_id, series_id)")]
    [InlineData(MatchMetricsSchema.Events, "PRIMARY KEY (stream_id, seq, idx)")]
    [InlineData(MatchMetricsSchema.Series, "PRIMARY KEY (id)|UNIQUE (name, tags)")]
    public async Task A_table_has_its_keys(string table, string keys)
    {
        const string sql = """
                           SELECT string_agg(pg_get_constraintdef(oid), '|' ORDER BY contype, conname)
                           FROM pg_constraint WHERE conrelid = to_regclass(@t)
                           """;

        (await _database.Scalar<string>(sql, ("t", table))).Should().Be(keys);
    }
}
