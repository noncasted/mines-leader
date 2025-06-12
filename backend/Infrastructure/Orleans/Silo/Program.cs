using Aspire;
using Common;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Infrastructure.Orleans;

var builder = WebApplication.CreateBuilder(args);

// Basic services
builder
    .AddServiceDefaults()
    .AddOrleans();

// Cluster services
builder
    .AddEnvironment(ServiceTag.Silo)
    .AddMessaging()
    .AddOrleansUtils()
    .AddStateAttributes()
    .AddServersCollection();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();