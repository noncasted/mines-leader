using Shared;

namespace Game;

public class ServiceGetOrCreateCommand : ResponseCommand<ServiceContexts.GetRequest, ServiceContexts.GetResponse>
{
    public ServiceGetOrCreateCommand(IServiceFactory serviceFactory)
    {
        _serviceFactory = serviceFactory;
    }

    private readonly IServiceFactory _serviceFactory;
    
    protected override ServiceContexts.GetResponse Execute(
        CommandScope scope,
        ServiceContexts.GetRequest context)
    {
        var service = _serviceFactory.GetOrCreate(context);

        var overview = service.CreateOverview();

        return new ServiceContexts.GetResponse()
        {
            Key = service.Key,
            Properties = overview.Properties
        };
    }
}