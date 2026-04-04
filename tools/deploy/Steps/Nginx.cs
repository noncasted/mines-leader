namespace deploy;

public static class Nginx
{
    public static async Task Run()
    {
        Console.WriteLine("[Deploy] Configuring nginx...");

        var nginxConfig = await GenerateNginxConfigAsync();
        var configPath = "/etc/nginx/sites-available/default";

        await File.WriteAllTextAsync(configPath, nginxConfig);

        var enabledPath = "/etc/nginx/sites-enabled/default";

        if (File.Exists(enabledPath) == true)
            File.Delete(enabledPath);

        File.CreateSymbolicLink(enabledPath, configPath);

        await Command.Run("nginx", "-t");
        Console.WriteLine("[Deploy] Nginx configuration completed");

        Console.WriteLine("[Deploy] Starting nginx...");
        await Command.Run("service", "nginx start");
    }

    private static async Task<string> GenerateNginxConfigAsync()
    {
        var templatePath = "nginx-server.template";
        var template = await File.ReadAllTextAsync(templatePath);
        var configs = new List<string>();

        foreach (var (from, to) in Options.Ports)
        {
            var config = template
                .Replace("{from}", from.ToString())
                .Replace("{to}", to.ToString());

            configs.Add(config);
        }

        return string.Join("\n", configs);
    }
}