using GeoProj.Helpers;
using GeoProj.Models;
using Mapsui;
using System.Diagnostics;
using System.IO;

namespace GeoProj.Services
{
    public class AermodService : IAermodService
    {
        private readonly string _baseDir;

        public AermodService()
        {
            _baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "aermod_exe");
        }

        public async Task<Dictionary<string, List<DispersionDataPoint>>> RunSimulationAsync(List<AermodSource> sources, ReceptorSettings receptorSettings, IProgress<string> progress)
        {
            progress.Report("Крок 1/3: Генерація вхідних файлів (.inp)...");
            try
            {
                AermodFileGenerator.GenerateInputFiles(sources, receptorSettings, _baseDir);
            }
            catch (Exception ex)
            {
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

            if (!skipAermap)
            {
                await RunProcessAsync("aermap.exe", "", aermapDir);
            }

            await RunProcessAsync("aermet.exe", "aermet1.inp", aermetDir);
            await RunProcessAsync("aermet.exe", "aermet2.inp", aermetDir);
            await RunProcessAsync("aermod.exe", "", aermodDir);
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
