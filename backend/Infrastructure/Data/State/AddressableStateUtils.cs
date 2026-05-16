using Microsoft.Extensions.Logging;

namespace Infrastructure;

public class AddressableStateUtils
{
    public AddressableStateUtils(IOrleans orleans, IMessaging messaging, ILoggerFactory loggerFactory)
    {
        Orleans = orleans;
        Messaging = messaging;
        LoggerFactory = loggerFactory;
    }

    public IOrleans Orleans { get; }
    public IMessaging Messaging { get; }
    public ILoggerFactory LoggerFactory { get; }
}