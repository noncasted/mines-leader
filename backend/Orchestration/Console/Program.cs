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
builder.Services.Add<StateTest.Root>().As<IClusterTest>();
builder.Services.Add<StateMigrationTest.Root>().As<IClusterTest>();
builder.Services.Add<TransactionStateTest.Root>().As<IClusterTest>();
builder.Services.Add<TransactionStateChainedTest.Root>().As<IClusterTest>();
builder.Services.Add<TransactionStateChainedFailTest.Root>().As<IClusterTest>();
builder.Services.Add<TransactionStateOverlappingTest.Root>().As<IClusterTest>();
builder.Services.Add<TransactionSingleChainTest.Root>().As<IClusterTest>();
builder.Services.Add<TransactionSingleTargetTest.Root>().As<IClusterTest>();

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