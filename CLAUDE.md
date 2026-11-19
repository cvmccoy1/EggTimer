# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project
Egg Timer is a small Windows 11 countdown timer built with WPF on .NET 10 (`net10.0-windows`). It has no third-party runtime dependencies; the tests use xUnit.

## Commands
```bash
dotnet build                      # build solution (EggTimer.slnx)
dotnet test                       # run all xUnit tests
dotnet run --project src/EggTimer -- 5:30   # run with a preset time
```
Publish the self-contained single-file exe to `publish/`, which git ignores:
```bash
dotnet publish src/EggTimer -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```
- Close any running `EggTimer.exe` before publishing, because the file is locked while it runs.
- Releases are published to GitHub with `gh release create vX.Y.Z publish/EggTimer.exe`, and `<Version>` in the csproj is bumped to match.

## Layout
- `src/EggTimer/TimerEngine.cs`: UI-independent state machine (Idle → LeadIn → Running → Finished, plus Paused). It uses a fixed 5 s lead-in. Remaining time comes from a deadline on an injectable `ITimeSource`, not from counting ticks. `Update()` raises `WarningSecond(n, isLeadIn)` for 3/2/1, `LeadInCompleted` and `Finished`. `Format`/`CeilSeconds` round partial seconds **up**.
- `src/EggTimer/MainWindow.xaml(.cs)`: the UI. A 50 ms `DispatcherTimer` drives `engine.Update()` only while the timer is ticking. The window maps engine events to sounds, the title, taskbar progress (`TaskbarItemInfo`) and the finish behavior (activate + `FlashWindowEx`).
- `src/EggTimer/Controls/NumericUpDown`: custom integer spin box. WPF has no built-in one.
- `src/EggTimer/SoundService.cs`: tones are synthesized as in-memory 16-bit PCM WAV and played with `SoundPlayer`. App volume works by scaling the samples, since system sounds can't be volume-controlled.
- `src/EggTimer/SettingsStore.cs`: JSON at `%AppData%\EggTimer\settings.json`. Loading is tolerant of missing or corrupt files and clamps values. Writes are atomic (temp file + move) and best-effort.
- `src/EggTimer/TimeArgParser.cs`: command-line `M:SS`/`MM:SS` (0–99 min, 00–59 s, > 0). A valid argument is saved as the last time. An invalid one shows a warning and the app falls back to the saved time.
- `src/EggTimer/App.xaml`: the theme and the color palette.
- `tools/make-icon.ps1`: regenerates `src/EggTimer/Assets/EggTimer.ico`. Run it with Windows PowerShell, since it needs System.Drawing.
- `tests/EggTimer.Tests`: parser, engine (with a fake clock) and settings/WAV tests.

## Behavior requirements (agreed with the user, keep them)
- Start always runs a 5 s lead-in. Pause/Resume works during the lead-in and the countdown. Reset returns to the set time.
- The time inputs and presets (1/3/5/10 min) are locked while the timer is in the lead-in, running or paused.
- The warning-beeps checkbox controls the 3-2-1 beeps at the end of the lead-in and before 0:00, and the double beep when the main countdown starts.
- At 0:00 the alarm chime plays **once**, and the window activates and flashes.
- Saved between runs: the last time, the volume, the beeps checkbox and the window position.

## Gotchas
- **Theme:** `ThemeMode="Dark"` (Fluent) is experimental, so `WPF0001` is suppressed in the csproj. The Fluent templates resolve many brushes with `StaticResource` inside the theme dictionary. Overriding generic accent keys therefore doesn't recolor controls. Override the control-level keys instead (e.g. `CheckBoxCheckBackgroundFillChecked`, `SliderThumbBackground`) or set properties directly (the slider's `Foreground`).
- **Interop:** `LibraryImport` (`NativeMethods.cs`) requires `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.
- **Tests:** the test project targets `net10.0-windows` and references the WPF exe project. Internals are exposed through `InternalsVisibleTo`.
- **Settings during testing:** running the app by hand overwrites the user's real settings file. Back it up and restore it when doing automated UI checks.
