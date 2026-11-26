using Cluster.Discovery;
using Microsoft.AspNetCore.Builder;

namespace Cluster.Monolith;

// One role of a monolith: the same factory the separate service of the role calls from its Program.cs. The runner
// fills the options (the content root of the role, its port and names on the command line); the application name is
// the one of the role (its static web assets, its trace source), not the one of the monolith executable.
public record MonolithHost
{
    public required ServiceTag Tag { get; init; }
    public required string ApplicationName { get; init; }
    public required Func<WebApplicationOptions, WebApplication> Build { get; init; }
}
