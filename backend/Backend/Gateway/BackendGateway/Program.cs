using Aspire;
using Backend.Gateway;

var builder = WebApplication.CreateBuilder(args);

builder.SetupBackendGateway();

var app = builder.Build();

app.UseHttpsRedirection();

app.AddIdentityEndpoints();
app.AddBackendMiddleware();

app.MapDefaultEndpoints();
app.UseCors("cors");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "api"));
}

app.Run();