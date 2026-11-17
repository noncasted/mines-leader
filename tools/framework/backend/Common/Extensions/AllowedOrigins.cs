using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Common.Extensions;

// Browser origins the WebGL client is served from. Configured as a comma-separated list under
// ConfigurationKey (env Cors__AllowedOrigins); in Development any loopback origin
// (localhost, 127.0.0.1, any port) is allowed on top of the list. Outside Development an empty list fails startup;
// a project without browser clients says so explicitly with NoBrowserOrigins as the whole value.
// An entry that is not a bare origin (path, query, fragment, user info) fails startup.
public sealed class AllowedOrigins
{
    public AllowedOrigins(IReadOnlyList<string> origins, bool allowLoopback)
    {
        Origins = origins.Select(Normalize).ToArray();
        AllowLoopback = allowLoopback;
    }

    public const string ConfigurationKey = "Cors:AllowedOrigins";

    // The whole value of ConfigurationKey for a project whose clients are native only (they send no Origin): no
    // browser origin is allowed, Development still adds loopback. It is not an origin, so a list cannot contain it
    // by mistake, and an empty value still fails startup outside Development.
    public const string NoBrowserOrigins = "none";

    public IReadOnlyList<string> Origins { get; }
    public bool AllowLoopback { get; }

    public static AllowedOrigins Read(IConfiguration configuration, IHostEnvironment environment)
    {
        var raw = configuration[ConfigurationKey] ?? "";
        var origins = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var isDevelopment = environment.IsDevelopment();

        if (origins.Contains(NoBrowserOrigins) == true)
        {
            if (origins.Length > 1)
            {
                throw new InvalidOperationException(
                    $"{ConfigurationKey}: '{NoBrowserOrigins}' is the whole value, " +
                    $"it cannot be combined with origins ('{raw}')");
            }

            return new AllowedOrigins([], isDevelopment);
        }

        if (origins.Length == 0 && isDevelopment == false)
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} is empty in {environment.EnvironmentName}: list the origins the client is served from " +
                $"(e.g. https://game.example,https://html-classic.itch.zone) or set '{NoBrowserOrigins}' " +
                "when the clients are native only");
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
            || uri.PathAndQuery != "/"
            || uri.Fragment.Length > 0
            || uri.UserInfo.Length > 0)
        {
            throw new InvalidOperationException($"{ConfigurationKey}: '{origin}' is not an origin (scheme://host[:port])");
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }
}
