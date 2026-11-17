namespace EggTimer.Tests;

public class TimeArgParserTests
{
    [Theory]
    [InlineData("5:30", 5, 30)]
    [InlineData("05:30", 5, 30)]
    [InlineData("0:45", 0, 45)]
    [InlineData("99:59", 99, 59)]
    [InlineData("10:00", 10, 0)]
    [InlineData(" 3:05 ", 3, 5)]
    public void Parses_valid_times(string input, int minutes, int seconds)
    {
        Assert.True(TimeArgParser.TryParse(input, out int m, out int s, out string error), error);
        Assert.Equal(minutes, m);
        Assert.Equal(seconds, s);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("330")]
    [InlineData("5:3")]
    [InlineData("5:300")]
    [InlineData("5:75")]
    [InlineData("120:00")]
    [InlineData("100:00")]
    [InlineData("0:00")]
    [InlineData("-1:00")]
    [InlineData("5:30:00")]
    [InlineData("5m30s")]
    public void Rejects_invalid_times(string? input)
    {
        Assert.False(TimeArgParser.TryParse(input, out _, out _, out string error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
