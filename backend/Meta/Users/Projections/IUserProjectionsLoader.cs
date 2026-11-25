namespace Meta.Users;

/// <summary>
/// Reads all projections of the user in the current transaction. The list of sources is built by a generator
/// that is not connected to Meta, so the implementation is registered from outside.
/// </summary>
public interface IUserProjectionsLoader
{
    Task<IReadOnlyList<IProjectionPayload>> Load(Guid userId);
}
