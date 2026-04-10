using GameGateway;
using Orchestration;

var builder = WebApplication.CreateBuilder(args);

builder.SetupGameGateway();

var app = builder.Build();

app.AddMiddleware();
app.MapDefaultEndpoints();
app.AddSessionEndpoints();

app.UseCors("cors");

app.Run();