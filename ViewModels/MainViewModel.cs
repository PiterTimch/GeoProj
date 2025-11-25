using BruTile;
using BruTile.Web;
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
using System.Windows.Input;
using System.Windows;
using System.Diagnostics;
using System.Text;
using System.Collections.ObjectModel;
using SkiaSharp;
using GeoProj.Models;
using BruTile.Predefined;
using Mapsui.Tiling.Layers;
using System.Windows.Media;
using Color = Mapsui.Styles.Color;
using Brush = Mapsui.Styles.Brush;
using Pen = Mapsui.Styles.Pen;
using Point = NetTopologySuite.Geometries.Point;
using System.Collections.Specialized;

namespace GeoProj.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        #region Properties

        private bool _isPopupVisible;
        public bool IsPopupVisible
        {
            get => _isPopupVisible;
            set => SetProperty(ref _isPopupVisible, value);
        }

        private string _popupTitle;
        public string PopupTitle
        {
            get => _popupTitle;
            set => SetProperty(ref _popupTitle, value);
        }

        private string _popupContent;
        public string PopupContent
        {
            get => _popupContent;
            set => SetProperty(ref _popupContent, value);
        }

        public ObservableCollection<AermodSource> Sources { get; } = new ObservableCollection<AermodSource>();
        private AermodSource _selectedSource;
        public AermodSource SelectedSource
        {
            get => _selectedSource;
            set
            {
                if (SetProperty(ref _selectedSource, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
        private ILayer _sourcePointLayer;
        private readonly ObservableCollection<IFeature> _sourcePointFeatures = new ObservableCollection<IFeature>();
        private MPoint _selectedSourcePoint;

        private ILayer _resultHeatmapLayer;

        private Dictionary<string, List<DispersionDataPoint>> _simulationResults = new Dictionary<string, List<DispersionDataPoint>>();

        public List<string> LayerOptions { get; } = new List<string>
        {
            "Не показувати",
            "Середнє за 1 годину",
            "Середнє за 3 години",
            "Середнє за 24 години",
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

        private string _minLegendValue = "Мало";
        public string MinLegendValue
        {
            get => _minLegendValue;
            set => SetProperty(ref _minLegendValue, value);
        }

        private string _maxLegendValue = "Багато";
        public string MaxLegendValue
        {
            get => _maxLegendValue;
            set => SetProperty(ref _maxLegendValue, value);
        }

        private bool _isHeatmapVisible = false;
        public bool IsHeatmapVisible
        {
            get => _isHeatmapVisible;
            set => SetProperty(ref _isHeatmapVisible, value);
        }

        private string _statusText = "Готовий. Оберіть точку на карті.";
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        private string _coordsText = "X: ---   Y: ---";
        public string CoordsText { get => _coordsText; set => SetProperty(ref _coordsText, value); }

        private bool _isSimulationRunning = false;
        public bool IsSimulationRunning { get => _isSimulationRunning; set => SetProperty(ref _isSimulationRunning, value); }

        private readonly IAermodService _aermodService;

        private Map _map;
        public Map Map
        {
            get => _map;
            set => SetProperty(ref _map, value);
        }

        private ILayer _baseMapLayer;

        private bool _isDarkTheme;
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (SetProperty(ref _isDarkTheme, value))
                {
                    OnThemeChanged();
                }
            }
        }

        private ReceptorMode _selectedReceptorMode = ReceptorMode.Grid;
        public ReceptorMode SelectedReceptorMode
        {
            get => _selectedReceptorMode;
            set
            {
                if (SetProperty(ref _selectedReceptorMode, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                    if (value == ReceptorMode.Discrete)
                        StatusText = "Режим власних точок. (Alt + ПКМ) для додавання рецептора.";
                    else
                        StatusText = "Режим автоматичної сітки. Оберіть джерело (ЛКМ).";
                }
            }
        }

        public ObservableCollection<MPoint> CustomReceptors { get; } = new ObservableCollection<MPoint>();

        private ILayer _customReceptorsLayer;
        private readonly List<IFeature> _customReceptorFeatures = new List<IFeature>();
        #endregion

        #region Comands
        public ICommand RunSimulationCommand { get; }
        public ICommand ClearCustomReceptorsCommand { get; }
        public ICommand RemoveSourceCommand { get; }
        #endregion

        public MainViewModel(IAermodService aermodService)
        {
            _aermodService = aermodService;

            RunSimulationCommand = new RelayCommand(async (_) => await RunSimulationAsync(), (_) => CanRunSimulation());
            ClearCustomReceptorsCommand = new RelayCommand( _ => ClearCustomReceptors(), _ => SelectedReceptorMode == ReceptorMode.Discrete && CustomReceptors.Count > 0);
            RemoveSourceCommand = new RelayCommand( _ => RemoveSelectedSource(), (_) => SelectedSource != null);

            _sourcePointLayer = new MemoryLayer
            {
                Name = "Source Point",
                Features = _sourcePointFeatures,
                Style = CreateSourceMarkerStyle(),
                IsMapInfoLayer = true 
            };

            _customReceptorsLayer = new MemoryLayer
            {
                Name = "Custom Receptors",
                Features = _customReceptorFeatures,
                Style = CreateCustomReceptorStyle(),
                IsMapInfoLayer = true
            };

            Sources.CollectionChanged += OnSourcesChanged;
            CustomReceptors.CollectionChanged += OnCustomReceptorsChanged;

            SetupSources();
        }

        private void SetupSources()
        {
            AddSource(new MPoint { X = 2848870, Y = 6368740 }); // вул. Багата, 4
            AddSource(new MPoint { X = 2850161, Y = 6364040 }); // вул. Микулинецька, 64а
            AddSource(new MPoint { X = 2843962, Y = 6374778 }); // вул. Мирна, 43а
            AddSource(new MPoint { X = 2849584, Y = 6368341 }); // бульв. Данила Галицького, 4а
            AddSource(new MPoint { X = 2849570, Y = 6369615 }); // вул. Франка, 16
            AddSource(new MPoint { X = 2853854, Y = 6372832 }); // вул. Леся Курбаса, 3а
            AddSource(new MPoint { X = 2850970, Y = 6361941 }); // вул. Шкільна, 5
            AddSource(new MPoint { X = 2852123, Y = 6368996 }); // просп. Степана Бандери, 47в
            AddSource(new MPoint { X = 2844841, Y = 6370977 }); // вул. Петра Батьківського, 46б
            AddSource(new MPoint { X = 2849127, Y = 6371000 }); // вул. Транспортна, 7б
            AddSource(new MPoint { X = 2848153, Y = 6367130 }); // пров. Цегельний, 1а
            AddSource(new MPoint { X = 2849441, Y = 6367998 }); // вул. Торговиця, 12
            AddSource(new MPoint { X = 2849460, Y = 6368017 }); // вул. Торговиця, 12
            AddSource(new MPoint { X = 2851923, Y = 6369632 }); // вул. Лемківська, 23
            AddSource(new MPoint { X = 2844956, Y = 6368002 }); // вул. Тролейбусна, 7б
            AddSource(new MPoint { X = 2849457, Y = 6366382 }); // вул. Чернівецька, 25а
            AddSource(new MPoint { X = 2854742, Y = 6368985 }); // вул. Купчинського, 14а
            AddSource(new MPoint { X = 2850193, Y = 6371337 }); // вул. Лозовецька, 3а
            AddSource(new MPoint { X = 2845855, Y = 6367616 }); // бульв. Просвіти, 9
            AddSource(new MPoint { X = 2845905, Y = 6367603 }); // бульв. Просвіти, 9
            AddSource(new MPoint { X = 2850564, Y = 6368870 }); // просп. Степана Бандери, 14в
        }

        public void ShowFeatureInfo(IFeature feature)
        {
            if (feature == null) { HideInfo(); return; }

            if (feature.Fields.Contains("concentration"))
            {
                if (feature is GeometryFeature concFeature && concFeature.Geometry is Point concPoint)
                {
                    try
                    {
                        double conc = Convert.ToDouble(feature["concentration"]);
                        PopupTitle = "Дані Результату";
                        PopupContent = $"X: {concPoint.X:F0}\nY: {concPoint.Y:F0}\nКонц.: {conc:G4} µg/m³";
                        IsPopupVisible = true;
                    }
                    catch (Exception ex) { Debug.WriteLine($"Error showing receptor info: {ex.Message}"); HideInfo(); }
                }
            }
            else if (feature.Fields.Contains("receptor_id"))
            {
                if (feature is GeometryFeature recFeature && recFeature.Geometry is Point recPoint)
                {
                    PopupTitle = "Власний Рецептор";
                    PopupContent = $"ID: {feature["receptor_id"]}\nX: {recPoint.X:F0}\nY: {recPoint.Y:F0}";
                    IsPopupVisible = true;
                }
            }
            else if (feature.Fields.Contains("source_id"))
            {
                if (feature is GeometryFeature srcFeature && srcFeature.Geometry is Point srcPoint)
                {
                    PopupTitle = "Джерело Забруднення";
                    PopupContent = $"ID: {feature["source_id"]}\nX: {srcPoint.X:F0}\nY: {srcPoint.Y:F0}";
                    IsPopupVisible = true;
                }
            }
            else if (feature.Fields.Contains("height_m"))
            {
                ShowBuildingInfo(feature);
            }
            else
            {
                HideInfo();
            }
        }

        public void ShowBuildingInfo(IFeature feature)
        {
            if (feature == null)
            {
                HideInfo();
                return;
            }

            try
            {
                PopupTitle = "Інформація про будівлю";

                var sb = new StringBuilder();
                sb.AppendLine($"Висота: {feature["height_m"]?.ToString() ?? "-"} м");
                sb.AppendLine($"Адреса: {feature["addr:full"]?.ToString() ?? feature["addr:street"]?.ToString() ?? "-"}");
                sb.AppendLine($"Призначення: {feature["building"]?.ToString() ?? "-"}");

                PopupContent = sb.ToString().Trim();
                IsPopupVisible = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing building info: {ex.Message}");
                HideInfo();
            }
        }

        public void HideInfo()
        {

            if (IsPopupVisible)
            {
                IsPopupVisible = false;
            }
        }

        private bool CanRunSimulation()
        {
            if (IsSimulationRunning || Sources.Count == 0)
                return false;

            if (SelectedReceptorMode == ReceptorMode.Grid && SelectedSource == null)
            {
                StatusText = "Помилка: в режимі 'Сітка' потрібно вибрати джерело зі списку.";
                return false;
            }

            if (SelectedReceptorMode == ReceptorMode.Discrete && CustomReceptors.Count == 0)
            {
                StatusText = "Помилка: в режимі 'Точки' потрібно додати хоча б один рецептор (Alt+ПКМ).";
                return false;
            }

            return true;
        }

        private async Task RunSimulationAsync()
        {
            if (!CanRunSimulation()) return;

            IsSimulationRunning = true;
            CommandManager.InvalidateRequerySuggested();
            ClearResults();

            var receptorSettings = new ReceptorSettings
            {
                Mode = this.SelectedReceptorMode,
                Points = this.CustomReceptors.ToList(),
                GridOrigin = (SelectedReceptorMode == ReceptorMode.Grid) ? SelectedSource.Point : null
            };

            var progress = new Progress<string>(status => StatusText = status);

            try
            {
                _simulationResults = await _aermodService.RunSimulationAsync(Sources.ToList(), receptorSettings, progress);
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

        public void AddSource(MPoint mapPoint)
        {
            var newSource = new AermodSource(mapPoint);
            Sources.Add(newSource);
            SelectedSource = newSource;
            ClearResults();
        }

        private void RemoveSelectedSource()
        {
            if (SelectedSource != null)
            {
                Sources.Remove(SelectedSource);
                SelectedSource = null;
                ClearResults();
            }
        }

        public void SelectSourceByMapInfo(MapInfo mapInfo)
        {
            if (mapInfo?.Feature == null || !mapInfo.Feature.Fields.Contains("source_id"))
            {
                SelectedSource = null;
                return;
            }

            string sourceId = mapInfo.Feature["source_id"].ToString();
            SelectedSource = Sources.FirstOrDefault(s => s.SourceId == sourceId);
        }

        public void AddCustomReceptor(MPoint mapPoint)
        {
            if (SelectedReceptorMode != ReceptorMode.Discrete)
            {
                StatusText = "Помилка: додавати точки можна лише в режимі 'Власні точки'.";
                return;
            }
            CustomReceptors.Add(mapPoint);
            CommandManager.InvalidateRequerySuggested();
        }

        private void ClearCustomReceptors()
        {
            CustomReceptors.Clear();
            CommandManager.InvalidateRequerySuggested();
        }

        private void ClearResults()
        {
            if (_resultHeatmapLayer != null)
            {
                Map.Layers.Remove(_resultHeatmapLayer);
                _resultHeatmapLayer = null;
            }
            _simulationResults?.Clear();
            SelectedLayerOption = "Не показувати";
            IsHeatmapVisible = false;
            MinLegendValue = "Мало";
            MaxLegendValue = "Багато";
            Map?.RefreshGraphics();
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

        private void OnSourcesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _sourcePointFeatures.Clear();
            foreach (var source in Sources)
            {
                var ntsPoint = new Point(source.Point.X, source.Point.Y);
                var feature = new GeometryFeature { Geometry = ntsPoint };
                feature["source_id"] = source.SourceId;
                _sourcePointFeatures.Add(feature);
            }
            _sourcePointLayer.DataHasChanged();
        }

        private void OnCustomReceptorsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _customReceptorFeatures.Clear();
            int i = 1;
            foreach (var point in CustomReceptors)
            {
                var ntsPoint = new Point(point.X, point.Y);
                var feature = new GeometryFeature { Geometry = ntsPoint };
                feature["receptor_id"] = $"R{i++:D2}";
                _customReceptorFeatures.Add(feature);
            }
            _customReceptorsLayer.DataHasChanged();
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
                    NetTopologySuite.Geometries.Geometry geom = b.Polygon;
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

            var highLayer = new Layer("Buildings_High") { DataSource = highProvider, Style = highStyle, IsMapInfoLayer = true };
            var lowLayer = new Layer("Buildings_Low") { DataSource = lowProvider, Style = lowStyle, IsMapInfoLayer = true };

            _baseMapLayer = OpenStreetMap.CreateTileLayer();

            var map = new Map { CRS = "EPSG:3857" };
            map.Layers.Add(_baseMapLayer);
            map.Layers.Add(lowLayer);
            map.Layers.Add(highLayer);
            map.Layers.Add(_sourcePointLayer);
            map.Layers.Add(_customReceptorsLayer);

            Map = map;

            IsDarkTheme = false;
            OnThemeChanged();

            const double lon = 25.5948, lat = 49.5535;
            var (x, y) = SphericalMercator.FromLonLat(lon, lat);
            Map.Navigator.CenterOn(new MPoint(x, y));
            Map.Navigator.ZoomTo(2000);

            if (Map != null)
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

        private static IStyle CreateCustomReceptorStyle()
        {
            return new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                Fill = new Brush(new Color(0, 150, 255, 200)),
                Outline = new Pen(Color.White, 2),
                SymbolScale = 0.6,
                Opacity = 0.9f
            };
        }

        private void OnLayerSelectionChanged()
        {
            if (Map == null) return;

            if (_simulationResults == null)
            {
                UpdateHeatmapLayer(null);
                UpdateLegend(null);
                return;
            }

            string selectedKey = "";
            switch (SelectedLayerOption)
            {
                case "Середнє за 1 годину": selectedKey = "1-HR"; break;
                case "Середнє за 3 години": selectedKey = "3-HR"; break;
                case "Середнє за 24 години": selectedKey = "24-HR"; break;
                case "Середнє за період": selectedKey = "PERIOD"; break;
                default: selectedKey = ""; break;
            }

            List<DispersionDataPoint> dataPoints = null;
            if (!string.IsNullOrEmpty(selectedKey) && _simulationResults.ContainsKey(selectedKey))
            {
                dataPoints = _simulationResults[selectedKey];
            }

            UpdateLegend(dataPoints);
            UpdateHeatmapLayer(dataPoints);
        }

        private void UpdateLegend(List<DispersionDataPoint> dataPoints)
        {
            if (dataPoints == null || !dataPoints.Any())
            {
                MinLegendValue = "Мало";
                MaxLegendValue = "Багато";
                return;
            }

            double min = dataPoints.Min(d => d.Concentration);
            double max = dataPoints.Max(d => d.Concentration);

            MinLegendValue = min.ToString("G3");
            MaxLegendValue = max.ToString("G3");
        }

        private void UpdateHeatmapLayer(List<DispersionDataPoint> dataPoints)
        {
            if (_resultHeatmapLayer != null)
            {
                Map.Layers.Remove(_resultHeatmapLayer);
                _resultHeatmapLayer = null;
            }

            IsHeatmapVisible = false;

            if (dataPoints == null || !dataPoints.Any())
            {
                Map.RefreshGraphics();
                Debug.WriteLine("Дані для шару відсутні.");
                return;
            }

            Debug.WriteLine($"Оновлення шару з {dataPoints.Count} точками.");

            var features = new List<IFeature>();
            double maxIntensity = 0;

            try
            {
                foreach (var dp in dataPoints)
                {
                    var pointGeometry = new Point(dp.Point.X, dp.Point.Y);

                    var feature = new GeometryFeature(pointGeometry);
                    feature["concentration"] = dp.Concentration;
                    features.Add(feature);
                }

                maxIntensity = dataPoints.Max(p => p.Concentration);
                if (maxIntensity <= 0) maxIntensity = 1.0;
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
                    Opacity = 0.8,
                    IsMapInfoLayer = true
                };

                _resultHeatmapLayer = vectorLayer;
                Map.Layers.Add(_resultHeatmapLayer);
                Debug.WriteLine($"Додано Векторний Шар '{_resultHeatmapLayer.Name}' на карту.");
                Map.RefreshGraphics();
                IsHeatmapVisible = true;
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
                    Outline = new Pen(Color.Black, 2),
                    SymbolScale = GetSize(conc) / 10.0
                };
            });
        }

        private void OnThemeChanged()
        {
            UpdateAppResources();

            if (Map == null || _baseMapLayer == null) return;

            Map.Layers.Remove(_baseMapLayer);

            if (IsDarkTheme)
            {
                var darkThemeUrl = "https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{@2x}.png";
                var darkAttribution = new Attribution("© OpenStreetMap contributors, © CARTO", "https://carto.com/attributions");
                var darkTileSource = new HttpTileSource(
                    new GlobalSphericalMercator(0, 18),
                    darkThemeUrl,
                    new[] { "a", "b", "c", "d" },
                    name: "CartoDark",
                    attribution: darkAttribution,
                    userAgent: "GeoProj-App"
                );
                _baseMapLayer = new TileLayer(darkTileSource);
                Map.BackColor = Color.Black;
            }
            else
            {
                _baseMapLayer = OpenStreetMap.CreateTileLayer();
                Map.BackColor = new Color(240, 240, 240);
            }

            Map.Layers.Insert(0, _baseMapLayer);
        }

        private void UpdateAppResources()
        {
            var resources = Application.Current.Resources;

            if (IsDarkTheme)
            {
                resources["AppWindowBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x30));
                resources["PanelBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xAA, 0x22, 0x22, 0x22));
                resources["PopupBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33));
                resources["ButtonBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55));
                resources["TextBoxBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));

                resources["AppTextBrush"] = new SolidColorBrush(Colors.White);
                resources["PanelHeaderTextBrush"] = new SolidColorBrush(Colors.White);
                resources["PanelSubTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xCC, 0xCC));

                resources["PanelBorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x77, 0x77, 0x77));
            }
            else
            {
                resources["AppWindowBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF0, 0xF0, 0xF0));
                resources["PanelBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF));
                resources["PopupBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF9, 0xF9, 0xF9));
                resources["ButtonBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDD, 0xDD, 0xDD));
                resources["TextBoxBackgroundBrush"] = new SolidColorBrush(Colors.White);

                resources["AppTextBrush"] = new SolidColorBrush(Colors.Black);
                resources["PanelHeaderTextBrush"] = new SolidColorBrush(Colors.Black);
                resources["PanelSubTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2C, 0x3E, 0x50));

                resources["PanelBorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xB0, 0xB0, 0xB0));
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}