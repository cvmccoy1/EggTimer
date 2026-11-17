using System.IO;

namespace EggTimer.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "EggTimerTests-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(5, s.Minutes);
        Assert.Equal(0, s.Seconds);
        Assert.True(s.WarningBeeps);
    }

    [Fact]
    public void Round_trips_all_values()
    {
        var store = new SettingsStore(FilePath);
        store.Save(new AppSettings
        {
            Minutes = 12, Seconds = 34, Volume = 42, WarningBeeps = false, WindowLeft = 100, WindowTop = 200,
        });

        var s = store.Load();
        Assert.Equal(12, s.Minutes);
        Assert.Equal(34, s.Seconds);
        Assert.Equal(42, s.Volume);
        Assert.False(s.WarningBeeps);
        Assert.Equal(100, s.WindowLeft);
        Assert.Equal(200, s.WindowTop);
    }

    [Fact]
    public void Corrupt_file_gives_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");
        Assert.Equal(5, new SettingsStore(FilePath).Load().Minutes);
    }

    [Fact]
    public void Out_of_range_values_are_clamped()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "Minutes": 500, "Seconds": 90, "Volume": -5 }""");
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(99, s.Minutes);
        Assert.Equal(59, s.Seconds);
        Assert.Equal(0, s.Volume);
    }

    [Fact]
    public void Generated_sounds_are_valid_wav()
    {
        foreach (var wav in new[] { SoundService.BuildBeep(0.5), SoundService.BuildAlarm(0.5) })
        {
            Assert.Equal("RIFF"u8.ToArray(), wav[..4]);
            Assert.Equal("WAVE"u8.ToArray(), wav[8..12]);
            Assert.Equal(wav.Length - 8, BitConverter.ToInt32(wav, 4));
        }
    }
}
