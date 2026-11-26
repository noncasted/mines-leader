using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;

namespace Common.Extensions;

public static class TraceExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        // A feature registers its source together with its services: the tracer provider of AddOpenTelemetry picks
        // it up whenever that is called, no list of the sources to keep in sync. The registration is kept for the
        // tracer provider of a monolith process too (TraceSourceRegistration).
        public IHostApplicationBuilder AddTraceSource(ActivitySource source)
        {
            return builder.AddTraceSource(source.Name);
        }

        public IHostApplicationBuilder AddTraceSource(string sourceName)
        {
            builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(sourceName));
            builder.Services.AddSingleton(new TraceSourceRegistration(sourceName));
            return builder;
        }
    }
}
