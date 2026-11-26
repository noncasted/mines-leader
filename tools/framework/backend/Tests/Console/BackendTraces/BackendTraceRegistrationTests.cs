using Console;
using Console.Infrastructure.BackendTraces.AspireDashboard;
using Console.Infrastructure.BackendTraces.VictoriaTraces;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Framework.Tests.Console.BackendTraces;

public class BackendTraceRegistrationTests
{
    // A game picks one source by the extension it calls; a second call is a mistake of its composition.
    [Fact]
    public void ASecondSource_Throws()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.AddConsoleVictoriaTraces(new VictoriaTracesOptions { BaseUrl = new Uri("http://victoriatraces:10428") });

        Assert.Throws<InvalidOperationException>(() =>
            builder.AddConsoleAspireDashboardTraces(new AspireDashboardTraceOptions { BaseUrl = new Uri("http://localhost:21316"), ApiKey = "key" }));
    }
}
