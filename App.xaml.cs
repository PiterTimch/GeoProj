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
}

