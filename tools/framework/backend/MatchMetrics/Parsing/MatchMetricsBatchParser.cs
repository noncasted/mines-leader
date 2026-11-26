using System.Globalization;
using System.Text;
using Shared;

namespace Game.MatchMetrics;

// The client is not trusted: every rule of the contract is checked here, a batch that breaks one throws
// MatchMetricsRejectedException. Names, keys and texts pass only the closed charset of SharedMatchMetricNames, so the json built
// from them needs no escaping.
internal static class MatchMetricsBatchParser
{
    // Slots: the series of the stream known before this batch and their kinds.
    public static ParsedMatchBatch Parse(SharedMatchMetrics.Batch batch, IReadOnlyDictionary<int, SharedMatchMetrics.MatchSeriesKind> slots)
    {
        if (batch.Seq < 1)
            throw Reject($"Seq {batch.Seq} below 1");

        var series = ParseSeries(batch.Series ?? Array.Empty<SharedMatchMetrics.MatchSeriesDecl>(), slots);
        var chunks = ParseChunks(batch.Chunks ?? Array.Empty<SharedMatchMetrics.MatchChunk>(), slots, series);
        var events = ParseEvents(batch.Events ?? Array.Empty<SharedMatchMetrics.MatchEvent>());

        return new ParsedMatchBatch(batch.Seq, series, chunks, events);
    }

    private static List<ParsedMatchSeries> ParseSeries(
        SharedMatchMetrics.MatchSeriesDecl[] declarations,
        IReadOnlyDictionary<int, SharedMatchMetrics.MatchSeriesKind> slots)
    {
        if (declarations.Length > SharedMatchMetricsLimits.MaxSeries)
            throw Reject($"{declarations.Length} series over {SharedMatchMetricsLimits.MaxSeries}");

        var result = new List<ParsedMatchSeries>(declarations.Length);
        var taken = new HashSet<int>();
        var keys = new HashSet<MatchSeriesKey>();

        foreach (var declaration in declarations)
        {
            if (declaration.Slot is < 0 or >= SharedMatchMetricsLimits.MaxSeries)
                throw Reject($"The slot {declaration.Slot} out of 0..{SharedMatchMetricsLimits.MaxSeries - 1}");

            if (slots.ContainsKey(declaration.Slot) || taken.Add(declaration.Slot) == false)
                throw Reject($"The slot {declaration.Slot} is declared again");

            if (SharedMatchMetricNames.IsName(declaration.Name) == false)
                throw Reject("A series name breaks the rule of names");

            if (Enum.IsDefined(declaration.Kind) == false)
                throw Reject($"The kind {declaration.Kind} of a series");

            var key = new MatchSeriesKey(declaration.Name, ParseTags(declaration.TagKeys, declaration.TagValues));

            if (keys.Add(key) == false)
                throw Reject($"The series {key.Name} {key.Tags} in two slots");

            result.Add(new ParsedMatchSeries(declaration.Slot, key, declaration.Kind));
        }

        return result;
    }

    private static string ParseTags(string[]? keys, string[]? values)
    {
        keys ??= Array.Empty<string>();
        values ??= Array.Empty<string>();

        if (keys.Length != values.Length || keys.Length > SharedMatchMetricsLimits.MaxTags)
            throw Reject($"Tags {keys.Length} keys, {values.Length} values, at most {SharedMatchMetricsLimits.MaxTags}");

        var tags = new SortedDictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < keys.Length; i++)
        {
            if (SharedMatchMetricNames.IsKey(keys[i]) == false || SharedMatchMetricNames.IsValue(values[i]) == false)
                throw Reject("A tag breaks the rule of names");

            if (tags.TryAdd(keys[i], values[i]) == false)
                throw Reject($"The tag key '{keys[i]}' twice");
        }

        var json = new StringBuilder("{");

        foreach (var (key, value) in tags)
        {
            if (json.Length > 1)
                json.Append(',');

            json.Append('"').Append(key).Append("\":\"").Append(value).Append('"');
        }

