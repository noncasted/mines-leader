using Common;
using Infrastructure.Coordination;

var builder = WebApplication.CreateBuilder(args);

builder.SetupCoordinator();
builder.Services.AddHostedService<ClusterCoordinator>();

var app = builder.Build();

app.Run();