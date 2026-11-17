namespace EggTimer.Tests;

public class TimerEngineTests
{
    private sealed class FakeClock : ITimeSource
    {
        public TimeSpan Now { get; set; }
        public void Advance(double seconds) => Now += TimeSpan.FromSeconds(seconds);
    }

    private readonly FakeClock _clock = new();
    private readonly TimerEngine _engine;

    public TimerEngineTests() => _engine = new TimerEngine(_clock);

    private void Run(double seconds, double step = 0.05)
    {
        int steps = (int)Math.Round(seconds / step);
        for (int i = 0; i < steps; i++)
        {
            _clock.Advance(step);
            _engine.Update();
        }
    }

    [Fact]
    public void Starts_with_five_second_lead_in()
    {
        _engine.Start(TimeSpan.FromSeconds(10));
        Assert.Equal(TimerPhase.LeadIn, _engine.Phase);
        Assert.True(_engine.IsLeadIn);
        Assert.Equal(TimeSpan.FromSeconds(5), _engine.Remaining);
    }

    [Fact]
    public void Lead_in_then_countdown_then_finished()
    {
        int leadInCompleted = 0, finished = 0;
        _engine.LeadInCompleted += () => leadInCompleted++;
        _engine.Finished += () => finished++;

        _engine.Start(TimeSpan.FromSeconds(10));
        Run(5);
        Assert.Equal(TimerPhase.Running, _engine.Phase);
        Assert.False(_engine.IsLeadIn);
        Assert.Equal(1, leadInCompleted);
        Assert.Equal("00:10", TimerEngine.Format(_engine.Remaining));

        Run(9.9);
        Assert.Equal(TimerPhase.Running, _engine.Phase);
        Assert.Equal("00:01", TimerEngine.Format(_engine.Remaining));

        Run(0.1);
        Assert.Equal(TimerPhase.Finished, _engine.Phase);
        Assert.Equal(1, finished);
        Assert.Equal(TimeSpan.Zero, _engine.Remaining);

        Run(2);
        Assert.Equal(1, finished);
    }

    [Fact]
    public void Warning_seconds_fire_once_each_in_lead_in_and_at_end()
    {
        var warnings = new List<(int, bool)>();
        _engine.WarningSecond += (n, leadIn) => warnings.Add((n, leadIn));

        _engine.Start(TimeSpan.FromSeconds(10));
        Run(16);

        Assert.Equal(
            [(3, true), (2, true), (1, true), (3, false), (2, false), (1, false)],
            warnings);
    }

    [Fact]
    public void Short_timer_warns_for_available_seconds_only()
    {
        var warnings = new List<(int, bool)>();
        _engine.WarningSecond += (n, leadIn) => warnings.Add((n, leadIn));

        _engine.Start(TimeSpan.FromSeconds(2));
        Run(8);

        Assert.Equal([(3, true), (2, true), (1, true), (2, false), (1, false)], warnings);
        Assert.Equal(TimerPhase.Finished, _engine.Phase);
    }

    [Fact]
    public void Pause_and_resume_during_lead_in()
    {
        _engine.Start(TimeSpan.FromSeconds(10));
        Run(2);
        _engine.Pause();
        Assert.Equal(TimerPhase.Paused, _engine.Phase);

        Run(30);
        Assert.Equal(TimerPhase.Paused, _engine.Phase);
        Assert.True(_engine.IsLeadIn);
        Assert.Equal(3, TimerEngine.CeilSeconds(_engine.Remaining));

        _engine.TogglePause();
        Assert.Equal(TimerPhase.LeadIn, _engine.Phase);
        Run(3);
        Assert.Equal(TimerPhase.Running, _engine.Phase);
        Assert.Equal("00:10", TimerEngine.Format(_engine.Remaining));
    }

    [Fact]
    public void Pause_and_resume_during_countdown()
    {
        _engine.Start(TimeSpan.FromSeconds(10));
        Run(5 + 4);
        _engine.TogglePause();
        Assert.Equal(TimerPhase.Paused, _engine.Phase);

        Run(60);
        Assert.Equal("00:06", TimerEngine.Format(_engine.Remaining));

        _engine.Resume();
        Assert.Equal(TimerPhase.Running, _engine.Phase);
        Run(6);
        Assert.Equal(TimerPhase.Finished, _engine.Phase);
    }

    [Fact]
    public void Reset_returns_to_idle_and_stops_events()
    {
        int finished = 0;
        _engine.Finished += () => finished++;

        _engine.Start(TimeSpan.FromSeconds(3));
        Run(6);
        _engine.Reset();
        Assert.Equal(TimerPhase.Idle, _engine.Phase);
        Assert.False(_engine.IsLeadIn);

        Run(10);
        Assert.Equal(TimerPhase.Idle, _engine.Phase);
        Assert.Equal(0, finished);
    }

    [Fact]
    public void Restart_after_finish_runs_again()
    {
        _engine.Start(TimeSpan.FromSeconds(1));
        Run(7);
        Assert.Equal(TimerPhase.Finished, _engine.Phase);

        _engine.Start(TimeSpan.FromSeconds(1));
        Assert.Equal(TimerPhase.LeadIn, _engine.Phase);
    }

    [Fact]
    public void Large_stall_still_completes_correctly()
    {
        int leadInCompleted = 0, finished = 0;
        _engine.LeadInCompleted += () => leadInCompleted++;
        _engine.Finished += () => finished++;

        _engine.Start(TimeSpan.FromSeconds(10));
        _clock.Advance(100);
        _engine.Update();

        Assert.Equal(1, leadInCompleted);
        Assert.Equal(1, finished);
        Assert.Equal(TimerPhase.Finished, _engine.Phase);
    }

    [Fact]
    public void Zero_duration_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.Start(TimeSpan.Zero));
    }

    [Theory]
    [InlineData(0.0, "00:00")]
    [InlineData(0.01, "00:01")]
    [InlineData(4.01, "00:05")]
    [InlineData(5.0, "00:05")]
    [InlineData(330.0, "05:30")]
    [InlineData(5999.0, "99:59")]
    public void Format_rounds_up_to_whole_seconds(double seconds, string expected)
    {
        Assert.Equal(expected, TimerEngine.Format(TimeSpan.FromSeconds(seconds)));
    }
}
