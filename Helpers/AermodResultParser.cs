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
        public static Dictionary<string, List<DispersionDataPoint>> ParseAermodOutFile(string filePath, ReceptorMode mode)
        {
            Debug.WriteLine("======================================================");
            Debug.WriteLine($"========== ЗАПУСК ПАРСERA (Режим: {mode}) ===========");
            Debug.WriteLine("======================================================");

            var results = new Dictionary<string, List<DispersionDataPoint>>
            {
                ["1-HR"] = new List<DispersionDataPoint>(),
                ["3-HR"] = new List<DispersionDataPoint>(),
                ["24-HR"] = new List<DispersionDataPoint>(),
                ["PERIOD"] = new List<DispersionDataPoint>()
            };

            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"[DEBUG] Файл результатів не знайдено: {filePath}");
                return results;
            }

            var lines = File.ReadAllLines(filePath);
            var ci = CultureInfo.InvariantCulture;

            if (mode == ReceptorMode.Grid)
            {
                ParseGridModeOutput(lines, ci, results);
            }
            else
            {
                ParseDiscreteModeOutput(lines, ci, results);
            }

            Debug.WriteLine("=====================================");
            Debug.WriteLine("========= ПАРСИНГ ЗАВЕРШЕНО =========");
            foreach (var kvp in results)
            {
                Debug.WriteLine($"Знайдено {kvp.Value.Count} точок для періоду {kvp.Key}");
            }
            Debug.WriteLine("=====================================");

            return results;
        }

        #region Discrete Mode Parser

        private static void ParseDiscreteModeOutput(string[] lines, CultureInfo ci, Dictionary<string, List<DispersionDataPoint>> results)
        {
            string currentPeriodKey = null;
            bool isParsingTable = false;
            int dataStep = 0;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                bool isPeriodTable = line.Contains("*** THE PERIOD") && line.Contains("AVERAGE CONCENTRATION");
                bool is1stHighestTable = line.Contains("1ST HIGHEST") && line.Contains("AVERAGE CONCENTRATION");

                if (isPeriodTable || is1stHighestTable)
                {
                    currentPeriodKey = null;
                    isParsingTable = false;

                    if (isPeriodTable)
                    {
                        currentPeriodKey = "PERIOD";
                        dataStep = 3;
                    }
                    else if (line.Contains("1-HR AVERAGE"))
                    {
                        currentPeriodKey = "1-HR";
                        dataStep = 4;
                    }
                    else if (line.Contains("3-HR AVERAGE"))
                    {
                        currentPeriodKey = "3-HR";
                        dataStep = 4;
                    }
                    else if (line.Contains("24-HR AVERAGE"))
                    {
                        currentPeriodKey = "24-HR";
                        dataStep = 4;
                    }

                    if (currentPeriodKey != null)
                    {
                        while (lineIndex + 1 < lines.Length && !lines[lineIndex].Contains("X-COORD (M)"))
                        {
                            lineIndex++;
                        }
                        lineIndex += 1;
                        isParsingTable = true;
                        Debug.WriteLine($"[DEBUG-Discrete] Знайдено таблицю для {currentPeriodKey}. Початок парсингу.");
                    }
                    continue;
                }

                if (isParsingTable)
                {
                    bool isOtherTable = line.Contains("HIGHEST") && !line.Contains("1ST HIGHEST");
                    bool isSummaryTable = line.Contains("*** THE SUMMARY OF MAXIMUM");
                    bool isMaxTable = line.Contains("*** THE MAXIMUM 400");
                    bool isFinalSummary = line.Contains("*** Message Summary :");

                    if (isOtherTable || isSummaryTable || isMaxTable || isFinalSummary)
                    {
                        Debug.WriteLine($"[DEBUG-Discrete] Кінець блоку для {currentPeriodKey}.");
                        isParsingTable = false;
                        currentPeriodKey = null;
                        continue;
                    }
                }

                if (isParsingTable && currentPeriodKey != null)
                {
                    if (string.IsNullOrEmpty(line) || !char.IsDigit(line[0]))
                    {
                        continue;
                    }

                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < dataStep) continue;

                    if (TryParseDiscretePoint(parts, 0, dataStep, ci, out var point1))
                    {
                        results[currentPeriodKey].Add(point1);
                        Debug.WriteLine($"[DEBUG-Discrete] +++ {currentPeriodKey}: X={point1.Point.X}, Y={point1.Point.Y}, Conc={point1.Concentration} +++");
                    }

                    if (parts.Length >= dataStep * 2)
                    {
                        if (TryParseDiscretePoint(parts, dataStep, dataStep, ci, out var point2))
                        {
                            results[currentPeriodKey].Add(point2);
                            Debug.WriteLine($"[DEBUG-Discrete] +++ {currentPeriodKey}: X={point2.Point.X}, Y={point2.Point.Y}, Conc={point2.Concentration} +++");
                        }
                    }
                }
            }
        }

        private static bool TryParseDiscretePoint(string[] parts, int offset, int step, CultureInfo ci, out DispersionDataPoint dataPoint)
        {
            dataPoint = null;
            try
            {
                string concValue = parts[offset + 2].TrimEnd('m', 'c');

                double x = double.Parse(parts[offset], ci);
                double y = double.Parse(parts[offset + 1], ci);
                double conc = double.Parse(concValue, ci);

                if (conc > 0)
                {
                    dataPoint = new DispersionDataPoint(new MPoint(x, y), conc);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DEBUG-Discrete] Не вдалося розпарсити точку: {ex.Message} (Рядок: {string.Join(" ", parts)})");
                return false;
            }
            return false;
        }

        #endregion

        #region Grid Mode Parser

        private static readonly Regex GridSourceOriginRegex = new Regex(
            @"X-ORIG\s*=\s*([\d\.\-]+)\s*;\s*Y-ORIG\s*=\s*([\d\.\-]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static void ParseGridModeOutput(string[] lines, CultureInfo ci, Dictionary<string, List<DispersionDataPoint>> results)
        {
            MPoint sourceOrigin = null;
            var periodDistances = new List<double>();
            var directionDataCount = new Dictionary<double, int>();

            string currentPeriodKey = null;
            bool isParsingPeriodMatrix = false;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (sourceOrigin == null && line.Contains("ORIGIN FOR POLAR NETWORK"))
                {
                    string searchLine = line;
                    if (!line.Contains("X-ORIG"))
                    {
                        if (lineIndex + 1 < lines.Length) searchLine = lines[lineIndex + 1];
                    }

                    var match = GridSourceOriginRegex.Match(searchLine);
                    if (match.Success)
                    {
                        double x = double.Parse(match.Groups[1].Value, ci);
                        double y = double.Parse(match.Groups[2].Value, ci);
                        sourceOrigin = new MPoint(x, y);
                        Debug.WriteLine($"[DEBUG-Grid] !!! ЗНАЙДЕНО ДЖЕРЕЛО: X={sourceOrigin.X}, Y={sourceOrigin.Y} !!!");
                    }
                    continue;
                }

                bool isPeriodTable = line.Contains("*** THE PERIOD") && line.Contains("AVERAGE CONCENTRATION");
                bool isRectable = line.Contains("1ST HIGHEST") && line.Contains("AVERAGE CONCENTRATION");

                if (isPeriodTable || isRectable)
                {
                    currentPeriodKey = null;
                    if (isPeriodTable) currentPeriodKey = "PERIOD";
                    else if (line.Contains("1-HR AVERAGE")) currentPeriodKey = "1-HR";
                    else if (line.Contains("3-HR AVERAGE")) currentPeriodKey = "3-HR";
                    else if (line.Contains("24-HR AVERAGE")) currentPeriodKey = "24-HR";

                    if (currentPeriodKey != null)
                    {
                        Debug.WriteLine($"[DEBUG-Grid] !!! ПОЧАТОК ПАРСИНГУ. Ключ: {currentPeriodKey} !!!");
                        isParsingPeriodMatrix = true;
                        periodDistances.Clear();
                        directionDataCount.Clear();
                    }
                    continue;
                }

                if (isParsingPeriodMatrix)
                {
                    bool isOtherTable = line.Contains("HIGHEST") && !line.Contains("1ST HIGHEST");
                    bool isSummaryTable = line.Contains("*** THE SUMMARY OF MAXIMUM");
                    bool isMaxTable = line.Contains("*** THE MAXIMUM 400");
                    bool isFinalSummary = line.Contains("*** Message Summary :");

                    if (isOtherTable || isSummaryTable || isMaxTable || isFinalSummary)
                    {
                        Debug.WriteLine($"[DEBUG-Grid] !!! ЗНАЙДЕНО КІНЕЦЬ БЛОКУ. Скидаємо парсинг. !!!");
                        isParsingPeriodMatrix = false;
                        periodDistances.Clear();
                        directionDataCount.Clear();
                        currentPeriodKey = null;
                        continue;
                    }
                }

                if (isParsingPeriodMatrix && currentPeriodKey != null)
                {
                    if (line.Trim().StartsWith("(DEGREES)") && line.Contains("|"))
                    {
                        if (periodDistances.Count == 0)
                        {
                            var parts = line.Split('|');
                            if (parts.Length < 2) continue;

                            var dists = parts[1].Trim().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var d in dists)
                            {
                                if (double.TryParse(d, NumberStyles.Float, ci, out double distVal))
                                {
                                    periodDistances.Add(distVal);
                                }
                            }
                            Debug.WriteLine($"[DEBUG-Grid] !!! ЗНАЙДЕНО {periodDistances.Count} ДИСТАНЦІЙ для {currentPeriodKey} !!!");
                        }
                        continue;
                    }

                    if (periodDistances.Count > 0 && char.IsDigit(line.TrimStart().FirstOrDefault()))
                    {
                        var parts = line.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2) continue;

                        if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, ci, out double directionDeg)) continue;

                        if (!directionDataCount.TryGetValue(directionDeg, out int distanceOffset))
                        {
                            distanceOffset = 0;
                        }

                        var concs = parts[1].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (concs.Length == 0) continue;

                        int elementStep;
                        if (currentPeriodKey == "PERIOD") elementStep = 1;
                        else if (concs[0].Contains("(")) elementStep = 1;
                        else elementStep = 2;

                        int dataPointsOnLine = concs.Length / elementStep;

                        for (int i = 0; i < dataPointsOnLine; i++)
                        {
                            int dataIndex = i * elementStep;
                            int distanceIndex = distanceOffset + i;

                            if (distanceIndex >= periodDistances.Count) break;

                            string concValue = CleanGridConcValue(concs[dataIndex]);

                            if (!double.TryParse(concValue, NumberStyles.Float, ci, out double concentration) || concentration <= 0)
                                continue;

                            double distance = periodDistances[distanceIndex];

                            if (sourceOrigin == null)
                            {
                                Debug.WriteLine("[DEBUG-Grid] !!! КРИТИЧНА ПОМИЛКА: sourceOrigin == null !!!");
                                isParsingPeriodMatrix = false;
                                break;
                            }

                            var (x, y) = CalculateXY(sourceOrigin, directionDeg, distance);
                            results[currentPeriodKey].Add(new DispersionDataPoint(new MPoint(x, y), concentration));
                        }

                        directionDataCount[directionDeg] = distanceOffset + dataPointsOnLine;
                        continue;
                    }
                }
            }
        }

        private static string CleanGridConcValue(string rawValue)
        {
            if (string.IsNullOrEmpty(rawValue)) return "0";
            if (rawValue.EndsWith("m") || rawValue.EndsWith("c")) return rawValue.Substring(0, rawValue.Length - 1);
            if (rawValue.Contains("m(") || rawValue.Contains("c(")) return rawValue.Split('m', 'c')[0];
            return rawValue;
        }

        private static (double X, double Y) CalculateXY(MPoint origin, double directionDeg, double distance)
        {
            double angleDeg = (90.0 - directionDeg + 360.0) % 360.0;
            double angleRad = angleDeg * (Math.PI / 180.0);
            double x = origin.X + distance * Math.Cos(angleRad);
            double y = origin.Y + distance * Math.Sin(angleRad);
            return (x, y);
        }

        #endregion
    }
}