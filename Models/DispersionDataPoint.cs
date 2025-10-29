using Mapsui;

namespace GeoProj.Models
{
    public class DispersionDataPoint
    {
        public MPoint Point { get; }
        public double Concentration { get; }

        public DispersionDataPoint(MPoint point, double concentration)
        {
            Point = point;
            Concentration = concentration;
        }
    }
}
