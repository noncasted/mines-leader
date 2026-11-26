using Cluster.Discovery;

namespace Framework.Tests.Fixtures;

// Roles of a cluster as a project declares them (the framework knows only ServiceTag.Coordinator).
public static class TestRoles
{
    public static readonly ServiceTag Meta = new("Meta");
    public static readonly ServiceTag Game = new("Game");
    public static readonly ServiceTag Silo = new("Silo");
}
