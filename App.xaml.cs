using GeoProj.Services;
using GeoProj.ViewModels;
using System.Windows;

namespace GeoProj;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        IAermodService aermodService = new AermodService();

        MainViewModel mainViewModel = new MainViewModel(aermodService);

        MainWindow mainWindow = new MainWindow();

        mainWindow.DataContext = mainViewModel;

        mainWindow.Show();
    }

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            LogCrash((Exception)e.ExceptionObject, "AppDomain.UnhandledException");
        };

        DispatcherUnhandledException += (s, e) =>
        {
            LogCrash(e.Exception, "Dispatcher.UnhandledException");
            e.Handled = true;
        };
    }

    private void LogCrash(Exception ex, string eventName)
    {
        try
        {
            string logMessage = $"FATAL CRASH on {eventName}:\n" +
                                $"Time: {DateTime.Now}\n" +
                                $"Message: {ex.Message}\n" +
                                $"Stack Trace:\n{ex.StackTrace}\n\n";

            string exePath = AppDomain.CurrentDomain.BaseDirectory;
            string logPath = System.IO.Path.Combine(exePath, "FATAL_CRASH_LOG.txt");

            System.IO.File.WriteAllText(logPath, logMessage);

            MessageBox.Show($"Fatal error. Progtam is crashed\n" +
                            $"Please, send file 'FATAL_CRASH_LOG.txt' to the developer.\n\n" +
                            $"Error: {ex.Message}",
                            "Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception logEx)
        {
            MessageBox.Show($"Fatal logging error: {logEx.Message}\n" +
                            $"Main error: {ex.Message}",
                            "Double Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Environment.Exit(1);
        }
    }
}

