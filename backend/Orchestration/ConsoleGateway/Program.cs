using BlazorBlueprint.Components;
using Console;
using Console.Pages.Home;
using ConsoleGateway;
using Orchestration;

var builder = WebApplication.CreateBuilder(args);

builder.SetupConsole();
builder.AddCommonConsoleComponents();
builder.Services.AddBlazorBlueprintComponents();
builder.Services.AddSingleton<IAuditLogStorage, AuditLogStorage>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();

app.AddBenchmarkEndpoints();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddAdditionalAssemblies(typeof(Home).Assembly);

app.Run();