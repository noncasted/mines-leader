using MudBlazor.Services;

namespace Management.Web;

public static class WebSetup
{
    public static IHostApplicationBuilder SetupMudBlazor(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        // Project services
        services
            .AddMudServices()
            .AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.AddCommonConsoleComponents();
        
        return builder;
    }
}