using System.Globalization;
using System.IO;
using NetTopologySuite.Geometries;

namespace GeoProj;

public class InputFileExporter
{
    public List<BuildingFootprint> Buildings { get; set; } = new List<BuildingFootprint>();

    public async Task ExportToAermodInputAsync(string outputDirectoryPath)
    {

    }
}
