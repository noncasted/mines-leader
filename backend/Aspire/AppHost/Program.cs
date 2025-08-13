using Projects;
using Console = Projects.Console;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

// var startup = builder.AddProject<Startup>("startup")
//     .WithReference(postgres)
//     .WaitFor(postgres);

var silo = builder.AddProject<Silo>("silo")
//    .WaitForCompletion(startup)
    .WithReference(postgres);

builder.AddProject<BackendGateway>("backend")
    .WaitFor(silo)
    .WithReference(postgres)
    .WithExternalHttpEndpoints();

builder.AddProject<GameGateway>("game")
    .WaitFor(silo)
    .WithReference(postgres)
    .WithExternalHttpEndpoints();

builder.AddProject<Console>("console")
    .WaitFor(silo)
    .WithReference(postgres)
    .WithExternalHttpEndpoints();

builder.Build().Run();