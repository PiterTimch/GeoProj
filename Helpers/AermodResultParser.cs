using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using GeoProj.Models;
using Mapsui;

namespace GeoProj.Helpers
{
    public static class AermodResultParser
    {
        private static readonly Regex MaxTableRegex = new Regex(
            @"^\s*\d+\.\s+([\d\.\-]+)m?\s*\([\d]+\)\s+AT\s+\(\s*([\d\.\-]+),\s*([\d\.\-]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SourceOriginRegex = new Regex(
            @"X-ORIG\s*=\s*([\d\.\-]+)\s*;\s*Y-ORIG\s*=\s*([\d\.\-]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static MPoint _sourceOrigin;
        private static readonly List<double> _periodDistances = new List<double>();

        public static Dictionary<string, List<DispersionDataPoint>> ParseAermodOutFile(string filePath)
        {
            var results = new Dictionary<string, List<DispersionDataPoint>>
            {
                ["1-HR"] = new List<DispersionDataPoint>(),
                ["3-HR"] = new List<DispersionDataPoint>(),
                ["24-HR"] = new List<DispersionDataPoint>(),
                ["PERIOD"] = new List<DispersionDataPoint>()
            };

            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"Файл результатів не знайдено: {filePath}");
                return results;
            }

            var lines = File.ReadAllLines(filePath);
            var ci = CultureInfo.InvariantCulture;

            _sourceOrigin = null;
            _periodDistances.Clear();

            string currentPeriodKey = null;
            bool isParsingMaxTable = false;
            bool isParsingPeriodMatrix = false;
            bool findPeriodDistancesNext = false;
            int headerLinesToSkip = 0;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];

                if (_sourceOrigin == null && line.Contains("ORIGIN FOR POLAR NETWORK"))
                {
                    string searchLine = line;
                    if (!line.Contains("X-ORIG"))
                    {
                        if (lineIndex + 1 < lines.Length) searchLine = lines[lineIndex + 1];
                    }

                    var match = SourceOriginRegex.Match(searchLine);
                    if (match.Success)
                    {
                        double x = double.Parse(match.Groups[1].Value, ci);
                        double y = double.Parse(match.Groups[2].Value, ci);
                        _sourceOrigin = new MPoint(x, y);
                        Debug.WriteLine($"--- Знайдено координати джерела: X={_sourceOrigin.X}, Y={_sourceOrigin.Y} ---");
                    }
                    continue;
                }

                if (line.Contains("*** THE MAXIMUM"))
                {
                    isParsingPeriodMatrix = false;
                    isParsingMaxTable = false;
                    currentPeriodKey = null;

                    if (line.Contains("1-HR AVERAGE")) currentPeriodKey = "1-HR";
                    else if (line.Contains("3-HR AVERAGE")) currentPeriodKey = "3-HR";
                    else if (line.Contains("24-HR AVERAGE")) currentPeriodKey = "24-HR";

                    if (currentPeriodKey != null)
                    {
                        Debug.WriteLine($"--- Знайдено таблицю MAX для: {currentPeriodKey} ---");
                        isParsingMaxTable = true;
                        headerLinesToSkip = 5;
                    }
                    continue;
                }

                if (!isParsingMaxTable && !isParsingPeriodMatrix &&
                    line.Contains("*** THE PERIOD") && line.Contains("AVERAGE CONCENTRATION") && !line.Contains("SUMMARY"))
                {
                    Debug.WriteLine($"--- Знайдено таблицю PERIOD ---");
                    currentPeriodKey = "PERIOD";
                    isParsingPeriodMatrix = true;
                    _periodDistances.Clear();
                    findPeriodDistancesNext = false;
                    continue;
                }


                if (isParsingMaxTable && currentPeriodKey != null)
                {
                    if (headerLinesToSkip > 0)
                    {
                        Debug.WriteLine($"Пропускаємо рядок заголовка MAXTABLE: {line}");
                        headerLinesToSkip--;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(line) || line.Contains("***"))
                    {
                        isParsingMaxTable = false;
                        continue;
                    }

                    var matches = MaxTableRegex.Matches(line);
                    foreach (Match match in matches)
                    {
                        if (match.Success && match.Groups.Count >= 4)
                        {
                            try
                            {
                                double conc = double.Parse(match.Groups[1].Value, ci);
                                double x = double.Parse(match.Groups[2].Value, ci);
                                double y = double.Parse(match.Groups[3].Value, ci);
                                if (conc > 0)
                                {
                                    results[currentPeriodKey].Add(new DispersionDataPoint(new MPoint(x, y), conc));
                                    Debug.WriteLine($"Додано точку MAX: X={x}, Y={y}, Conc={conc} для {currentPeriodKey}");
                                }
                            }
                            catch { /* ignore */ }
                        }
                    }
                    continue;
                }

                if (isParsingPeriodMatrix && currentPeriodKey == "PERIOD")
                {
                    if (line.Contains("DISTANCE (METERS)"))
                    {
                        findPeriodDistancesNext = true;
                        continue;
                    }

                    if (findPeriodDistancesNext && !line.Contains("DEGREES") && !string.IsNullOrWhiteSpace(line))
                    {
                        var dists = line.Trim().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var d in dists)
                        {
                            if (double.TryParse(d, NumberStyles.Float, ci, out double distVal))
                                _periodDistances.Add(distVal);
                        }
                        Debug.WriteLine($"Знайдено {_periodDistances.Count} дистанцій.");
                        findPeriodDistancesNext = false;
                        continue;
                    }

                    if (_periodDistances.Count > 0 && char.IsDigit(line.TrimStart().FirstOrDefault()))
                    {
                        var parts = line.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2) continue;

                        double directionDeg = double.Parse(parts[0].Trim(), ci);
                        var concs = parts[1].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        for (int i = 0; i < concs.Length; i++)
                        {
                            if (i >= _periodDistances.Count) break;

                            if (!double.TryParse(concs[i], NumberStyles.Float, ci, out double concentration) || concentration <= 0)
                                continue;

                            double distance = _periodDistances[i];

                            if (_sourceOrigin == null)
                            {
                                Debug.WriteLine("ПОМИЛКА: Не знайдено координати джерела (X-ORIG, Y-ORIG)!");
                                isParsingPeriodMatrix = false;
                                break;
                            }

                            var (x, y) = CalculateXY(_sourceOrigin, directionDeg, distance);
                            results["PERIOD"].Add(new DispersionDataPoint(new MPoint(x, y), concentration));
                            Debug.WriteLine($"Додано точку PERIOD: X={x}, Y={y}, Conc={concentration}");
                        }
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(line) || line.Contains("***"))
                    {
                        if (line.Contains("***"))
                        {
                            isParsingPeriodMatrix = false;
                        }
                    }
                }
            }

            foreach (var kvp in results)
            {
                Debug.WriteLine($"Знайдено {kvp.Value.Count} точок для періоду {kvp.Key}");
            }

            return results;
        }

        private static (double X, double Y) CalculateXY(MPoint origin, double directionDeg, double distance)
        {
            double angleDeg = 90.0 - directionDeg;
            if (angleDeg <= 0) angleDeg += 360.0;

            double angleRad = angleDeg * (Math.PI / 180.0);

            double x = origin.X + distance * Math.Cos(angleRad);
            double y = origin.Y + distance * Math.Sin(angleRad);

            return (x, y);
        }
    }
}