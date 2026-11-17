using System.Windows;

namespace EggTimer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var store = new SettingsStore();
        var settings = store.Load();

        if (e.Args.Length > 0)
        {
            string arg = string.Join(" ", e.Args);
            if (TimeArgParser.TryParse(arg, out int minutes, out int seconds, out string error))
            {
                settings.Minutes = minutes;
                settings.Seconds = seconds;
                store.Save(settings);
            }
            else
            {
                MessageBox.Show(
                    $"{error}\n\nStarting with the last saved time ({settings.Minutes}:{settings.Seconds:00}) instead.",
                    "Egg Timer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        var window = new MainWindow(store, settings);
        MainWindow = window;
        window.Show();
    }
}
