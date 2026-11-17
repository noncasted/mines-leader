using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Cluster.Monitoring.Online;

// The online of one UTC day, keyed by DateOnly.DayNumber. Not deploy-scoped: the history outlives restarts and
// deploys. Users holds the ids of everyone who came this day: the unique counts of a day and of several days.
[GenerateSerializer]
[GrainState(Table = "state_online_days", State = "online_day", Lookup = "OnlineDay", Key = GrainKeyType.Integer)]
public class OnlineDayState : IDirectStateValue
{
    [Id(0)] [JsonProperty] public int Day { get; private set; }
    [Id(1)] [JsonProperty] public int Peak { get; private set; }
    [Id(2)] [JsonProperty] public HashSet<Guid> Users { get; private set; } = new();
    [Id(3)] [JsonProperty] public List<OnlineHourState> Hours { get; private set; } = new();

    public int Version => 0;

    public static OnlineDayState Create(int day)
    {
        return new OnlineDayState { Day = day };
    }

    public bool HasOpenHours => Hours.Any(hour => hour.IsClosed == false);

    // Applies the users online now to the hour; every other open hour of the day is over.
    public void Apply(int hour, IReadOnlySet<Guid> online)
    {
        var current = Hours.Find(entry => entry.Hour == hour);

        if (current == null)
        {
            current = OnlineHourState.Open(hour);
            Hours.Add(current);
        }

        foreach (var other in Hours)
        {
            if (other != current && other.IsClosed == false)
                other.Close();
        }

        current.Apply(online);
        Peak = Math.Max(Peak, online.Count);
        Users.UnionWith(online);
    }

    public void CloseHours()
    {
        foreach (var hour in Hours)
        {
            if (hour.IsClosed == false)
                hour.Close();
        }
    }

    public OnlineHourState? FindHour(int hour)
    {
        return Hours.Find(entry => entry.Hour == hour);
    }

    // The same day written by another coordinator (two of them overlap during a deploy): nothing either has counted
    // is lost.
    public OnlineDayState MergedWith(OnlineDayState other)
    {
        if (other.Day != Day)
            throw new InvalidOperationException($"Online day {Day} merged with day {other.Day}");

        var hours = Hours.Concat(other.Hours)
                         .GroupBy(hour => hour.Hour)
                         .Select(group => group.Aggregate((merged, hour) => merged.MergedWith(hour)))
                         .OrderBy(hour => hour.Hour)
                         .ToList();

        return new OnlineDayState
        {
            Day = Day,
            Peak = Math.Max(Peak, other.Peak),
            Users = new HashSet<Guid>(Users.Concat(other.Users)),
            Hours = hours
        };
    }

    public OnlineDayState Copy()
    {
        return new OnlineDayState
        {
            Day = Day,
            Peak = Peak,
            Users = new HashSet<Guid>(Users),
            Hours = Hours.Select(hour => hour.Copy()).ToList()
        };
    }
}
