using Game.ClientTelemetry;
using Orchestration;

var builder = WebApplication.CreateBuilder(args);

builder.SetupSilo();
builder.AddClientTelemetrySessions();

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();