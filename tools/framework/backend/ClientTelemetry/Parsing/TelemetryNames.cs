namespace Game.ClientTelemetry;

// A metric, a mark and a unit are named in code: the closed alphabet leaves no room for a free text.
public static class TelemetryNames
{
    public static bool IsValidName(string name)
    {
        return IsValid(name, ClientTelemetryLimits.MaxNameLength);
    }

    public static bool IsValidUnit(string unit)
    {
        return IsValid(unit, ClientTelemetryLimits.MaxUnitLength);
    }

    // The last segment of an address: words of [A-Za-z0-9_-] between dots, no path, query, host or address.
    public static bool IsValidFile(string file)
    {
        if (file.Length == 0 || file.Length > ClientTelemetryLimits.MaxStartupFileLength || file[0] == '.' || file[^1] == '.' || file.Contains(".."))
            return false;

        foreach (var symbol in file)
        {
            var allowed = symbol is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-';
            if (!allowed)
                return false;
        }

        return true;
    }

    private static bool IsValid(string text, int maxLength)
    {
        if (text.Length == 0 || text.Length > maxLength)
            return false;

        foreach (var symbol in text)
        {
            var allowed = symbol is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_';
            if (!allowed)
                return false;
        }

        return true;
    }
}
