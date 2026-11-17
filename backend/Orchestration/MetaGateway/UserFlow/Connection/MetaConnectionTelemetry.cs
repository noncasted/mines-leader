using System.Diagnostics;

namespace MetaGateway.UserFlow.Connection;

public static class MetaConnectionTelemetry
{
    public static readonly ActivitySource Source = new("MetaGateway.Connection");
}
