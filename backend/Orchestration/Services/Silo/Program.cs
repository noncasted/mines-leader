// The separate service of the role: its host alone in the process.
var app = Silo.SiloHost.Build(new WebApplicationOptions { Args = args });

app.Run();
