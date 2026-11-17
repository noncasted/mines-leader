using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Game.ClientTelemetry;

// The kind of an error, over the text the Scrubber has left: the parser has already turned line numbers, addresses and other
// numbers into <n> and <id>, here they go, so another line, build address or index gives the same fingerprint. A hash and not
// string.GetHashCode: the value is stored and compared between processes. The stack may be empty: then the message decides.
public static class TelemetryErrorFingerprint
{
    private const RegexOptions _options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;
    private const int _hexLength = 16;

    private static readonly Regex _variablePart = new("<(?:n|id)>", _options);
    private static readonly Regex _space = new(@"\s+", _options);

    public static string Of(TelemetryErrorLevel level, string message, string stack)
    {
        // A line break cannot stay inside a normalized part, so it divides them without doubt.
        var text = $"{level}\n{Normalize(message)}\n{Normalize(stack)}";

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[.._hexLength];
    }

    private static string Normalize(string text)
    {
        return _space.Replace(_variablePart.Replace(text, ""), " ").Trim();
    }
}
