using System.Globalization;

namespace Console;

// The offset of the browser from UTC at the moment the page asked for it; UTC until the browser answers (prerender).
public readonly struct BrowserTimeZone
{
    public BrowserTimeZone(TimeSpan offset)
    {
        Offset = offset;
    }

    public TimeSpan Offset { get; }

    public static BrowserTimeZone Utc => new(TimeSpan.Zero);

    public DateTime ToLocal(DateTime utc)
    {
        return DateTime.SpecifyKind(utc, DateTimeKind.Unspecified) + Offset;
    }

    public string Name()
    {
        if (Offset == TimeSpan.Zero)
            return "UTC";

        var sign = Offset < TimeSpan.Zero ? "-" : "+";
        var offset = Offset.Duration();
        var minutes = offset.Minutes == 0 ? "" : $":{offset.Minutes.ToString("00", CultureInfo.InvariantCulture)}";

        return $"UTC{sign}{(int)offset.TotalHours}{minutes}";
    }
}
