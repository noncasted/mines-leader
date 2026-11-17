using System.Globalization;
using Cluster.Monitoring.Online;

namespace Console.Infrastructure.Online;

// Times of the online statistics as the console shows them: the hours and the update time in the time zone of the
// browser, the days as they are kept, UTC days (a day of the statistics is a UTC day of the coordinator).
public static class OnlinePeriodFormat
{
    public static string Label(OnlinePeriod period, OnlineScale scale, BrowserTimeZone zone)
    {
        return scale switch
        {
            OnlineScale.Hours => zone.ToLocal(period.Start).ToString("HH:mm", CultureInfo.InvariantCulture),
            OnlineScale.Days => period.Start.ToString("dd.MM", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(scale), scale, null)
        };
    }

    public static string Day(OnlinePeriod period)
    {
        return period.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static string Updated(DateTime time, BrowserTimeZone zone)
    {
        return $"{zone.ToLocal(time).ToString("HH:mm:ss", CultureInfo.InvariantCulture)} {zone.Name()}";
    }

    // The last periods of the scale: the oldest first, the current one last.
    public static IReadOnlyList<OnlinePeriod> Last(OnlineStatsLiveData data, OnlineScale scale, int count)
    {
        var periods = scale switch
        {
            OnlineScale.Hours => data.Hours,
            OnlineScale.Days => data.Days,
            _ => throw new ArgumentOutOfRangeException(nameof(scale), scale, null)
        };

        return periods.TakeLast(count).ToArray();
    }
}
