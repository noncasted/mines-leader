using Infrastructure;

namespace Meta.Users;

public static class UserProjectionExtensions
{
    extension(IOrleans orleans)
    {
        public Task SendOneTimeProjection(Guid id, IProjectionPayload payload)
        {
            var projection = orleans.GetGrain<IUserProjection>(id);

            if (TransactionContextProvider.Current == null)
                return orleans.InTransaction(() => projection.SendOneTime(payload));

            return projection.SendOneTime(payload);
        }
    }
}