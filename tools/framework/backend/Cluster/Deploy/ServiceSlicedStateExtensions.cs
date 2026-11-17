using Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cluster.Deploy;

public static class ServiceSlicedStateExtensions
{
    public static IHostApplicationBuilder AddServiceSlicedState<TData, TSlice>(this IHostApplicationBuilder builder)
        where TData : class, IServiceSlices<TSlice>, new()
        where TSlice : class
    {
        builder.Services.AddSingleton(new TData());

        builder.Add<ServiceSlicedState<TData, TSlice>>()
               .As<IServiceSlicedState<TData, TSlice>>()
               .As<IDeploymentState<TData>>()
               .As<IDeployAware>();

        return builder;
    }
}
