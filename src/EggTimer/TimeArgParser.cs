using System.Globalization;
using System.Text.RegularExpressions;

namespace EggTimer;

public static partial class TimeArgParser
{
    public const int MaxMinutes = 99;

    [GeneratedRegex(@"^\s*(\d+):(\d+)\s*$")]
    private static partial Regex TimePattern();

    /// <summary>
    /// Parses "M:SS" or "MM:SS" (minutes 0-99, seconds 00-59, total &gt; 0).
    /// </summary>
    public static bool TryParse(string? text, out int minutes, out int seconds, out string error)
    {
        minutes = 0;
        seconds = 0;
        error = string.Empty;

        var match = TimePattern().Match(text ?? string.Empty);
        if (!match.Success || match.Groups[2].Value.Length != 2)
        {
            error = $"\"{text}\" is not a valid time. Use minutes and seconds like 5:30 or 05:30.";
            return false;
        }

        if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int m)
            || m > MaxMinutes)
        {
            error = $"\"{text}\" has too many minutes. The maximum is {MaxMinutes}:59.";
            return false;
        }

        int s = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        if (s > 59)
        {
            error = $"\"{text}\" has invalid seconds. Seconds must be 00 to 59.";
            return false;
        }

        if (m == 0 && s == 0)
        {
            error = "The time must be greater than 0:00.";
            return false;
        }

        minutes = m;
        seconds = s;
        return true;
    }
}
