using Aspire;
using Backend.Gateway;
using Backend.Matches;
using Backend.Users;
using Common;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using ServiceLoop;

var builder = WebApplication.CreateBuilder(args);

// Basic services
builder
    .AddServiceDefaults()
    .AddOrleansClient();

// Cluster services
builder
    .AddEnvironment(ServiceTag.Backend)
    .AddServiceLoop()
    .AddMessaging()
    .AddOrleansUtils()
    .AddStateAttributes()
    .AddServersCollection()
    .ConfigureCors();

// Project services
builder
    .AddUserFlow()
    .AddUserFactory()
    .AddBackendMatchServices()
    .AddMatchmakingServices();

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseHttpsRedirection();

app
    .AddIdentityEndpoints()
    .AddUserEndpoints()
    .AddMatchmakingEndpoints();

app.AddBackendMiddleware();

app.MapDefaultEndpoints();
app.UseCors("cors");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "api"));
}

app.Run();