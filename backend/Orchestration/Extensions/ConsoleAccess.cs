using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Orchestration;

/// <summary>
/// The admin token (<c>CONSOLE_TOKEN</c>): Console login and the internal HTTP endpoints of Meta / Game.
/// Empty or <see cref="DevelopmentToken"/> turns the check off - allowed only in Development;
/// any other environment fails startup with such a value.
/// </summary>
public sealed class ConsoleAccess
{
    private ConsoleAccess(byte[]? token)
    {
        _token = token;
    }

    public const string TokenKey = "CONSOLE_TOKEN";
    public const string DevelopmentToken = "local-dev-token";

    private readonly byte[]? _token;

    public bool IsEnabled => _token != null;

    public static ConsoleAccess Read(IConfiguration configuration, IHostEnvironment environment)
    {
        var token = configuration[TokenKey] ?? "";

        if (token.Length > 0 && token != DevelopmentToken)
            return new ConsoleAccess(Encoding.UTF8.GetBytes(token));

        if (environment.IsDevelopment() == true)
            return new ConsoleAccess(null);

        var value = token.Length == 0 ? "empty" : $"the development value '{DevelopmentToken}'";

        throw new InvalidOperationException(
            $"{TokenKey} is {value} in {environment.EnvironmentName}: authentication can be off only in Development");
    }

    public bool IsValid(string? token)
    {
        if (_token == null)
            throw new InvalidOperationException("Console access is disabled: there is no token to compare with");

        if (string.IsNullOrEmpty(token) == true)
            return false;

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), _token);
    }

    /// <summary>
    /// True when the check is off or the request carries <c>Authorization: Bearer &lt;CONSOLE_TOKEN&gt;</c>.
    /// </summary>
    public bool IsAuthorized(HttpRequest request)
    {
        if (IsEnabled == false)
            return true;

        const string prefix = "Bearer ";
        var header = request.Headers.Authorization.ToString();

        if (header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == false)
            return false;

        return IsValid(header[prefix.Length..].Trim());
    }
}

public static class ConsoleAccessExtensions
{
    public static ConsoleAccess AddConsoleAccess(this IHostApplicationBuilder builder)
    {
        var access = ConsoleAccess.Read(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(access);
        return access;
    }

    /// <summary>
    /// Internal endpoints (user / session ids): 401 without the console token, see <see cref="ConsoleAccess.IsAuthorized"/>.
    /// </summary>
    public static TBuilder RequireConsoleToken<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) => {
            var access = context.HttpContext.RequestServices.GetRequiredService<ConsoleAccess>();

            if (access.IsAuthorized(context.HttpContext.Request) == false)
                return Results.Unauthorized();

            return await next(context);
        });

        return builder;
    }
}
