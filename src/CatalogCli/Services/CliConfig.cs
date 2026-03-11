using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatalogCli.Services;

public class CliConfig
{
    [JsonPropertyName("serverUrl")]
    public string ServerUrl { get; set; } = string.Empty;

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    // ── File location ─────────────────────────────────────────────────────────

    public static string ConfigFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "catalogcli",
            "settings.json");

    // ── Load / Save ───────────────────────────────────────────────────────────

    public static CliConfig Load()
    {
        var path = ConfigFilePath;
        if (!File.Exists(path))
            return new CliConfig();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CliConfig>(json) ?? new CliConfig();
        }
        catch
        {
            return new CliConfig();
        }
    }

    public void Save()
    {
        var path = ConfigFilePath;
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    // ── Priority resolution (flag > env > config file) ────────────────────────

    public static (string? serverUrl, string? token) Resolve(string? flagServer, string? flagToken)
    {
        var config = Load();

        var serverUrl = flagServer
            ?? Environment.GetEnvironmentVariable("ESTIMATOR_API_URL")
            ?? (string.IsNullOrEmpty(config.ServerUrl) ? null : config.ServerUrl);

        var token = flagToken
            ?? Environment.GetEnvironmentVariable("ESTIMATOR_API_TOKEN")
            ?? (string.IsNullOrEmpty(config.Token) ? null : config.Token);

        return (serverUrl, token);
    }
}
