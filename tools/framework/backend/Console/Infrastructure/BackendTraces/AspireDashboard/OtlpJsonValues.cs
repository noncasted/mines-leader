using System.Globalization;
using System.Text.Json;

namespace Console.Infrastructure.BackendTraces.AspireDashboard;

// The values of the OTLP JSON encoding: 64-bit numbers may come as strings, an attribute value is an object with one typed field,
// an enum may come as its number or its name.
public static class OtlpJsonValues
{
    private const int _statusError = 2;
    private const string _statusErrorName = "STATUS_CODE_ERROR";

    public static ulong UnixNano(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.String
            ? ulong.Parse(element.GetString()!, CultureInfo.InvariantCulture)
            : element.GetUInt64();
    }

    public static string? OptionalString(JsonElement owner, string name)
    {
        return owner.TryGetProperty(name, out var value) ? value.GetString() : null;
    }

    // stringValue, intValue, boolValue, doubleValue as they read; an array, a key list or bytes as their JSON; an empty value is "".
    public static string AttributeValue(JsonElement value)
    {
        foreach (var field in value.EnumerateObject())
        {
            return field.Name switch
            {
                "stringValue" => field.Value.GetString()!,
                "intValue" => field.Value.ValueKind == JsonValueKind.String ? field.Value.GetString()! : field.Value.GetRawText(),
                "boolValue" => field.Value.GetBoolean() ? "true" : "false",
                "doubleValue" => field.Value.GetDouble().ToString(CultureInfo.InvariantCulture),
                _ => field.Value.GetRawText()
            };
        }

        return string.Empty;
    }

    public static IReadOnlyList<KeyValuePair<string, string>> Attributes(JsonElement owner)
    {
        if (!owner.TryGetProperty("attributes", out var attributes))
            return [];

        return attributes.EnumerateArray()
            .Select(attribute => KeyValuePair.Create(attribute.GetProperty("key").GetString()!, AttributeValue(attribute.GetProperty("value"))))
            .ToArray();
    }

    public static BackendTraceError? Error(JsonElement span)
    {
        if (!span.TryGetProperty("status", out var status) || !status.TryGetProperty("code", out var code))
            return null;

        var isError = code.ValueKind == JsonValueKind.Number ? code.GetInt32() == _statusError : code.GetString() == _statusErrorName;

        return isError ? new BackendTraceError(OptionalString(status, "message") ?? string.Empty) : null;
    }
}
