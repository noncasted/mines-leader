using Aspire;
using Backend.Matches;
using Common;
using Game;
using Game.Gateway;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Infrastructure.Orleans;

var builder = WebApplication.CreateBuilder(args);

// Basic services
builder
    .AddServiceDefaults()
    .AddOrleansClient();

// Cluster services
builder
    .AddEnvironment(ServiceTag.Server)
    .AddMessaging()
    .AddOrleansUtils()
    .AddServerOverviewPusher();

// Project services
builder
    .AddGameMatchServices()
    .AddGlobalSessions()
    .AddSessionServices();

builder.Services
    .AddOpenApi()
    .AddCors();

var app = builder.Build();

app.AddMiddleware();
app.UseHttpsRedirection();
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "api"));
}

app.Run();