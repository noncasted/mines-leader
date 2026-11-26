using Cluster.Discovery;

namespace Console;

// How the console shows a service role: icon and position. Roles are open strings, so a role without its own
// icon gets the generic one and roles outside the required set go after it, by name.
public static class ServiceTagView
{
    public static string Icon(this ServiceRoleIcons icons, ServiceTag tag)
    {
        return icons.Icons.GetValueOrDefault(tag, "circle");
    }

    public static IEnumerable<IServiceOverview> OrderByRole(
        this IEnumerable<IServiceOverview> services,
        IReadOnlyList<ServiceTag> roleOrder)
    {
        return services.OrderBy(s => RoleIndex(roleOrder, s.Tag))
                       .ThenBy(s => s.Tag.Value, StringComparer.Ordinal)
                       .ThenBy(s => s.Id);
    }

    private static int RoleIndex(IReadOnlyList<ServiceTag> roleOrder, ServiceTag tag)
    {
        for (var i = 0; i < roleOrder.Count; i++)
        {
            if (roleOrder[i] == tag)
                return i;
        }

        return int.MaxValue;
    }
}
