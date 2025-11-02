using deploy;

await Validation.Run();
await Certificates.Run();
await Nginx.Run();
await Application.Run();

Console.WriteLine("[Deploy] Application started. Press Ctrl+C to stop.");
await Task.Delay(Timeout.Infinite);