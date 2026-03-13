using Common.Extensions;
using Console;
using Console.Components;
using Orchestration;
using Tests;

var builder = WebApplication.CreateBuilder(args);

builder.SetupConsole();
builder.AddCommonConsoleComponents();

builder.Services.Add<MessagingDirectQueueStressTest.Root>().As<IClusterTest>();
builder.Services.Add<MessagingTransactionalQueueStressTest.Root>().As<IClusterTest>();
builder.Services.Add<MessagePipeSendStressTest.Root>().As<IClusterTest>();
builder.Services.Add<MessagePipeSendResponseStressTest.Root>().As<IClusterTest>();
builder.Services.Add<GrainStateTest.Root>().As<IClusterTest>();
builder.Services.Add<TransactionStateTest.Root>().As<IClusterTest>();
builder.Services.Add<ChainedTransactionStateTest.Root>().As<IClusterTest>();
builder.Services.Add<ChainedTransactionStateTestFail.Root>().As<IClusterTest>();
builder.Services.Add<TransactionLimiterTest.Root>().As<IClusterTest>();

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