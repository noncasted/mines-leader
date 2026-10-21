using Cluster.Discovery;

namespace Cluster.Discovery;

// The service roles of this game. The coordinator role belongs to the cluster (ServiceTag.Coordinator).
public static class GameRoles
{
    public static readonly ServiceTag Meta = new("Meta");
    public static readonly ServiceTag Game = new("Game");
    public static readonly ServiceTag Silo = new("Silo");
    public static readonly ServiceTag Console = new("Console");

    // Roles that must be present in the service discovery before a participant finishes its startup,
    // in the order the console lists them.
    public static readonly IReadOnlyList<ServiceTag> Required = [ServiceTag.Coordinator, Meta, Game, Silo, Console];
}
