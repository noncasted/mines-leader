using Infrastructure;

namespace Meta.Users;

public class UserProjectionChannelId : IRuntimeChannelId
{
    public UserProjectionChannelId(Guid id)
    {
        _id = id;
    }

    private readonly Guid _id;

    public string ToRaw()
    {
        return $"user-projection-{_id}";
    }
}