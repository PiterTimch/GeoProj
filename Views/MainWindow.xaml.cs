using Mapsui;
using System.Windows;
using System.Windows.Input;
using Mapsui.Extensions;
using GeoProj.ViewModels;

namespace GeoProj;

public partial class MainWindow : Window
{
    private MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
        if (_viewModel == null)
        {
            MessageBox.Show("Фатальна помилка: ViewModel не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        await _viewModel.InitializeMapAsync();
        MapControl.Map = _viewModel.Map;
    }

    private void OnMapClicked_AddSourcePoint(object sender, MapInfoEventArgs e)
    {
        if (e.MapInfo?.WorldPosition == null || _viewModel == null) return;

        var mapPoint = e.MapInfo.WorldPosition;
        _viewModel.SetSourcePoint(mapPoint);
    }
    private void MapControl_MouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel == null) return;

        try
        {
            var screenPos = e.GetPosition(MapControl);
            var worldPos = MapControl.Map.Navigator.Viewport.ScreenToWorld(new MPoint(screenPos.X, screenPos.Y));
            _viewModel.UpdateMouseCoordinates(worldPos);
        }
        catch
        {
            _viewModel.UpdateMouseCoordinates(null);
        }
    }
}