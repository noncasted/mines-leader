using System.Security.Cryptography;
using System.Text;
using Common.Network;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Orchestration;

/// <summary>
/// S4: the key of the game session tickets (<c>SESSION_TICKET_SECRET</c>, base64 of at least 32 bytes), the same
/// value for the meta (issues) and the game gateways (verify). Empty -> a fixed development key, allowed only in
/// Development (as <see cref="ConsoleAccess"/>); empty, too short, not base64 or the development key in any other
/// environment fails startup.
/// </summary>
public static class SessionTicketsSetup
{
    public const string SecretKey = "SESSION_TICKET_SECRET";

    // Public on purpose: it only has to be equal on the local meta and game, and it is refused outside Development.
    public static readonly byte[] DevelopmentKey =
        SHA256.HashData(Encoding.UTF8.GetBytes("mines-leader development session ticket key"));

    public static SessionTicketKey ReadKey(IConfiguration configuration, IHostEnvironment environment)
    {
        var secret = configuration[SecretKey] ?? "";

        if (secret.Length == 0)
        {
            if (environment.IsDevelopment() == true)
                return new SessionTicketKey(DevelopmentKey);

            throw new InvalidOperationException(
                $"{SecretKey} is empty in {environment.EnvironmentName}: the development key is allowed only in Development");
        }

        byte[] key;

        try
        {
            key = Convert.FromBase64String(secret);
        }
        catch (FormatException e)
        {
            throw new InvalidOperationException($"{SecretKey} is not base64", e);
        }

        if (key.Length < SessionTicketKey.MinLength)
        {
            throw new InvalidOperationException(
                $"{SecretKey} is {key.Length} bytes, at least {SessionTicketKey.MinLength} are required");
        }

        if (environment.IsDevelopment() == false && CryptographicOperations.FixedTimeEquals(key, DevelopmentKey) == true)
        {
            throw new InvalidOperationException(
                $"{SecretKey} is the development key in {environment.EnvironmentName}");
        }

        return new SessionTicketKey(key);
    }

    public static IHostApplicationBuilder AddSessionTickets(this IHostApplicationBuilder builder)
    {
        var key = ReadKey(builder.Configuration, builder.Environment);

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(key);
        builder.Services.AddSingleton<SessionTickets>();
        builder.Services.AddSingleton<ISessionTicketIssuer>(sp => sp.GetRequiredService<SessionTickets>());
        builder.Services.AddSingleton<ISessionTicketVerifier>(sp => sp.GetRequiredService<SessionTickets>());

        return builder;
    }
}
