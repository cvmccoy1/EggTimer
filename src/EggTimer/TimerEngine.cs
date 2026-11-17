using System.Diagnostics;

namespace EggTimer;

public enum TimerPhase
{
    Idle,
    LeadIn,
    Running,
    Paused,
    Finished,
}

/// <summary>Monotonic time source, injectable for tests.</summary>
public interface ITimeSource
{
    TimeSpan Now { get; }
}

public sealed class StopwatchTimeSource : ITimeSource
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    public TimeSpan Now => _stopwatch.Elapsed;
}

/// <summary>
/// UI-independent countdown state machine: a fixed 5 second lead-in followed by the main countdown.
/// Remaining time is computed from a deadline on the time source, so it never drifts regardless of how
/// often <see cref="Update"/> is called.
/// </summary>
public sealed class TimerEngine
{
    public static readonly TimeSpan LeadInDuration = TimeSpan.FromSeconds(5);

    /// <summary>Warning beeps fire when the displayed seconds reach these values.</summary>
    public const int WarningSeconds = 3;

    private readonly ITimeSource _clock;
    private TimeSpan _deadline;
    private TimeSpan _pausedRemaining;
    private int _lastWarned = int.MaxValue;

    public TimerEngine(ITimeSource clock) => _clock = clock;

    public TimerPhase Phase { get; private set; } = TimerPhase.Idle;

    /// <summary>True while the current (possibly paused) segment is the lead-in.</summary>
    public bool IsLeadIn { get; private set; }

    /// <summary>Length of the main countdown for the current run.</summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>Time left in the current segment (lead-in or main countdown).</summary>
    public TimeSpan Remaining => Phase switch
    {
        TimerPhase.LeadIn or TimerPhase.Running => Max(_deadline - _clock.Now, TimeSpan.Zero),
        TimerPhase.Paused => _pausedRemaining,
        TimerPhase.Finished => TimeSpan.Zero,
        _ => Duration,
    };

    /// <summary>Raised with (seconds, isLeadIn) when the displayed time reaches 3, 2 and 1.</summary>
    public event Action<int, bool>? WarningSecond;
    public event Action? LeadInCompleted;
    public event Action? Finished;
    public event Action? StateChanged;

    public void Start(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be positive.");

        Duration = duration;
        IsLeadIn = true;
        BeginSegment(LeadInDuration);
        Phase = TimerPhase.LeadIn;
        StateChanged?.Invoke();
        Update();
    }

    public void Pause()
    {
        if (Phase is not (TimerPhase.LeadIn or TimerPhase.Running)) return;
        _pausedRemaining = Remaining;
        Phase = TimerPhase.Paused;
        StateChanged?.Invoke();
    }

    public void Resume()
    {
        if (Phase != TimerPhase.Paused) return;
        _deadline = _clock.Now + _pausedRemaining;
        Phase = IsLeadIn ? TimerPhase.LeadIn : TimerPhase.Running;
        StateChanged?.Invoke();
        Update();
    }

    public void TogglePause()
    {
        if (Phase == TimerPhase.Paused) Resume();
        else Pause();
    }

    public void Reset()
    {
        IsLeadIn = false;
        Phase = TimerPhase.Idle;
        StateChanged?.Invoke();
    }

    /// <summary>Advance the state machine; call periodically while running.</summary>
    public void Update()
    {
        if (Phase is not (TimerPhase.LeadIn or TimerPhase.Running)) return;

        var remaining = _deadline - _clock.Now;
        if (remaining > TimeSpan.Zero)
        {
            CheckWarning(remaining);
            return;
        }

        if (IsLeadIn)
        {
            IsLeadIn = false;
            // Chain off the old deadline so the overall timing stays exact.
            _deadline += Duration;
            _lastWarned = int.MaxValue;
            Phase = TimerPhase.Running;
            LeadInCompleted?.Invoke();
            StateChanged?.Invoke();
            Update();
            return;
        }

        Phase = TimerPhase.Finished;
        Finished?.Invoke();
        StateChanged?.Invoke();
    }

    private void BeginSegment(TimeSpan length)
    {
        _deadline = _clock.Now + length;
        _lastWarned = int.MaxValue;
    }

    private void CheckWarning(TimeSpan remaining)
    {
        int shown = CeilSeconds(remaining);
        if (shown >= 1 && shown <= WarningSeconds && shown < _lastWarned)
        {
            _lastWarned = shown;
            WarningSecond?.Invoke(shown, IsLeadIn);
        }
    }

    /// <summary>Whole seconds, rounded up, so the display reads 0 only when time is truly up.</summary>
    public static int CeilSeconds(TimeSpan t)
    {
        if (t <= TimeSpan.Zero) return 0;
        return (int)((t.Ticks + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond);
    }

    /// <summary>Formats as MM:SS, rounding partial seconds up.</summary>
    public static string Format(TimeSpan t)
    {
        int total = CeilSeconds(t);
        return $"{total / 60:00}:{total % 60:00}";
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
