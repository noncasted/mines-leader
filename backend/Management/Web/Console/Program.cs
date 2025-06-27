using Aspire;
using Common;
using Infrastructure.Discovery;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using Management.Configs;
using Management.Web;
using MudBlazor.Services;
using Management.Web.Components;
using ServiceLoop;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Basic services
builder
    .AddServiceDefaults()
    .AddOrleansClient();

// Cluster services
builder
    .AddEnvironment(ServiceTag.Console)
    .AddServiceLoop()
    .AddMessaging()
    .AddOrleansUtils()
    .AddServersCollection()
    .AddConfigsServices();

// Project services
services
    .AddMudServices()
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.AddCommonConsoleComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(Main).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();