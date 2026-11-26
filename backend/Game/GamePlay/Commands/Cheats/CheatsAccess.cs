using Cluster.State;
using Microsoft.Extensions.Hosting;

namespace Game.GamePlay;

public class CheatsAccess : ICheatsAccess
{
    public CheatsAccess(IHostEnvironment environment, IGameFlags flags)
    {
        _environmentName = environment.EnvironmentName;
        _isDevelopment = environment.IsDevelopment();
        _flags = flags;
    }

    private readonly string _environmentName;
    private readonly bool _isDevelopment;
    private readonly IGameFlags _flags;

    public bool IsAllowed => _isDevelopment == true && _flags.CheatsEnabled == true;

    public string RefusalReason
    {
        get
        {
            if (_isDevelopment == false)
                return $"host environment is {_environmentName}, not Development";

            if (_flags.CheatsEnabled == false)
                return $"game flag {GameFlagNames.Cheats} is off";

            throw new InvalidOperationException("Cheats are allowed: there is no refusal reason");
        }
    }
}
