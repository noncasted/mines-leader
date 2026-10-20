using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Common.Extensions;

/// <summary>
/// Browser origins the WebGL client is served from. Configured as a comma-separated list under
/// <see cref="ConfigurationKey"/> (env <c>Cors__AllowedOrigins</c>); in Development any loopback origin
/// (localhost, 127.0.0.1, any port) is allowed on top of the list. Outside Development an empty list fails startup.
/// </summary>
public sealed class AllowedOrigins
{
    public AllowedOrigins(IReadOnlyList<string> origins, bool allowLoopback)
    {
        Origins = origins.Select(Normalize).ToArray();
        AllowLoopback = allowLoopback;
    }

    public const string ConfigurationKey = "Cors:AllowedOrigins";

    public IReadOnlyList<string> Origins { get; }
    public bool AllowLoopback { get; }

    public static AllowedOrigins Read(IConfiguration configuration, IHostEnvironment environment)
    {
        var raw = configuration[ConfigurationKey] ?? "";
        var origins = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var isDevelopment = environment.IsDevelopment();

        if (origins.Length == 0 && isDevelopment == false)
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} is empty in {environment.EnvironmentName}: list the origins the client is served from " +
                "(e.g. https://minesleader.xyz,https://html-classic.itch.zone)");
        }

        return new AllowedOrigins(origins, isDevelopment);
    }

    public bool IsAllowed(string origin)
    {
        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri) == false)
            return false;

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            return false;

        if (AllowLoopback == true && uri.IsLoopback == true)
            return true;

        var normalized = uri.GetLeftPart(UriPartial.Authority);
        return Origins.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string origin)
    {
        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri) == false
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || uri.PathAndQuery != "/")
        {
            throw new InvalidOperationException($"{ConfigurationKey}: '{origin}' is not an origin (scheme://host[:port])");
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }
}

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

    /// <summary>
    /// CORS for HTTP requests plus the same origin check for WebSocket handshakes (CORS does not cover them).
    /// Adds <c>UseWebSockets</c>. A handshake without an Origin header (native client, Unity editor) is allowed:
    /// only browsers send it and only browsers are subject to cross-site hijacking.
    /// </summary>
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
