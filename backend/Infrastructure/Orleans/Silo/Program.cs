using Aspire;
using Common;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using ServiceLoop;

var builder = WebApplication.CreateBuilder(args);

// Basic services
builder
    .AddServiceDefaults()
    .AddOrleans();

// Cluster services
builder
    .AddEnvironment(ServiceTag.Silo)
    .AddServiceLoop()
    .AddMessaging()
    .AddOrleansUtils()
    .AddStateAttributes()
    .AddServersCollection();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();