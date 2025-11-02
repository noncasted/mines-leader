using Common.Extensions;
using Infrastructure;
using Meta.Users;
using Microsoft.AspNetCore.Mvc;
using Shared;

namespace MetaGateway;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder AddIdentityEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost(SharedBackendUserSignUp.Endpoint, DevelopSignUp);
        builder.MapPost(SharedBackendUserLogin.Endpoint, LogIn);

        return builder;
    }

    private static async Task<SharedBackendUserSignUp.Response> DevelopSignUp(
        [FromBody] SharedBackendUserSignUp.Request request,
        [FromServices] IUserFactory factory,
        [FromServices] IOrleans orleans,
        [FromServices] ILogger<IUserFactory> logger)
    {
        using var activity = TraceExtensions.PlayerEndpoints.Start("DevelopSignUp");

        var options = new UserCreateOptions();
        logger.LogInformation("[User] Develop sign up");

        var id = await factory.Create(options);

        var userName = $"User_{id.ToString().Substring(0, 8)}";
        var user = orleans.CreateUserHandle(id);

        await orleans.InTransaction(() => user.Entity.SetName(userName));

        logger.LogInformation("[User] Develop sign up is completed with id {Id}", id);

        activity.Stop();

        return new SharedBackendUserSignUp.Response
        {
            Id = id
        };
    }

    private static Task<SharedBackendUserLogin.Response> LogIn(
        [FromBody] SharedBackendUserLogin.Request request,
        [FromServices] IUserFactory factory,
        [FromServices] ILogger<IUserFactory> logger)
    {
        using var activity = TraceExtensions.PlayerEndpoints.Start("LogIn");

        logger.LogInformation("[User] Develop login with id {Id}", request.Id);

        activity.Stop();

        return Task.FromResult(new SharedBackendUserLogin.Response());
    }
}