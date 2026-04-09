using System.Collections;
using deploy;

Console.WriteLine("[Deploy] Environment variables:");

foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
    Console.WriteLine($"[Deploy]   {entry.Key}={entry.Value}");

await Validation.Run();
await Certificates.Run();
await Nginx.Run();
await Application.Run();

Console.WriteLine("[Deploy] Application started. Press Ctrl+C to stop.");
await Task.Delay(Timeout.Infinite);