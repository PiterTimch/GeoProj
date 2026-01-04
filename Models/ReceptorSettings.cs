using Mapsui;

namespace GeoProj.Models
{
    public enum ReceptorMode
    {
        Grid,
        Discrete
    }

    public class ReceptorSettings
    {
        public ReceptorMode Mode { get; set; }
        public List<MPoint> Points { get; set; }
        public MPoint GridOrigin { get; set; }

        public ReceptorSettings()
        {
            Mode = ReceptorMode.Grid;
            Points = new List<MPoint>();
        }
    }
}
