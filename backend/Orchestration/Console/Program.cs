using Common.Extensions;
using Console;
using Console.Components;
using Orchestration;
using Tests;

var builder = WebApplication.CreateBuilder(args);

builder.SetupConsole();
builder.AddCommonConsoleComponents();
builder.Services.Add<MessagingDirectQueueStressTest.Root>();
builder.Services.Add<MessagingTransactionalQueueStressTest.Root>();
builder.Services.Add<MessagePipeSendStressTest.Root>();
builder.Services.Add<MessagePipeSendResponseStressTest.Root>();

var app = builder.Build();

if (app.Environment.IsDevelopment() == false)
{
    app.UseExceptionHandler("/Error", true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();