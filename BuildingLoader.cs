using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System.IO;

public static class BuildingLoader
{
    public static List<BuildingFootprint> LoadBuildings(string geoJsonPath)
    {
        if (!File.Exists(geoJsonPath))
        {
            throw new FileNotFoundException($"GeoJSON wasn't found: {geoJsonPath}");
        }

        string json = File.ReadAllText(geoJsonPath);

        var reader = new GeoJsonReader();
        var featureCollection = reader.Read<FeatureCollection>(json);

        var buildings = new List<BuildingFootprint>();

        foreach (var feature in featureCollection)
        {
            var geom = feature.Geometry as Polygon;
            if (geom == null) continue;

            var attrs = feature.Attributes;
            var tags = attrs.GetNames().ToDictionary(name => name, name => attrs[name]?.ToString() ?? "");

            double height = 0.0;

            if (attrs.Exists("height"))
            {
                if (double.TryParse(attrs["height"].ToString(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double h))
                    height = h;
            }
            else if (attrs.Exists("building:levels"))
            {
                if (double.TryParse(attrs["building:levels"].ToString(), out double levels))
                {
                    height = levels * 3.0;
                }
            }
            else
            {
                height = 0;
            }

            buildings.Add(new BuildingFootprint
            {
                Id = attrs.Exists("@id") ? attrs["@id"].ToString() : Guid.NewGuid().ToString(),
                Polygon = geom,
                HeightMeters = height,
                Tags = tags
            });
        }

        return buildings;
    }
}
