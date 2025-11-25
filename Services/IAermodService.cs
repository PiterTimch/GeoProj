using GeoProj.Models;

namespace GeoProj.Services
{
    public interface IAermodService
    {
        Task<Dictionary<string, List<DispersionDataPoint>>> RunSimulationAsync(List<AermodSource> sources, ReceptorSettings receptorSettings, IProgress<string> progress);
    }
}