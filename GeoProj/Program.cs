using GeoProj.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace OverpassTest
{
    class Program
    {
        //static void GenerateFilesForAermod(OsmRoot data)
        //{
        //    string outputDir = "AermodFiles";
        //    Directory.CreateDirectory(outputDir);

        //    string inpPath = Path.Combine(outputDir, "buildings.inp");
        //    string sfcPath = Path.Combine(outputDir, "dummy.sfc");
        //    string pblPath = Path.Combine(outputDir, "dummy.pbl");

        //    // --- Створюємо мінімальні SFC і PBL файли ---
        //    File.WriteAllText(sfcPath, "MINIMAL SFC FILE FOR TEST");
        //    File.WriteAllText(pblPath, "MINIMAL PBL FILE FOR TEST");

        //    var inpBuilder = new StringBuilder();

        //    // --- CO блок ---
        //    inpBuilder.AppendLine("CO STARTING");
        //    inpBuilder.AppendLine("TITLEONE  Buildings Test");
        //    inpBuilder.AppendLine("TITLETWO  Generated from JSON");
        //    inpBuilder.AppendLine("MODELOPT  SIMPLE");
        //    inpBuilder.AppendLine("AVERTIME  1");
        //    inpBuilder.AppendLine("POLLUTID  PM10");
        //    inpBuilder.AppendLine("RUNORNOT  RUN");
        //    inpBuilder.AppendLine("CO FINISHED\n");

        //    // --- SO блок: всі будинки як джерела ---
        //    inpBuilder.AppendLine("SO STARTING");
        //    foreach (var element in data.elements)
        //    {
        //        if (element.tags != null && element.nodes != null && element.nodes.Count > 0)
        //        {
        //            string srcName = "SRC" + element.id;

        //            // Висота джерела
        //            double height = 10.0; // стандарт
        //            if (double.TryParse(element.tags.Levels, out double levels))
        //                height = levels * 2.8;

        //            double x = element.nodes.Average(n => (double)n);
        //            double y = element.nodes.Average(n => (double)n);

        //            inpBuilder.AppendLine($"LOCATION {srcName} {x} {y} 0.0");
        //            inpBuilder.AppendLine($"SRCPARAM {srcName} POINT 1.0 1.0 {height} 0.0");
        //            inpBuilder.AppendLine($"SRCGROUP {srcName}");
        //            inpBuilder.AppendLine($"EMISFACT {srcName} 1.0");
        //        }
        //    }
        //    inpBuilder.AppendLine("SO FINISHED\n");

        //    inpBuilder.AppendLine("RE STARTING");
        //    inpBuilder.AppendLine("GRIDCART GRID1 0.0 1000.0 100 0.0 1000.0 100 0.0");
        //    inpBuilder.AppendLine("RE FINISHED\n");

        //    inpBuilder.AppendLine("ME STARTING");
        //    inpBuilder.AppendLine($"SURFFILE {Path.GetFileName(sfcPath)}");
        //    inpBuilder.AppendLine($"PROFFILE {Path.GetFileName(pblPath)}");
        //    inpBuilder.AppendLine($"SURFDATA {Path.GetFileName(sfcPath)}");
        //    inpBuilder.AppendLine($"UAIRDATA {Path.GetFileName(pblPath)}");
        //    inpBuilder.AppendLine("PROFBASE 0.0");
        //    inpBuilder.AppendLine("ME FINISHED\n");

        //    inpBuilder.AppendLine("OU STARTING");
        //    inpBuilder.AppendLine("RECTABLE ALLAVE FIRST");
        //    inpBuilder.AppendLine("PERIOD 1");
        //    inpBuilder.AppendLine("OU FINISHED");

        //    File.WriteAllText(inpPath, inpBuilder.ToString());

        //    Console.WriteLine($"Файли для AERMOD згенеровано в папці {outputDir}:");
        //    Console.WriteLine($" - {Path.GetFileName(inpPath)}");
        //    Console.WriteLine($" - {Path.GetFileName(sfcPath)}");
        //    Console.WriteLine($" - {Path.GetFileName(pblPath)}");
        //}

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            int buildingsWithoutLevels = 0;

            string jsonPath = "Data/Buildings.json";
            string json = File.ReadAllText(jsonPath);

            OsmRoot data = JsonConvert.DeserializeObject<OsmRoot>(json);

            foreach (var element in data.elements)
            {
                if (element.tags != null)
                {
                    string address = $"{element.tags.Street} {element.tags.Housenumber}, {element.tags.Postcode}";
                    Console.WriteLine($"Адреса: {address}");

                    if (element.nodes != null)
                    {
                        Console.WriteLine($"Вузли (ID): {string.Join(", ", element.nodes)}");

                        if (double.TryParse(element.tags.Levels, out double levels))
                        {
                            double height = levels * 2.8;
                            Console.WriteLine($"Висота будівлі: {height} м");
                        }
                        else
                        {
                            buildingsWithoutLevels++;
                            Console.WriteLine("Висота: невідомо");
                        }

                        Console.WriteLine(new string('-', 60));
                    }
                }
            }

            Console.WriteLine("Загальна кількість будинків: " + data.elements.Count);
            Console.WriteLine("Будинки без поверхів: " + buildingsWithoutLevels);

            //GenerateFilesForAermod(data);
        }
    }
}
