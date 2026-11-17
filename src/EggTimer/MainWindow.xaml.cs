using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace EggTimer;

public partial class MainWindow : Window
{
    private const string AppName = "Egg Timer";

    private readonly SettingsStore _store;
    private readonly AppSettings _settings;
    private readonly TimerEngine _engine = new(new StopwatchTimeSource());
    private readonly SoundService _sound = new();
    private readonly DispatcherTimer _ticker;
    private readonly Brush _normalForeground;
    private bool _initializing = true;
    private bool _draggingVolume;

    public MainWindow(SettingsStore store, AppSettings settings)
    {
        _store = store;
        _settings = settings;

        InitializeComponent();
        _normalForeground = DisplayText.Foreground;

        MinutesBox.Value = settings.Minutes;
        SecondsBox.Value = settings.Seconds;
        WarningBeepsCheck.IsChecked = settings.WarningBeeps;
        VolumeSlider.Value = settings.Volume;
        VolumeText.Text = $"{settings.Volume}%";
        RestorePosition();

        MinutesBox.ValueChanged += TimeInput_Changed;
        SecondsBox.ValueChanged += TimeInput_Changed;
        VolumeSlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => _draggingVolume = true));
        VolumeSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) =>
        {
            _draggingVolume = false;
            _sound.PlayBeep(Volume);
        }));

        _ticker = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(50) };
        _ticker.Tick += (_, _) =>
        {
            _engine.Update();
            RefreshDisplay();
        };

        _engine.WarningSecond += (_, _) =>
        {
            if (WarningBeepsCheck.IsChecked == true) _sound.PlayBeep(Volume);
        };
        _engine.Finished += OnFinished;
        _engine.StateChanged += RefreshAll;

        _initializing = false;
        RefreshAll();
    }

    private TimeSpan SetTime => new(0, MinutesBox.Value, SecondsBox.Value);
    private int Volume => (int)Math.Round(VolumeSlider.Value);

    // ----- Button handlers -----

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (SetTime <= TimeSpan.Zero) return;
        if (_engine.Phase is not (TimerPhase.Idle or TimerPhase.Finished)) return;
        _sound.Stop();
        SaveSettings();
        _engine.Start(SetTime);
    }

    private void Pause_Click(object sender, RoutedEventArgs e) => _engine.TogglePause();

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _sound.Stop();
        _engine.Reset();
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out int minutes))
        {
            MinutesBox.Value = minutes;
            SecondsBox.Value = 0;
        }
    }

    private void TimeInput_Changed(object? sender, EventArgs e)
    {
        if (_initializing) return;
        // Editing after the timer finished returns to the idle state showing the new time.
        if (_engine.Phase == TimerPhase.Finished)
        {
            _sound.Stop();
            _engine.Reset();
        }
        SaveSettings();
        RefreshAll();
    }

    private void WarningBeeps_Click(object sender, RoutedEventArgs e) => SaveSettings();

    private void Volume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolumeText is null || _initializing) return;
        VolumeText.Text = $"{Volume}%";
        SaveSettings();
        // Keyboard or track clicks: preview immediately. Thumb drags preview on release.
        if (!_draggingVolume) _sound.PlayBeep(Volume);
    }

    // ----- Timer events -----

    private void OnFinished()
    {
        _sound.PlayAlarm(Volume);

        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        // Toggling Topmost reliably raises the window above others without keeping it pinned.
        Topmost = true;
        Topmost = false;
        NativeMethods.FlashUntilForeground(new WindowInteropHelper(this).Handle);
    }

    // ----- UI refresh -----

    private void RefreshAll()
    {
        var phase = _engine.Phase;
        bool editable = phase is TimerPhase.Idle or TimerPhase.Finished;

        InputPanel.IsEnabled = editable;
        PresetPanel.IsEnabled = editable;
        StartButton.IsEnabled = editable && SetTime > TimeSpan.Zero;
        PauseButton.IsEnabled = phase is TimerPhase.LeadIn or TimerPhase.Running or TimerPhase.Paused;
        PauseButton.Content = phase == TimerPhase.Paused ? "Resume" : "Pause";
        ResetButton.IsEnabled = phase != TimerPhase.Idle;

        bool ticking = phase is TimerPhase.LeadIn or TimerPhase.Running;
        if (ticking && !_ticker.IsEnabled) _ticker.Start();
        else if (!ticking && _ticker.IsEnabled) _ticker.Stop();

        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        var phase = _engine.Phase;
        var remaining = _engine.Remaining;
        bool leadIn = _engine.IsLeadIn; // true during the lead-in, running or paused

        string display;
        string caption;
        string title;

        if (leadIn)
        {
            int n = TimerEngine.CeilSeconds(remaining);
            display = n.ToString();
            caption = phase == TimerPhase.Paused ? "Paused — get ready" : "Get ready…";
            title = $"Starting in {n} – {AppName}";
            DisplayText.Foreground = AccentBrush();
        }
        else
        {
            var shown = phase == TimerPhase.Idle ? SetTime : remaining;
            display = TimerEngine.Format(shown);
            caption = phase switch
            {
                TimerPhase.Paused => "Paused",
                TimerPhase.Finished => "Time's up!",
                _ => string.Empty,
            };
            title = $"{display} – {AppName}";
            DisplayText.Foreground = phase == TimerPhase.Finished ? AccentBrush() : _normalForeground;
        }

        DisplayText.Text = display;
        CaptionText.Text = caption;
        if (Title != title) Title = title;

        // Taskbar progress: elapsed fraction of the main countdown.
        switch (phase)
        {
            case TimerPhase.LeadIn:
                Taskbar.ProgressState = TaskbarItemProgressState.Indeterminate;
                break;
            case TimerPhase.Running:
                Taskbar.ProgressState = TaskbarItemProgressState.Normal;
                Taskbar.ProgressValue = Elapsed(remaining);
                break;
            case TimerPhase.Paused:
                Taskbar.ProgressState = TaskbarItemProgressState.Paused;
                Taskbar.ProgressValue = leadIn ? 0.02 : Math.Max(Elapsed(remaining), 0.02);
                break;
            default:
                Taskbar.ProgressState = TaskbarItemProgressState.None;
                break;
        }
    }

    private double Elapsed(TimeSpan remaining) =>
        _engine.Duration > TimeSpan.Zero
            ? Math.Clamp(1.0 - remaining / _engine.Duration, 0.0, 1.0)
            : 0.0;

    private Brush AccentBrush() =>
        TryFindResource("AccentTextFillColorPrimaryBrush") as Brush
        ?? new SolidColorBrush(SystemParameters.WindowGlassColor);

    // ----- Settings & window position -----

    private void SaveSettings()
    {
        if (_initializing) return;
        _settings.Minutes = MinutesBox.Value;
        _settings.Seconds = SecondsBox.Value;
        _settings.Volume = Volume;
        _settings.WarningBeeps = WarningBeepsCheck.IsChecked == true;
        _store.Save(_settings);
    }

    private void RestorePosition()
    {
        if (_settings.WindowLeft is not double left || _settings.WindowTop is not double top) return;

        // Only restore if the title bar area would be visible on the current monitor layout.
        var screen = new Rect(
            SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (screen.Contains(new Point(left + 40, top + 10)) && screen.Contains(new Point(left + 120, top + 30)))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (!bounds.IsEmpty)
        {
            _settings.WindowLeft = bounds.Left;
            _settings.WindowTop = bounds.Top;
        }
        _ticker.Stop();
        SaveSettings();
        _sound.Dispose();
    }
}
