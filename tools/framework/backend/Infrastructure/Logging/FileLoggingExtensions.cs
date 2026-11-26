using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Logging;

public static class FileLoggingExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        // Dev-only diagnostics (backend/.telemetry/logs of the repository, {serviceName}.log): production containers
        // log through OpenTelemetry only. Options: FileLoggerOptions in the "FileLogging" configuration section.
        public IHostApplicationBuilder AddFileLogging(string serviceName)
        {
            if (builder.Environment.IsDevelopment() == false)
                return builder;

            var options = builder.Configuration.GetSection(FileLoggerOptions.SectionName).Get<FileLoggerOptions>()
                          ?? new FileLoggerOptions();

            if (options.Enabled == false)
                return builder;

            // Factory registration: the container disposes the provider on shutdown, which flushes the queue.
            builder.Services.AddSingleton<ILoggerProvider>(_ => new FileLoggerProvider(serviceName, options));
            return builder;
        }
    }
}
