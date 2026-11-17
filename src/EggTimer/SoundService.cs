using System.IO;
using System.Media;

namespace EggTimer;

/// <summary>
/// Synthesizes the warning beep and the end-of-timer chime as in-memory WAV data so playback volume can
/// be controlled by the app (system sounds ignore an app-level volume).
/// </summary>
public sealed class SoundService : IDisposable
{
    private const int SampleRate = 44100;

    private readonly Dictionary<(string, int), byte[]> _cache = new();
    private SoundPlayer? _player;

    /// <summary>Short beep used for the 3-2-1 warnings.</summary>
    public void PlayBeep(int volumePercent) => Play("beep", volumePercent, BuildBeep);

    /// <summary>Higher-pitched double beep signalling that the main countdown has begun.</summary>
    public void PlayDoubleBeep(int volumePercent) => Play("double", volumePercent, BuildDoubleBeep);

    /// <summary>Rising four-note chime played once when the timer reaches zero.</summary>
    public void PlayAlarm(int volumePercent) => Play("alarm", volumePercent, BuildAlarm);

    public void Stop()
    {
        _player?.Stop();
    }

    public void Dispose()
    {
        _player?.Stop();
        _player?.Dispose();
        _player = null;
    }

    private void Play(string name, int volumePercent, Func<double, byte[]> build)
    {
        volumePercent = Math.Clamp(volumePercent, 0, 100);
        if (volumePercent == 0) return;

        if (!_cache.TryGetValue((name, volumePercent), out var wav))
        {
            // Square the slider position so the control feels perceptually even.
            double amplitude = Math.Pow(volumePercent / 100.0, 2);
            wav = build(amplitude);
            _cache[(name, volumePercent)] = wav;
        }

        _player?.Stop();
        _player?.Dispose();
        _player = new SoundPlayer(new MemoryStream(wav, writable: false));
        try
        {
            _player.Play(); // asynchronous
        }
        catch (InvalidOperationException)
        {
            // No audio device, etc. Sound is non-essential.
        }
    }

    internal static byte[] BuildBeep(double amplitude)
    {
        var samples = new List<short>();
        AppendTone(samples, 880, 0.15, amplitude * 0.9, decay: false);
        return ToWav(samples);
    }

    internal static byte[] BuildDoubleBeep(double amplitude)
    {
        var samples = new List<short>();
        AppendTone(samples, 1320, 0.10, amplitude * 0.9, decay: false);
        AppendSilence(samples, 0.07);
        AppendTone(samples, 1320, 0.10, amplitude * 0.9, decay: false);
        return ToWav(samples);
    }

    internal static byte[] BuildAlarm(double amplitude)
    {
        var samples = new List<short>();
        double[] notes = [1046.50, 1318.51, 1567.98]; // C6, E6, G6
        foreach (double f in notes)
            AppendTone(samples, f, 0.22, amplitude, decay: true);
        AppendTone(samples, 2093.00, 0.9, amplitude, decay: true); // C7, held
        return ToWav(samples);
    }

    private static void AppendTone(List<short> samples, double frequency, double seconds, double amplitude, bool decay)
    {
        int count = (int)(SampleRate * seconds);
        int fade = (int)(SampleRate * 0.006); // short attack/release to avoid clicks
        for (int i = 0; i < count; i++)
        {
            double t = (double)i / SampleRate;
            double env = 1.0;
            if (i < fade) env = (double)i / fade;
            else if (i > count - fade) env = (double)(count - i) / fade;
            if (decay) env *= Math.Exp(-3.0 * t / seconds);

            // Fundamental plus a soft octave overtone for a bell-like tone.
            double v = Math.Sin(2 * Math.PI * frequency * t) * 0.8
                     + Math.Sin(2 * Math.PI * frequency * 2 * t) * 0.2;
            samples.Add((short)Math.Clamp(v * env * amplitude * short.MaxValue, short.MinValue, short.MaxValue));
        }
    }

    private static void AppendSilence(List<short> samples, double seconds)
        => samples.AddRange(Enumerable.Repeat((short)0, (int)(SampleRate * seconds)));

    private static byte[] ToWav(List<short> samples)
    {
        const short channels = 1;
        const short bitsPerSample = 16;
        int dataSize = samples.Count * sizeof(short);

        using var ms = new MemoryStream(44 + dataSize);
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8);
        w.Write(36 + dataSize);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);                                          // PCM chunk size
        w.Write((short)1);                                    // PCM format
        w.Write(channels);
        w.Write(SampleRate);
        w.Write(SampleRate * channels * bitsPerSample / 8);   // byte rate
        w.Write((short)(channels * bitsPerSample / 8));       // block align
        w.Write(bitsPerSample);
        w.Write("data"u8);
        w.Write(dataSize);
        foreach (short s in samples) w.Write(s);
        w.Flush();
        return ms.ToArray();
    }
}
