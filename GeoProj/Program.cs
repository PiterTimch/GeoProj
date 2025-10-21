using GeoProj.Models;
using Newtonsoft.Json;
using System;
using System.IO;

namespace OverpassTest
{
    class Program
    {
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

            Console.WriteLine("Загальна кількість будинків: " + data.elements.Count.ToString());
            Console.WriteLine("Будинки без поверхів: " + buildingsWithoutLevels.ToString());
        }
    }
}
