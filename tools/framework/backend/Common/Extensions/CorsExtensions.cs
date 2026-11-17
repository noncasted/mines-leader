using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Common.Extensions;

public static class CorsExtensions
{
    public const string PolicyName = "cors";

    public static void ConfigureCors(this IHostApplicationBuilder builder)
    {
        var origins = AllowedOrigins.Read(builder.Configuration, builder.Environment);

        builder.Services.AddSingleton(origins);

        builder.Services.AddCors(options => {
            options.AddPolicy(PolicyName, policy => {
                policy
                    .SetIsOriginAllowed(origins.IsAllowed)
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });
    }

    // CORS for HTTP requests plus the same origin check for WebSocket handshakes (CORS does not cover them).
    // Adds UseWebSockets. A handshake without an Origin header (native client, Unity editor) is allowed:
    // only browsers send it and only browsers are subject to cross-site hijacking.
    public static IApplicationBuilder UseAllowedOrigins(this IApplicationBuilder app)
    {
        var origins = app.ApplicationServices.GetRequiredService<AllowedOrigins>();

        app.UseCors(PolicyName);
        app.UseWebSockets();

        app.Use(async (context, next) => {
            if (context.WebSockets.IsWebSocketRequest == true)
            {
                var origin = context.Request.Headers.Origin.ToString();

                if (origin.Length > 0 && origins.IsAllowed(origin) == false)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
            }

            await next(context);
        });

        return app;
    }
}
