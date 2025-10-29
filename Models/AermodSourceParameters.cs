namespace GeoProj.Models
{
    public struct AermodSourceParameters
    {
        public double EmissionRate { get; set; }  // г/с
        public double StackHeight { get; set; }   // м
        public double StackTemp { get; set; }     // K
        public double StackVelocity { get; set; } // м/с
        public double StackDiameter { get; set; } // м
    }
}
