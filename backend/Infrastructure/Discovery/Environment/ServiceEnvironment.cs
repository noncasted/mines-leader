using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Discovery;

public interface IServiceEnvironment
{
    Guid ServiceId { get; }
    bool IsDevelopment { get; }
    string ServiceName { get; }
    string ServiceUrl { get; }
    ServiceTag Tag { get; }
}

public class ServiceEnvironment : IServiceEnvironment
{
    public Guid ServiceId { get; } = Guid.NewGuid();
    public required bool IsDevelopment { get; init; }
    public required string ServiceName { get; init; }
    public required string ServiceUrl { get; init; }
    public required ServiceTag Tag { get; init; }
}

public static class EnvironmentExtensions
{
    public static IHostApplicationBuilder AddEnvironment(this IHostApplicationBuilder builder, ServiceTag tag)
    {
        if (builder.Environment.IsDevelopment() == true)
        {
            builder.Services.AddSingleton<IServiceEnvironment>(new ServiceEnvironment
            {
                IsDevelopment = true,
                ServiceName = "dev",
                ServiceUrl = "http://localhost:5268",
                Tag = tag
            });
        }
        else
        {
            builder.Services.AddSingleton<IServiceEnvironment>(new ServiceEnvironment
            {
                IsDevelopment = false,
                ServiceName = Environment.GetEnvironmentVariable("SERVICE_NAME")!,
                ServiceUrl = Environment.GetEnvironmentVariable("SERVICE_PUBLIC_URL")!,
                Tag = tag
            });
        }

        return builder;
    }

    public static string ServerUrlToWebSocket(this IServiceEnvironment environment, string url)
    {
        if (environment.IsDevelopment == true)
            return url.Replace("http://", "ws://");

        return url.Replace("https://", "wss://");
    }
}