        return json.Append('}').ToString();
    }

    private static List<ParsedMatchChunk> ParseChunks(
        SharedMatchMetrics.MatchChunk[] chunks,
        IReadOnlyDictionary<int, SharedMatchMetrics.MatchSeriesKind> slots,
        List<ParsedMatchSeries> declared)
    {
        if (chunks.Length > MatchMetricsLimits.MaxChunks)
            throw Reject($"{chunks.Length} chunks over {MatchMetricsLimits.MaxChunks}");

        var result = new List<ParsedMatchChunk>(chunks.Length);
        var values = 0;

        foreach (var chunk in chunks)
        {
            var kind = KindOf(chunk.Slot, slots, declared);

            if (chunk.Values == null || chunk.Values.Length == 0)
                throw Reject($"A chunk of the slot {chunk.Slot} without values");

            values += chunk.Values.Length;

            if (values > MatchMetricsLimits.MaxValues)
                throw Reject($"Values over {MatchMetricsLimits.MaxValues}");

            if (chunk.Start < 0 || (long)chunk.Start + chunk.Values.Length > MatchMetricsLimits.MaxBucket)
                throw Reject($"The buckets {chunk.Start}+{chunk.Values.Length} out of 0..{MatchMetricsLimits.MaxBucket}");

            RequireAge(chunk.AgeMs);

            foreach (var value in chunk.Values)
            {
                if (float.IsInfinity(value) || float.IsNaN(value) && kind == SharedMatchMetrics.MatchSeriesKind.Counter)
                    throw Reject($"The value {value} in the slot {chunk.Slot} of {kind}");
            }

            result.Add(new ParsedMatchChunk(chunk.Slot, chunk.Start, chunk.AgeMs, chunk.Values));
        }

        RequireDisjoint(result);

        return result;
    }

    // A bucket of a series once a batch: two chunks with one start would break the key of the table, overlapping ones would
    // count their common buckets twice.
    private static void RequireDisjoint(List<ParsedMatchChunk> chunks)
    {
        var ordered = chunks.OrderBy(chunk => chunk.Slot).ThenBy(chunk => chunk.Start).ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            var previous = ordered[i - 1];
            var chunk = ordered[i];

            if (chunk.Slot == previous.Slot && chunk.Start < (long)previous.Start + previous.Values.Length)
                throw Reject($"The chunks of the slot {chunk.Slot} from {previous.Start} and {chunk.Start} overlap");
        }
    }

    private static SharedMatchMetrics.MatchSeriesKind KindOf(
        int slot,
        IReadOnlyDictionary<int, SharedMatchMetrics.MatchSeriesKind> slots,
        List<ParsedMatchSeries> declared)
    {
        if (slots.TryGetValue(slot, out var known))
            return known;

        foreach (var series in declared)
        {
            if (series.Slot == slot)
                return series.Kind;
        }

        throw Reject($"A chunk of the slot {slot} not declared");
    }

    private static List<ParsedMatchEvent> ParseEvents(SharedMatchMetrics.MatchEvent[] events)
    {
        if (events.Length > MatchMetricsLimits.MaxEvents)
            throw Reject($"{events.Length} events over {MatchMetricsLimits.MaxEvents}");

        var result = new List<ParsedMatchEvent>(events.Length);

        foreach (var entry in events)
        {
            if (SharedMatchMetricNames.IsName(entry.Name) == false)
                throw Reject("An event name breaks the rule of names");

            if (float.IsFinite(entry.Time) == false || entry.Time < 0f || entry.Time > MatchMetricsLimits.MaxBucket)
                throw Reject($"The time {entry.Time} of the event {entry.Name}");

            RequireAge(entry.AgeMs);
            result.Add(new ParsedMatchEvent(entry.Name, entry.Time, entry.AgeMs, ParseFields(entry.Fields)));
        }

        return result;
    }

    // The fields as a json object; the stream of the server builds its events with it too.
    internal static string ParseFields(SharedMatchMetrics.MatchEventField[]? fields)
    {
        fields ??= Array.Empty<SharedMatchMetrics.MatchEventField>();

        if (fields.Length > SharedMatchMetricsLimits.MaxFields)
            throw Reject($"{fields.Length} fields over {SharedMatchMetricsLimits.MaxFields}");

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var json = new StringBuilder("{");

        foreach (var field in fields)
        {
            if (SharedMatchMetricNames.IsKey(field.Key) == false || keys.Add(field.Key) == false)
                throw Reject("A field key breaks the rule of names or comes twice");

            if (json.Length > 1)
                json.Append(',');

            json.Append('"').Append(field.Key).Append("\":");

            if (field.Text != null)
            {
                if (SharedMatchMetricNames.IsValue(field.Text) == false)
                    throw Reject($"The text of the field '{field.Key}' breaks the rule of names");

                json.Append('"').Append(field.Text).Append('"');
                continue;
            }

            if (double.IsFinite(field.Number) == false)
                throw Reject($"The field '{field.Key}' is not a finite number");

            json.Append(field.Number.ToString("R", CultureInfo.InvariantCulture));
        }

        return json.Append('}').ToString();
    }

    private static void RequireAge(long ageMs)
    {
        if (ageMs is < 0 or > MatchMetricsLimits.MaxAgeMs)
            throw Reject($"The age {ageMs} ms out of 0..{MatchMetricsLimits.MaxAgeMs}");
    }

    private static MatchMetricsRejectedException Reject(string reason)
    {
        return new MatchMetricsRejectedException(reason);
    }
}
