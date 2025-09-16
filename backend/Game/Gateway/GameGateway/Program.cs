using Aspire;
using Game.Gateway;

var builder = WebApplication.CreateBuilder(args);

builder.SetupGameGateway();

var app = builder.Build();

app.AddMiddleware();
app.UseHttpsRedirection();
app.MapDefaultEndpoints();

app.UseCors("cors");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "api"));
}

app.Run();