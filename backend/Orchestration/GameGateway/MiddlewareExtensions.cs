using Common.Extensions;

namespace GameGateway;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder AddMiddleware(this IApplicationBuilder app)
    {
        app.UseAllowedOrigins();
        app.UseMiddleware<SessionConnectionMiddleware>();
        app.UseRouting();

        return app;
    }
}