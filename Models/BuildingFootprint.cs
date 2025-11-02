using NetTopologySuite.Geometries;

public class BuildingFootprint
{
    public string Id { get; set; }
    public Polygon Polygon { get; set; }
    public double HeightMeters { get; set; }
    public Dictionary<string, string> Tags { get; set; }
}
