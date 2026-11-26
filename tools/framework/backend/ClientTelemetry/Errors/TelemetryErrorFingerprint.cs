using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Game.ClientTelemetry;

// The kind of an error, over the text the Scrubber has left: the parser has already turned line numbers, addresses and other
// numbers into <n> and <id>, here they go, so another line, build address or index gives the same fingerprint. A hash and not
// string.GetHashCode: the value is stored and compared between processes. The stack may be empty: then the message decides. The
// category of the logger is a part when there is one; without it the text is the one of before it, so stored groups still match.
public static class TelemetryErrorFingerprint
{
    private const RegexOptions _options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;
    private const int _hexLength = 16;

    private static readonly Regex _variablePart = new("<(?:n|id)>", _options);
    private static readonly Regex _space = new(@"\s+", _options);

    public static string Of(TelemetryErrorLevel level, string message, string stack, string? category)
    {
        // A line break cannot stay inside a normalized part nor a category, so it divides them without doubt.
        var text = $"{level}\n{Normalize(message)}\n{Normalize(stack)}";

        if (category != null)
            text += $"\n{category}";

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[.._hexLength];
    }

    private static string Normalize(string text)
    {
        return _space.Replace(_variablePart.Replace(text, ""), " ").Trim();
    }
}
