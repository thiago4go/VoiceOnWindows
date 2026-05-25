using System.Diagnostics;
using System.Text.Json;

namespace VoiceOnWindows;

internal sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string AppDirectory { get; }
    public string ConfigPath { get; }
    public string LogPath { get; }
    public string RecordingsDirectory { get; }

    public ConfigStore()
    {
        AppDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Voice On Windows");
        ConfigPath = Path.Combine(AppDirectory, "config.json");
        LogPath = Path.Combine(AppDirectory, "voice-on-windows.log");
        RecordingsDirectory = Path.Combine(AppDirectory, "recordings");
    }

    public AppConfig Load()
    {
        Directory.CreateDirectory(AppDirectory);

        if (!File.Exists(ConfigPath))
        {
            Save(new AppConfig());
        }

        var json = File.ReadAllText(ConfigPath);
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        config.Normalize();
        config.ConfigPath = ConfigPath;
        config.LogPath = LogPath;
        Save(config);
        return config;
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(AppDirectory);
        config.Normalize();
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions) + Environment.NewLine);
    }

    public void Log(string message, object? details = null)
    {
        try
        {
            Directory.CreateDirectory(AppDirectory);
            var line = JsonSerializer.Serialize(new
            {
                at = DateTimeOffset.Now,
                message,
                details
            });
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
}
