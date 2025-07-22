using Backend.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Shared;

namespace Backend.Gateway;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder AddIdentityEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost(SharedBackendUserAuth.Endpoint, DevelopSignUp);

        return builder;
    }

    private static async Task<SharedBackendUserAuth.Response> DevelopSignUp(
        [FromBody] SharedBackendUserAuth.Request request,
        [FromServices] IUserFactory factory,
        [FromServices] ILogger<IUserFactory> logger)
    {
        var options = new UserCreateOptions
        {
            Name = request.Name
        };
        
        logger.LogInformation("[User] Develop sign up with name {Name}", request.Name);

        var id = await factory.Create(options);
 
        logger.LogInformation("[User] Develop sign up is completed with id {Id}", id);
        
        return new SharedBackendUserAuth.Response
        {
            Id = id
        };
    }
}