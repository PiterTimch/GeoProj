using Mapsui;
using System.Windows;
using System.Windows.Input;
using Mapsui.Extensions;
using GeoProj.ViewModels;
using Mapsui.UI.Wpf.Extensions;

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

    private void OnMapClicked_Interact(object sender, MapInfoEventArgs e)
    {
        if (e.MapInfo?.WorldPosition == null || _viewModel == null) return;

        _viewModel.HideInfo();
        var worldPos = e.MapInfo.WorldPosition;

        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
        {
            _viewModel.AddSource(worldPos);
        }
        else
        {
            _viewModel.SelectSourceByMapInfo(e.MapInfo);
        }
    }

    private void MapControl_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null || MapControl.Map == null) return;

        var screenPositionWpf = e.GetPosition(MapControl);
        var screenPositionMapsui = screenPositionWpf.ToMapsui();
        var viewport = MapControl.Map.Navigator.Viewport;

        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
        {
            var worldPos = viewport.ScreenToWorld(screenPositionMapsui);
            _viewModel.AddCustomReceptor(worldPos);
            e.Handled = true;
            return;
        }

        var mapInfo = MapControl.GetMapInfo(screenPositionMapsui, (int)viewport.Resolution);

        if (mapInfo == null || mapInfo.MapInfoRecords == null || !mapInfo.MapInfoRecords.Any())
        {
            _viewModel.HideInfo();
            return;
        }

        _viewModel.ShowFeatureInfo(mapInfo.MapInfoRecords.FirstOrDefault()?.Feature);
    }

    private void MapControl_MouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel == null || MapControl.Map == null) return;

        try
        {
            var screenPosWpf = e.GetPosition(MapControl);
            var screenPositionMapsui = screenPosWpf.ToMapsui();

            var viewport = MapControl.Map.Navigator.Viewport;
            var worldPos = viewport.ScreenToWorld(screenPositionMapsui);
            _viewModel.UpdateMouseCoordinates(worldPos);
        }
        catch (Exception)
        {
            _viewModel.UpdateMouseCoordinates(null);
        }
    }

    private void MapControl_MouseLeave(object sender, MouseEventArgs e)
    {
        _viewModel?.UpdateMouseCoordinates(null);
    }
}