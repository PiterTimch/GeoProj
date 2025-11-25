using GeoProj.Helpers;
using Mapsui;

namespace GeoProj.Models
{
    public class AermodSource : BaseViewModel
    {
        private static int _counter = 0;

        public string SourceId { get; }
        public MPoint Point { get; }

        public string DisplayName => $"Джерело {SourceId}";

        private double _emissionRate = 100.0;
        public double EmissionRate { get => _emissionRate; set => SetProperty(ref _emissionRate, value); }

        private double _stackHeight = 50.0;
        public double StackHeight { get => _stackHeight; set => SetProperty(ref _stackHeight, value); }

        private double _stackTemp = 450.0;
        public double StackTemp { get => _stackTemp; set => SetProperty(ref _stackTemp, value); }

        private double _stackVelocity = 20.0;
        public double StackVelocity { get => _stackVelocity; set => SetProperty(ref _stackVelocity, value); }

        private double _stackDiameter = 1.5;
        public double StackDiameter { get => _stackDiameter; set => SetProperty(ref _stackDiameter, value); }

        public AermodSource(MPoint point)
        {
            Point = point;
            SourceId = $"S{Interlocked.Increment(ref _counter):D2}";
        }
    }
}