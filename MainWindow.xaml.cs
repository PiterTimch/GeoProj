using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Projections;
using Mapsui.UI.Wpf;
using Mapsui.Limiting;
using System.Windows;
using Mapsui.Tiling;
using System.Windows.Controls;
using NetTopologySuite.Geometries;
using Mapsui.Nts;

namespace GeoProj;

public partial class MainWindow : Window
{

    private readonly MapControl _mapView;
    private ILayer? _baseMapLayer;
    private readonly ContextMenu _settingsMenu;

    public MainWindow()
    {
        InitializeComponent();
        _mapView = MapControl;
        _ = SetupMap();

        _settingsMenu = new ContextMenu();
        var toggleMapMenuItem = new MenuItem
        {
            Header = "Показати/Сховати карту",
            IsCheckable = true,
            IsChecked = true
        };
        toggleMapMenuItem.Click += ToggleMapMenuItem_Click;
        _settingsMenu.Items.Add(toggleMapMenuItem);
    }

    private async Task SetupMap()
    {
        var (highFeatures, lowFeatures) = await Task.Run(async () =>
        {
            var highList = new List<IFeature>();
            var lowList = new List<IFeature>();

            var buildings = BuildingLoader.LoadBuildings("Data/buildings.geojson");
            
            // Тест конвертації
            //------------------------
            var segment = buildings.Where(x => x.HeightMeters > 0).Take(20).ToList();

            InputFileExporter fileExporter = new InputFileExporter
            {
                Buildings = segment
            };

            await fileExporter.ExportToAermodInputAsync("Output");
            //------------------------

            foreach (var b in buildings)
            {
                Geometry geom = b.Polygon;
                if (geom == null) continue;

                if (geom is MultiPolygon mp)
                {
                    foreach (Polygon sub in mp.Geometries)
                        ConvertAndAddFeature(sub, b, highList, lowList);
                }
                else if (geom is Polygon poly)
                {
                    ConvertAndAddFeature(poly, b, highList, lowList);
                }
            }

            return (highList, lowList);
        });

        var highStyle = new VectorStyle
        {
            Fill = new Brush(new Color(255, 50, 50, 200)),
            Outline = new Pen(new Color(50, 50, 50), 1)
        };

        var lowStyle = new VectorStyle
        {
            Fill = new Brush(new Color(150, 0, 255, 180)),
            Outline = new Pen(new Color(40, 40, 40), 1)
        };

        var highProvider = new MemoryProvider(highFeatures);
        var lowProvider = new MemoryProvider(lowFeatures);

        var highLayer = new Layer("Buildings_High") { DataSource = highProvider, Style = highStyle };
        var lowLayer = new Layer("Buildings_Low") { DataSource = lowProvider, Style = lowStyle };

        _mapView.Map?.Layers.Add(lowLayer);
        _mapView.Map?.Layers.Add(highLayer);

        var map = new Map { CRS = "EPSG:3857", BackColor = Color.Black };
        map.Layers.Add(OpenStreetMap.CreateTileLayer());
        map.Layers.Add(lowLayer);
        map.Layers.Add(highLayer);

        _mapView.Map = map;

        const double lon = 25.5948, lat = 49.5535;
        var (x, y) = SphericalMercator.FromLonLat(lon, lat);
        _mapView.Map.Navigator.CenterOn(new MPoint(x, y));
        _mapView.Map.Navigator.ZoomTo(2000);

        if (_mapView.Map == null)
        {
            _mapView.Refresh();
        }

    }

    private void ConvertAndAddFeature(Polygon poly, BuildingFootprint b, List<IFeature> highList, List<IFeature> lowList)
    {
        if (poly == null || poly.Coordinates == null || poly.Coordinates.Length < 3) return;

        var coords = poly.Coordinates.Select(c =>
        {
            var (mx, my) = SphericalMercator.FromLonLat(c.X, c.Y);
            return new Coordinate(mx, my);
        }).ToList();

        if (!coords.First().Equals2D(coords.Last()))
            coords.Add(coords.First().Copy());

        var shell = new LinearRing(coords.ToArray());
        var converted = new Polygon(shell);

        var feature = new GeometryFeature { Geometry = converted };

        foreach (var kv in b.Tags)
            feature[kv.Key] = kv.Value;

        feature["height_m"] = b.HeightMeters;

        if (b.HeightMeters > 1.0)
        {
            highList.Add(feature);
        }
        else
        {
            lowList.Add(feature);
        }
    }

    public void ToggleBaseMapVisibility()
    {
        if (_baseMapLayer != null)
        {
            _baseMapLayer.Enabled = !_baseMapLayer.Enabled;
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _settingsMenu.PlacementTarget = sender as Button;
        _settingsMenu.IsOpen = true;
    }

    private void ToggleMapMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ToggleBaseMapVisibility();
    }
}

public class CustomViewportLimiter : IViewportLimiter
{
    private readonly ViewportLimiter _limiter = new ViewportLimiter();
    private readonly MRect? _panBounds;
    private readonly MMinMax? _zoomBounds;

    public CustomViewportLimiter(MRect? panBounds, MMinMax? zoomBounds)
    {
        _panBounds = panBounds;
        _zoomBounds = zoomBounds;
    }

    public Viewport Limit(Viewport viewport)
    {
        return _limiter.Limit(viewport, _panBounds, _zoomBounds);
    }

    public Viewport Limit(Viewport viewport, MRect? panBounds, MMinMax? zoomBounds)
    {
        return _limiter.Limit(viewport, _panBounds, _zoomBounds);
    }
}