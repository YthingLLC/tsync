using Microsoft.Extensions.Configuration;

public class Settings
{
    public string? ClientId { get; init; }
    public string? TenantId { get; init; }
    public string[]? GraphUserScopes { get; init; }

    public string? TrelloApiKey { get; init; }

    public string? TrelloUserToken { get; init; }

    public string? DownloadPath { get; init; }


    public static Settings LoadSettings()
    {
        // Load settings
        IConfiguration config = new ConfigurationBuilder()
            // appsettings.json is required
            .AddJsonFile("appsettings.json", false)
            // appsettings.Development.json" is optional, values override appsettings.json
            .AddJsonFile("appsettings.Development.json", true)
            .Build();

        return config.GetRequiredSection("Settings").Get<Settings>() ??
               throw new Exception("Could not load app settings. See README for configuration instructions.");
    }
}