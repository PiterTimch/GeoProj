using GeoProj.Helpers;
using GeoProj.Models;
using Mapsui;

namespace GeoProj.Services
{
    public interface IAermodService
    {
        Task<Dictionary<string, List<DispersionDataPoint>>> RunSimulationAsync(
            MPoint sourcePoint,
            AermodSourceParameters parameters,
            IProgress<string> progress);
    }
}
