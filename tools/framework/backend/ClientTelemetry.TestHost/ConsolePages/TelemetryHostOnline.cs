using Cluster.Deploy;
using Cluster.Monitoring.Online;
using Internal;

namespace Framework.ClientTelemetry.TestHost.ConsolePages;

// A stand-in for the online statistics of a coordinator: this host tracks no users, the online page and widget show a fixed sample.
public class TelemetryHostOnline : SharedViewableProperty<OnlineStatsLiveData>, IDeploymentState<OnlineStatsLiveData>
{
    public TelemetryHostOnline() : base(Sample(DateTime.UtcNow))
    {
    }

    public bool IsInitialized => true;

    public Task SetValue(OnlineStatsLiveData value)
    {
        throw new InvalidOperationException("The online statistics of the test host are fixed");
    }

    private static OnlineStatsLiveData Sample(DateTime now)
    {
        var hour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var hours = Enumerable.Range(0, 48)
            .Select(index => Period(hour.AddHours(index - 47), 4 + (int)(6 * (1 + Math.Sin(index / 3.8))), 2))
            .ToList();
        var days = Enumerable.Range(0, 30)
            .Select(index => Period(hour.Date.AddDays(index - 29), 8 + index % 7 * 3, 5))
            .ToList();

        return new OnlineStatsLiveData
        {
            Current = hours[^1].Peak - 1,
            UpdatedAt = now,
            Hours = hours,
            Days = days,
            UniqueLast7Days = 120,
            UniqueLast30Days = 310
        };
    }

    private static OnlinePeriod Period(DateTime start, int peak, int uniqueFactor)
    {
        return new OnlinePeriod { Start = start, Peak = peak, UniqueUsers = peak * uniqueFactor };
    }
}
