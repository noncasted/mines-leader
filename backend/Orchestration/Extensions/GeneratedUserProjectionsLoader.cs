using Infrastructure;
using Meta.Users;

namespace Orchestration;

public class GeneratedUserProjectionsLoader : IUserProjectionsLoader
{
    public GeneratedUserProjectionsLoader(IOrleans orleans)
    {
        _orleans = orleans;
    }

    private readonly IOrleans _orleans;

    public Task<IReadOnlyList<IProjectionPayload>> Load(Guid userId)
    {
        return GeneratedUserProjections.GetAllUserProjections(_orleans, userId);
    }
}
