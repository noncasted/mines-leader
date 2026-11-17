namespace Cluster.Monitoring.Online;

// Counts the online by the ids of the users: every change of the users online is applied to the day and the hour
// of its time, the peaks are the largest sets seen, the unique counts are the sizes of the id sets, so a user who
// leaves and comes back in the same hour or day is counted once. Holds the days of the statistics in memory and
// tells which of them changed since the last TakeChanged. Not thread-safe: the owner serializes the calls.
public class OnlineCounter
{
    public OnlineCounter(IEnumerable<OnlineDayState> restored)
    {
        foreach (var day in restored)
            _days.Add(day.Day, day);
    }

    public const int HoursKept = 48;
    public const int DaysKept = 30;

    private readonly SortedDictionary<int, OnlineDayState> _days = new();
    private readonly HashSet<int> _changed = new();

    private int _current;
    private int? _lastDay;

    public static int DayOf(DateTime utc)
    {
        return DateOnly.FromDateTime(utc).DayNumber;
    }

    public static int FirstKeptDay(DateTime utc)
    {
        return DayOf(utc) - (DaysKept - 1);
    }

    public void Apply(IReadOnlySet<Guid> online, DateTime utc)
    {
        var dayNumber = DayOf(utc);
        _current = online.Count;

        if (_lastDay != dayNumber)
        {
            // A new day (or the first apply after a restore): the open hours of the other days are over.
            foreach (var other in _days.Values)
            {
                if (other.Day != dayNumber && other.HasOpenHours == true)
                {
                    other.CloseHours();
                    _changed.Add(other.Day);
                }
            }

            _lastDay = dayNumber;
            Trim(utc);
        }

        if (_days.TryGetValue(dayNumber, out var day) == false)
        {
            day = OnlineDayState.Create(dayNumber);
            _days.Add(dayNumber, day);
        }

        day.Apply(utc.Hour, online);
        _changed.Add(dayNumber);
    }

    // Copies of the days changed since the last call, to be written while the counter goes on.
    public IReadOnlyList<OnlineDayState> TakeChanged()
    {
        var changed = new List<OnlineDayState>(_changed.Count);

        foreach (var dayNumber in _changed)
        {
            if (_days.TryGetValue(dayNumber, out var day) == true)
                changed.Add(day.Copy());
        }

        _changed.Clear();
        return changed;
    }

    // The days of a write that failed: written again with the next one.
    public void MarkChanged(IEnumerable<int> days)
    {
        _changed.UnionWith(days);
    }

    public OnlineStatsLiveData Summary(DateTime utc)
    {
        var hourStart = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
        var today = DayOf(utc);

        var hours = new List<OnlinePeriod>(HoursKept);

        for (var back = HoursKept - 1; back >= 0; back--)
        {
            var start = hourStart.AddHours(-back);
            var hour = _days.GetValueOrDefault(DayOf(start))?.FindHour(start.Hour);

            hours.Add(new OnlinePeriod
            {
                Start = start,
                Peak = hour?.Peak ?? 0,
                UniqueUsers = hour?.UniqueUsers ?? 0
            });
        }

        var days = new List<OnlinePeriod>(DaysKept);

        for (var back = DaysKept - 1; back >= 0; back--)
        {
            var dayNumber = today - back;
            var day = _days.GetValueOrDefault(dayNumber);

            days.Add(new OnlinePeriod
            {
                Start = DateOnly.FromDayNumber(dayNumber).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                Peak = day?.Peak ?? 0,
                UniqueUsers = day?.Users.Count ?? 0
            });
        }

        return new OnlineStatsLiveData
        {
            Current = _current,
            UpdatedAt = utc,
            Hours = hours,
            Days = days,
            UniqueLast7Days = UniqueSince(today - 6),
            UniqueLast30Days = UniqueSince(today - (DaysKept - 1))
        };
    }

    private int UniqueSince(int firstDay)
    {
        var users = new HashSet<Guid>();

        foreach (var day in _days.Values)
        {
            if (day.Day >= firstDay)
                users.UnionWith(day.Users);
        }

        return users.Count;
    }

    // The days out of the statistics leave the memory; their rows stay in the database.
    private void Trim(DateTime utc)
    {
        var first = FirstKeptDay(utc);

        foreach (var old in _days.Keys.Where(day => day < first).ToList())
        {
            if (_changed.Contains(old) == false)
                _days.Remove(old);
        }
    }
}
