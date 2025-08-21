using Microsoft.Extensions.Configuration;
using Projects;
using Console = Projects.Console;

var builder = DistributedApplication.CreateBuilder(args);
var token = Environment.GetEnvironmentVariable("ASPIRE_TOKEN");
System.Console.WriteLine($"11");

if (token != null)
{
    System.Console.WriteLine($"22");
    System.Console.Write($"Token: {token}");

    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppHost:BrowserToken"] = token,
        }
    );
}

var postgres = builder.AddPostgres("postgres")
    .WithAnnotation(new ContainerNameAnnotation()
        {
            Name = "mines-leader-postgres",
        }
    )
    .WithPgAdmin(configure =>
        {
            configure
                .WithAnnotation(new ContainerNameAnnotation()
                    {
                        Name = "mines-leader-pgadmin",
                    }
                )
                .WithLifetime(ContainerLifetime.Persistent);
        }
    )
    .WithLifetime(ContainerLifetime.Persistent);

var startup = builder.AddProject<Startup>("startup")
    .WithReference(postgres)
    .WaitFor(postgres);

var silo = builder.AddProject<Silo>("silo")
    .WaitForCompletion(startup)
    .WithReference(postgres);

builder.AddProject<Coordinator>("coordinator")
    .WaitForStart(silo)
    .WithReference(postgres);

builder.AddProject<BackendGateway>("backend")
    .WaitForStart(silo)
    .WithReference(postgres)
    .WithExternalHttpEndpoints();

builder.AddProject<GameGateway>("game")
    .WaitForStart(silo)
    .WithReference(postgres)
    .WithExternalHttpEndpoints();

builder.AddProject<Console>("console")
    .WaitForStart(silo)
    .WithReference(postgres)
    .WithExternalHttpEndpoints();

builder.Build().Run();