using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Meta.Users;

public static class UserServicesExtensions
{
    public static IHostApplicationBuilder AddUserServices(this IHostApplicationBuilder builder)
    {
        builder.Add<UserFactory>()
               .As<IUserFactory>();

        builder.AddStateCollection<UserCollection, Guid, UserState>()
               .As<IUserCollection>();

        return builder;
    }
}