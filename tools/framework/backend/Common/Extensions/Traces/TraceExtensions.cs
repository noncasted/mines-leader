using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;

namespace Common.Extensions;

public static class TraceExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        // A feature registers its source together with its services: the tracer provider of AddOpenTelemetry picks
        // it up whenever that is called, no list of the sources to keep in sync.
        public IHostApplicationBuilder AddTraceSource(ActivitySource source)
        {
            builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(source.Name));
            return builder;
        }
    }
}
