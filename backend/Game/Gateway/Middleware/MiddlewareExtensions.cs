using Microsoft.AspNetCore.Builder;

namespace Game.Gateway;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder AddMiddleware(this IApplicationBuilder app)
    {
        app.UseCors(x => x
            .AllowAnyMethod()
            .AllowAnyHeader()
            .SetIsOriginAllowed(_ => true)
            .AllowCredentials()); 
        
        app.UseWebSockets();
        app.UseMiddleware<ConnectionMiddleware>();
        app.UseRouting();

        return app;
    }
}