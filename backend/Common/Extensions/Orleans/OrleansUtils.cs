using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Common;

public interface IOrleans
{
    IGrainFactory Grains { get; }
    ITransactions Transactions { get; }
    ILogger Logger { get; }
}

public class OrleansUtils : IOrleans
{
    public OrleansUtils(
        IGrainFactory grains,
        ITransactions transactions, 
        ILogger<OrleansUtils> logger)
    {
        Grains = grains;
        Transactions = transactions;
        Logger = logger;
    }

    public IGrainFactory Grains { get; }
    public ITransactions Transactions { get; }
    public ILogger Logger { get; }
}

public static class OrleansUtilsExtensions
{
    public static IHostApplicationBuilder AddOrleansUtils(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ITransactions, Transactions>();
        builder.Services.AddSingleton<IOrleans, OrleansUtils>();
        
        return builder;
    }
}