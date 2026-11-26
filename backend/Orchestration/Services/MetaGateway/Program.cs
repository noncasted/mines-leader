// The separate service of the role: its host alone in the process.
var app = MetaGateway.MetaHost.Build(new WebApplicationOptions { Args = args });

app.Run();
