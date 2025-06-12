using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Backend.Users;

public static class UserServicesExtensions
{
    public static IHostApplicationBuilder AddUserFactory(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IUserFactory, UserFactory>();
        return builder;
    }
}