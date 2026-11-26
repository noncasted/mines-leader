using Cluster.Discovery;
using Microsoft.Extensions.Configuration;

namespace Cluster.Monolith;

// Monolith:Hosts:<role> of the configuration of the process: the port of the host (required, no shared
// ASPNETCORE_URLS) and its content root (default hosts/<role> next to the binaries, where the publish of the
// monolith puts the appsettings and configs of the role; a relative path is taken from the binaries too).
public record MonolithHostOptions
{
    public const string SectionName = "Monolith:Hosts";

    public required int Port { get; init; }
    public required string ContentRoot { get; init; }

    public static MonolithHostOptions Read(IConfiguration configuration, ServiceTag tag)
    {
        var section = configuration.GetSection($"{SectionName}:{tag.Value}");

        var port = section.GetValue<int?>("Port")
                   ?? throw new InvalidOperationException($"{SectionName}:{tag.Value}:Port is not configured");

        var contentRoot = Path.GetFullPath(section["ContentRoot"] ?? Path.Combine("hosts", tag.Value),
            AppContext.BaseDirectory);

        if (Directory.Exists(contentRoot) == false)
            throw new DirectoryNotFoundException($"The content root of the host {tag.Value} is missing: {contentRoot}");

        return new MonolithHostOptions
        {
            Port = port,
            ContentRoot = contentRoot
        };
    }
}
