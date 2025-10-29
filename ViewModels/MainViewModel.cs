using GeoProj.Helpers;
using GeoProj.Services;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Providers;
using Mapsui.Styles.Thematics;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems.Transformations;
using ProjNet.CoordinateSystems;
using System.Windows.Input;
using System.Windows;
using System.Diagnostics;
using SkiaSharp;
using GeoProj.Models;

namespace GeoProj.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly IAermodService _aermodService;

        private Map _map;
        public Map Map
        {
            get => _map;
            set => SetProperty(ref _map, value);
        }

        private ILayer _sourcePointLayer;
        private readonly List<IFeature> _sourcePointFeatures = new List<IFeature>();
        private MPoint _selectedSourcePoint;
        private ILayer _resultHeatmapLayer;
        private Dictionary<string, List<DispersionDataPoint>> _simulationResults = new Dictionary<string, List<DispersionDataPoint>>();

        private double _emissionRate = 100.0;
        public double EmissionRate { get => _emissionRate; set => SetProperty(ref _emissionRate, value); }

        private double _stackHeight = 50.0;
        public double StackHeight { get => _stackHeight; set => SetProperty(ref _stackHeight, value); }

        private double _stackTemp = 450.0;
        public double StackTemp { get => _stackTemp; set => SetProperty(ref _stackTemp, value); }

        private double _stackVelocity = 20.0;
        public double StackVelocity { get => _stackVelocity; set => SetProperty(ref _stackVelocity, value); }

        private double _stackDiameter = 1.5;
        public double StackDiameter { get => _stackDiameter; set => SetProperty(ref _stackDiameter, value); }

        public List<string> LayerOptions { get; } = new List<string>
        {
            "Не показувати",
            "1-годинний максимум",
            "3-годинний максимум",
            "24-годинний максимум",
            "Середнє за період"
        };

        private string _selectedLayerOption = "Не показувати";
        public string SelectedLayerOption
        {
            get => _selectedLayerOption;
            set
            {
                if (SetProperty(ref _selectedLayerOption, value))
                {
                    OnLayerSelectionChanged();
                }
            }
        }

        private string _statusText = "Готовий. Оберіть точку на карті.";
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        private string _coordsText = "X: ---   Y: ---";
        public string CoordsText { get => _coordsText; set => SetProperty(ref _coordsText, value); }

        private bool _isSimulationRunning = false;
        public bool IsSimulationRunning { get => _isSimulationRunning; set => SetProperty(ref _isSimulationRunning, value); }

        public ICommand RunSimulationCommand { get; }

        public MainViewModel(IAermodService aermodService)
        {
            _aermodService = aermodService;

            RunSimulationCommand = new RelayCommand(async (_) => await RunSimulationAsync(), (_) => CanRunSimulation());

            _sourcePointLayer = new MemoryLayer
            {
                Name = "Source Point",
                Features = _sourcePointFeatures,
                Style = CreateSourceMarkerStyle(),
                IsMapInfoLayer = true
            };

            //_ = SetupMap();
        }

        private bool CanRunSimulation()
        {
            return _selectedSourcePoint != null && !IsSimulationRunning;
        }

        private async Task RunSimulationAsync()
        {
            if (!CanRunSimulation()) return;

            IsSimulationRunning = true;
            CommandManager.InvalidateRequerySuggested();

            AermodSourceParameters sourceParams;
            try
            {
                sourceParams = new AermodSourceParameters
                {
                    EmissionRate = this.EmissionRate,
                    StackHeight = this.StackHeight,
                    StackTemp = this.StackTemp,
                    StackVelocity = this.StackVelocity,
                    StackDiameter = this.StackDiameter
                };
            }
            catch (Exception ex)
            {
                ShowError($"Помилка у вхідних даних: {ex.Message}");
                IsSimulationRunning = false;
                CommandManager.InvalidateRequerySuggested();
                return;
            }

            var progress = new Progress<string>(status => StatusText = status);

            try
            {
                _simulationResults = await _aermodService.RunSimulationAsync(_selectedSourcePoint, sourceParams, progress);

                OnLayerSelectionChanged();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                StatusText = "Помилка симуляції.";
            }
            finally
            {
                IsSimulationRunning = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public void SetSourcePoint(MPoint mapPoint)
        {
            _selectedSourcePoint = mapPoint;

            var (lon, lat) = SphericalMercator.ToLonLat(mapPoint.X, mapPoint.Y);
            var ntsPoint = new NetTopologySuite.Geometries.Point(mapPoint.X, mapPoint.Y);
            var feature = new GeometryFeature { Geometry = ntsPoint };

            _sourcePointFeatures.Clear();
            _sourcePointFeatures.Add(feature);

            _sourcePointLayer.DataHasChanged();

            StatusText = $"Вибрано нове джерело. Можна запускати розрахунок.";
            MessageBox.Show($"Вибрано нове джерело викидів:\n" +
                            $"Довгота: {lon:F6}\n" +
                            $"Широта: {lat:F6}\n" +
                            $"(Координати карти: X={mapPoint.X:F0}, Y={mapPoint.Y:F0})");

            CommandManager.InvalidateRequerySuggested();
        }

        public void UpdateMouseCoordinates(MPoint worldPos)
        {
            if (worldPos != null)
            {
                CoordsText = $"X: {worldPos.X:0.0000}   Y: {worldPos.Y:0.0000}";
            }
            else
            {
                CoordsText = "X: ---   Y: ---";
            }
        }

        public async Task InitializeMapAsync()
        {
            var (highFeatures, lowFeatures) = await Task.Run(() =>
            {
                var highList = new List<IFeature>();
                var lowList = new List<IFeature>();

                var buildings = BuildingLoader.LoadBuildings("Data/buildings.geojson");

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

            Map?.Layers.Add(lowLayer);
            Map?.Layers.Add(highLayer);

            var map = new Map { CRS = "EPSG:3857", BackColor = Color.Black };
            map.Layers.Add(OpenStreetMap.CreateTileLayer());
            map.Layers.Add(lowLayer);
            map.Layers.Add(highLayer);

            map.Layers.Add(_sourcePointLayer);

            Map = map;

            const double lon = 25.5948, lat = 49.5535;
            var (x, y) = SphericalMercator.FromLonLat(lon, lat);
            Map.Navigator.CenterOn(new MPoint(x, y));
            Map.Navigator.ZoomTo(2000);

            if (Map == null)
            {
                Map.Refresh();
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
            if (b.HeightMeters > 1.0) highList.Add(feature);
            else lowList.Add(feature);
        }

        private static IStyle CreateSourceMarkerStyle()
        {
            return new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                Fill = new Brush(Color.Red),
                Outline = new Pen(Color.Black, 2),
                SymbolScale = 0.8,
                Opacity = 0.8f
            };
        }

        private void OnLayerSelectionChanged()
        {
            if (Map == null || _simulationResults == null) return;
            string selectedKey = "";
            switch (SelectedLayerOption)
            {
                case "1-годинний максимум": selectedKey = "1-HR"; break;
                case "3-годинний максимум": selectedKey = "3-HR"; break;
                case "24-годинний максимум": selectedKey = "24-HR"; break;
                case "Середнє за період": selectedKey = "PERIOD"; break;
                default: selectedKey = ""; break;
            }

            List<DispersionDataPoint> dataPoints = null;
            if (!string.IsNullOrEmpty(selectedKey) && _simulationResults.ContainsKey(selectedKey))
            {
                dataPoints = _simulationResults[selectedKey];
            }
            UpdateHeatmapLayer(dataPoints);
        }

        private void UpdateHeatmapLayer(List<DispersionDataPoint> dataPoints)
        {
            if (_resultHeatmapLayer != null)
            {
                Map.Layers.Remove(_resultHeatmapLayer);
                _resultHeatmapLayer = null;
            }

            if (dataPoints == null || !dataPoints.Any() || _selectedSourcePoint == null)
            {
                Map.RefreshGraphics();
                Debug.WriteLine("Дані для шару відсутні або не вибрано джерело.");
                return;
            }

            Debug.WriteLine($"Оновлення шару з {dataPoints.Count} точками.");

            ICoordinateTransformation transformation = null;
            double minX_Source = 0, minY_Source = 0, sourceWidth = 1, sourceHeight = 1;
            double maxIntensity = 1;

            try
            {
                var csFactory = new CoordinateSystemFactory();
                var ctFactory = new CoordinateTransformationFactory();
                var sourceCS = csFactory.CreateFromWkt(@"PROJCS[""WGS 84 / UTM zone 35N"", GEOGCS[""WGS 84"", DATUM[""WGS_1984"", SPHEROID[""WGS 84"",6378137,298.257223563]], PRIMEM[""Greenwich"",0], UNIT[""degree"",0.0174532925199433]], PROJECTION[""Transverse_Mercator""], PARAMETER[""latitude_of_origin"",0], PARAMETER[""central_meridian"",27], PARAMETER[""scale_factor"",0.9996], PARAMETER[""false_easting"",500000], PARAMETER[""false_northing"",0], UNIT[""metre"",1], AUTHORITY[""EPSG"",""32635""]]");
                var targetCS = csFactory.CreateFromWkt(@"PROJCS[""WGS 84 / Pseudo-Mercator"", GEOGCS[""WGS 84"", DATUM[""WGS_1984"", SPHEROID[""WGS 84"",6378137,298.257223563]], PRIMEM[""Greenwich"",0], UNIT[""degree"",0.0174532925199433]], PROJECTION[""Mercator_1SP""], PARAMETER[""central_meridian"",0], PARAMETER[""scale_factor"",1], PARAMETER[""false_easting"",0], PARAMETER[""false_northing"",0], UNIT[""metre"",1], AUTHORITY[""EPSG"",""3857""]]");
                transformation = ctFactory.CreateFromCoordinateSystems(sourceCS, targetCS);

                if (transformation == null) throw new Exception("Не вдалося створити трансформацію.");

                minX_Source = dataPoints.Min(p => p.Point.X);
                minY_Source = dataPoints.Min(p => p.Point.Y);
                var maxX_Source = dataPoints.Max(p => p.Point.X);
                var maxY_Source = dataPoints.Max(p => p.Point.Y);
                var sourceCenterX = (minX_Source + maxX_Source) / 2.0;
                var sourceCenterY = (minY_Source + maxY_Source) / 2.0;
                sourceWidth = maxX_Source - minX_Source;
                sourceHeight = maxY_Source - minY_Source;
                maxIntensity = dataPoints.Max(p => p.Concentration);

                if (sourceWidth <= 0) sourceWidth = 1;
                if (sourceHeight <= 0) sourceHeight = 1;
                if (maxIntensity <= 0) maxIntensity = 1;

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Помилка підготовки до трансформації: {ex.Message}");
                MessageBox.Show($"Помилка підготовки координат: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                Map.RefreshGraphics(); return;
            }

            var features = new List<IFeature>();
            MPoint sourcePoint_Proj = _selectedSourcePoint;

            try
            {
                foreach (var dp in dataPoints)
                {
                    double relativeX = (dp.Point.X - minX_Source) / sourceWidth - 0.5;
                    double relativeY = (dp.Point.Y - minY_Source) / sourceHeight - 0.5;

                    double targetX = sourcePoint_Proj.X + relativeX * 20000.0;
                    double targetY = sourcePoint_Proj.Y + relativeY * 20000.0;

                    var pointGeometry = new NetTopologySuite.Geometries.Point(targetX, targetY);

                    var feature = new GeometryFeature(pointGeometry);
                    feature["concentration"] = dp.Concentration;
                    features.Add(feature);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Помилка створення фіч: {ex.Message}");
                MessageBox.Show($"Помилка обробки точок: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                Map.RefreshGraphics(); return;
            }

            try
            {
                var provider = new MemoryProvider(features);

                var vectorLayer = new Layer("Result Points")
                {
                    DataSource = provider,
                    Style = CreateConcentrationStyle(maxIntensity),
                    Opacity = 0.8
                };

                _resultHeatmapLayer = vectorLayer;
                Map.Layers.Add(_resultHeatmapLayer);
                Debug.WriteLine($"Додано Векторний Шар '{_resultHeatmapLayer.Name}' на карту.");
                Map.RefreshGraphics();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Помилка створення Векторного Шару: {ex.Message}");
                MessageBox.Show($"Помилка відображення шару: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                Map.RefreshGraphics();
            }
        }

        private static IStyle CreateConcentrationStyle(double maxConcentration)
        {
            Color GetColor(double concentration)
            {
                if (maxConcentration <= 0) return Color.Blue;
                float intensity = Math.Clamp((float)(concentration / maxConcentration), 0f, 1f);
                float hue = (1.0f - intensity) * 240f;
                var skColor = SKColor.FromHsl(hue, 100, 50);
                return new Color(skColor.Red, skColor.Green, skColor.Blue, 180);
            }

            double GetSize(double concentration)
            {
                if (maxConcentration <= 0) return 4;
                float intensity = Math.Clamp((float)(concentration / maxConcentration), 0f, 1f);
                return 4 + intensity * 11;
            }

            return new ThemeStyle(f =>
            {
                double conc = 0;

                try
                {
                    if (f?.Fields != null && f.Fields.Contains("concentration"))
                    {
                        var val = f["concentration"];
                        if (val != null)
                            conc = Convert.ToDouble(val);
                    }
                }
                catch { /* ignore */ }

                return new SymbolStyle
                {
                    SymbolType = SymbolType.Ellipse,
                    Fill = new Brush(GetColor(conc)),
                    Outline = null,
                    SymbolScale = GetSize(conc) / 10.0
                };
            });
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}