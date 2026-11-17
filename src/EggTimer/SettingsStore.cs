using System.IO;
using System.Text.Json;

namespace EggTimer;

public sealed class AppSettings
{
    public int Minutes { get; set; } = 5;
    public int Seconds { get; set; }
    /// <summary>Volume in percent, 0-100.</summary>
    public int Volume { get; set; } = 70;
    public bool WarningBeeps { get; set; } = true;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    /// <summary>Clamp any out-of-range values from a hand-edited or corrupt file.</summary>
    public void Normalize()
    {
        Minutes = Math.Clamp(Minutes, 0, TimeArgParser.MaxMinutes);
        Seconds = Math.Clamp(Seconds, 0, 59);
        Volume = Math.Clamp(Volume, 0, 100);
        if (WindowLeft is double l && !double.IsFinite(l)) WindowLeft = null;
        if (WindowTop is double t && !double.IsFinite(t)) WindowTop = null;
    }
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SettingsStore(string? path = null)
    {
        FilePath = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EggTimer",
            "settings.json");
    }

    public string FilePath { get; }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
                if (settings is not null)
                {
                    settings.Normalize();
                    return settings;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Fall back to defaults on a missing, locked or corrupt file.
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Saving settings is best-effort; never crash the timer over it.
        }
    }
}
