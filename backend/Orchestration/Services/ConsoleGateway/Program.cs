// The separate service of the role: its host alone in the process.
var app = ConsoleGateway.ConsoleHost.Build(new WebApplicationOptions { Args = args });

app.Run();
