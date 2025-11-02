namespace GameGateway;

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
        app.UseMiddleware<SessionConnectionMiddleware>();
        app.UseRouting();

        return app;
    }
}