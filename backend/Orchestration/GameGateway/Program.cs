using GameGateway;
using Orchestration;

var builder = WebApplication.CreateBuilder(args);

builder.SetupGameGateway();
builder.AddConsoleAccess();

var app = builder.Build();

app.AddMiddleware();
app.MapDefaultEndpoints();
app.AddSessionEndpoints();

app.Run();