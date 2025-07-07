using Management.Web;
using MudBlazor.Services;
using Management.Web.Components;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

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