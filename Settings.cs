using System.Text.Json;

namespace GamepadLauncher;

public class Settings
{
    public string GamePath { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool LaunchIfAlreadyConnected { get; set; }
    public int DelaySeconds { get; set; }

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GamepadLauncher", "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { /* битый файл — начнём с чистых настроек */ }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
