using Management.Web;
using Management.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.SetupWeb();

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