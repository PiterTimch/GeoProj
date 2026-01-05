using GeoProj.Helpers;
using GeoProj.Models;
using Mapsui;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GeoProj.Services
{
    public class AermodService : IAermodService
    {
        private readonly string _baseDir;

        public AermodService()
        {
            _baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "aermod_exe");
        }

        public async Task<Dictionary<string, List<DispersionDataPoint>>> RunSimulationAsync(List<AermodSource> sources, ReceptorSettings receptorSettings, IProgress<string> progress, List<BuildingFootprint> buildings = null)
        {
            progress.Report("Крок 1/3: Генерація вхідних файлів (.inp)...");
            try
            {
                Debug.WriteLine($"[AermodService] Генерація файлів для {sources?.Count ?? 0} джерел");
                AermodFileGenerator.GenerateInputFiles(sources, receptorSettings, _baseDir, buildings);
                
                // Перевірка згенерованого файлу
                string inpPath = Path.Combine(_baseDir, "AERMOD", "aermod.inp");
                if (File.Exists(inpPath))
                {
                    var inpInfo = new FileInfo(inpPath);
                    Debug.WriteLine($"[AermodService] ✅ Файл aermod.inp згенеровано: {inpInfo.Length} байт, оновлено: {inpInfo.LastWriteTime}");
                    
                    // Перевірка вмісту
                    var lines = File.ReadAllLines(inpPath);
                    var srcLines = lines.Where(l => l.Contains("SRCPARAM")).Take(3).ToList();
                    Debug.WriteLine($"[AermodService] Перші рядки SRCPARAM:");
                    foreach (var line in srcLines)
                    {
                        Debug.WriteLine($"[AermodService]   {line}");
                    }
                }
                else
                {
                    throw new Exception($"Файл aermod.inp не було створено в {inpPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AermodService] ПОМИЛКА ГЕНЕРАЦІЇ: {ex.Message}");
                throw new Exception($"Не вдалося згенерувати файли: {ex.Message}", ex);
            }

            progress.Report("Крок 2/3: Запуск симуляції AERMOD (це може зайняти час)...");
            try
            {
                await RunAermodSimulation(skipAermap: true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Помилка під час виконання симуляції: {ex.Message}\nПеревірте ERRORS.OUT.", ex);
            }

            progress.Report("Крок 3/3: Обробка результатів...");
            try
            {
                string aermodOutPath = Path.Combine(_baseDir, "AERMOD", "aermod.out");

                var allResults = AermodResultParser.ParseAermodOutFile(aermodOutPath, receptorSettings.Mode);

                progress.Report("Симуляцію завершено успішно!");
                return allResults;
            }
            catch (Exception ex)
            {
                throw new Exception($"Помилка парсингу результатів: {ex.Message}", ex);
            }
        }

        private async Task RunAermodSimulation(bool skipAermap = false)
        {
            string aermapDir = Path.Combine(_baseDir, "AERMAP");
            string aermetDir = Path.Combine(_baseDir, "AERMET");
            string aermodDir = Path.Combine(_baseDir, "AERMOD");

            // Перевірка, чи існує файл aermod.inp перед запуском
            string inpPath = Path.Combine(aermodDir, "aermod.inp");
            if (!File.Exists(inpPath))
            {
                throw new Exception($"Файл aermod.inp не знайдено в {aermodDir}. Генерація файлів не вдалася.");
            }

            var inpInfo = new FileInfo(inpPath);
            Debug.WriteLine($"[AermodService] Файл aermod.inp існує: {inpInfo.Length} байт, оновлено: {inpInfo.LastWriteTime}");

            if (!skipAermap)
            {
                Debug.WriteLine("[AermodService] Запуск AERMAP...");
                await RunProcessAsync("aermap.exe", "", aermapDir);
            }

            Debug.WriteLine("[AermodService] Запуск AERMET (етап 1)...");
            await RunProcessAsync("aermet.exe", "aermet1.inp", aermetDir);
            
            Debug.WriteLine("[AermodService] Запуск AERMET (етап 2)...");
            await RunProcessAsync("aermet.exe", "aermet2.inp", aermetDir);
            
            Debug.WriteLine("[AermodService] Запуск AERMOD...");
            await RunProcessAsync("aermod.exe", "", aermodDir);
            
            // Перевірка, чи оновився файл результатів
            string outPath = Path.Combine(aermodDir, "aermod.out");
            if (File.Exists(outPath))
            {
                var outInfo = new FileInfo(outPath);
                Debug.WriteLine($"[AermodService] Файл aermod.out оновлено: {outInfo.LastWriteTime}, розмір: {outInfo.Length} байт");
            }
            else
            {
                Debug.WriteLine("[AermodService] ⚠️ Файл aermod.out не знайдено після запуску AERMOD!");
            }
        }

        private Task RunProcessAsync(string fileName, string arguments, string workingDirectory)
        {
            return Task.Run(() =>
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(workingDirectory, fileName),
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = new Process { StartInfo = startInfo })
                {
                    process.OutputDataReceived += (sender, args) => Debug.WriteLine(args.Data);
                    process.ErrorDataReceived += (sender, args) => Debug.WriteLine($"ПОМИЛКА: {args.Data}");

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"Процес {fileName} завершився з кодом {process.ExitCode}. Перевірте лог або ERRORS.OUT.");
                    }
                }
            });
        }
    }
}
