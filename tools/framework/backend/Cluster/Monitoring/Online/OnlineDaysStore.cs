using Infrastructure;
using Infrastructure.State;

namespace Cluster.Monitoring.Online;

public interface IOnlineDaysStore
{
    // The stored days from the first to the last day number, both included; a day without a row is absent.
    Task<IReadOnlyList<OnlineDayState>> Read(int firstDay, int lastDay);

    Task Write(IReadOnlyList<OnlineDayState> days);
}

// The rows of OnlineDayState, one per UTC day, keyed by its day number.
public class OnlineDaysStore : IOnlineDaysStore
{
    public OnlineDaysStore(IOrleans orleans)
    {
        _storage = orleans.StateStorage;
    }

    private readonly IStateStorage _storage;

    public async Task<IReadOnlyList<OnlineDayState>> Read(int firstDay, int lastDay)
    {
        var identities = Enumerable.Range(firstDay, lastDay - firstDay + 1).Select(Identity).ToList();
        var stored = await _storage.ReadBatch<long, OnlineDayState>(identities);
        return stored.Values.OrderBy(day => day.Day).ToList();
    }

    // Merged into the stored rows: the coordinators of two deploys overlap until the old one leaves its deploy, and
    // a whole-row write of one would drop what the other has counted. Not atomic: two writes of the same day in the
    // same moment may still lose one of them, the next flush of the loser writes its counter again.
    public async Task Write(IReadOnlyList<OnlineDayState> days)
    {
        if (days.Count == 0)
            return;

        var stored = await _storage.ReadBatch<long, OnlineDayState>(days.Select(day => Identity(day.Day)).ToList());
        var storedByDay = stored.Values.ToDictionary(day => day.Day);

        await _storage.Write(new StateWriteRequest
        {
            Records = days.ToDictionary(
                day => Identity(day.Day),
                IStateValue (day) => storedByDay.TryGetValue(day.Day, out var row) ? day.MergedWith(row) : day)
        });
    }

    private StateIdentity Identity(int day)
    {
        var info = _storage.Registry.Get<OnlineDayState>();

        return new StateIdentity
        {
            Key = (long)day,
            Type = info.Name,
            TableName = info.TableName,
            Extension = null
        };
    }
}
