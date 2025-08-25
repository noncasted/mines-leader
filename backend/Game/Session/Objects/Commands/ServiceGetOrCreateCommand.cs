using Shared;

namespace Game;

public class ServiceGetOrCreateCommand : ResponseCommand<SharedSessionService.GetRequest, SharedSessionService.GetResponse>
{
    public ServiceGetOrCreateCommand(IServiceFactory serviceFactory)
    {
        _serviceFactory = serviceFactory;
    }

    private readonly IServiceFactory _serviceFactory;
    
    protected override SharedSessionService.GetResponse Execute( IUser user, SharedSessionService.GetRequest request)
    {
        var service = _serviceFactory.GetOrCreate(request);

        var overview = service.CreateOverview();

        return new SharedSessionService.GetResponse()
        {
            Key = service.Key,
            Id = service.Id,
            Properties = overview.Properties
        };
    }
}