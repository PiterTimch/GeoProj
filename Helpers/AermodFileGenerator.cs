using GeoProj.Models;
using Mapsui;
using System.Globalization;
using System.IO;
using System.Text;

namespace GeoProj.Helpers
{
    public static class AermodFileGenerator
    {
        public static void GenerateInputFiles(MPoint sourcePoint, AermodSourceParameters sourceParams, string baseDirectoryPath, List<BuildingFootprint> buildings)
        {
            string aermetDir = Path.Combine(baseDirectoryPath, "AERMET");
            string aermodDir = Path.Combine(baseDirectoryPath, "AERMOD");

            Directory.CreateDirectory(aermetDir);
            Directory.CreateDirectory(aermodDir);

            GenerateAermetInputs(aermetDir);

            GenerateAermodInput(aermodDir, sourcePoint, sourceParams, buildings);
        }

        private static void GenerateAermetInputs(string aermetDir)
        {
            // --- aermet1.inp ---
            var sb1 = new StringBuilder();
            sb1.AppendLine("job");
            sb1.AppendLine("  messages  aermet_st1.msg");
            sb1.AppendLine("  report    aermet_st1.rpt");
            sb1.AppendLine("  ");
            sb1.AppendLine("upperair");
            sb1.AppendLine("** Upper air data for Albany, NY from RDNA CD (with 18 missing");
            sb1.AppendLine("** 12Z soundings substituted from Sterling, VA (93734))");
            sb1.AppendLine("  data      14735-92.fsl  fsl");
            sb1.AppendLine("  extract   alb92-93.iqa");
            sb1.AppendLine("  location  00014735  42.75n  73.80w  5 86.0");
            sb1.AppendLine("  xdates    1992/5/1 to 1993/5/19");
            sb1.AppendLine("  qaout     alb92-93.oqa");
            sb1.AppendLine("");
            sb1.AppendLine("surface");
            sb1.AppendLine("** Surface data for Allentown-Beth-Easton, PA in CD144 format");
            sb1.AppendLine("   data      14737.dat  CD144");
            sb1.AppendLine("   extract   14737.iqa");
            sb1.AppendLine("   qaout     14737.oqa");
            sb1.AppendLine("   location  14737 40.65N 75.43W 0");
            sb1.AppendLine("   xdates    1992/5/1 TO 1993/5/19");
            sb1.AppendLine("");
            sb1.AppendLine("onsite");
            sb1.AppendLine("  data      mcospfl.dat");
            sb1.AppendLine("");
            sb1.AppendLine("  location  000001   40.79n  75.14w  0");
            sb1.AppendLine("");
            sb1.AppendLine("  xdates    1992/5/1  1993/5/19");
            sb1.AppendLine("  qaout     mcospfl.oqa");
            sb1.AppendLine("  read      1  osyr  osmo  osdy  ");
            sb1.AppendLine("  read      2  HT01  WD01  WS01  SA01  TT01");
            sb1.AppendLine("  read      3  HT02  WD02  WS02  ");
            sb1.AppendLine("  read      4  HT03  WD03  WS03  ");
            sb1.AppendLine("  read      5  HT04  WD04  WS04  ");
            sb1.AppendLine("  read      6  HT05  WD05  WS05  ");
            sb1.AppendLine("  read      7  HT06  WD06  WS06  ");
            sb1.AppendLine("");
            sb1.AppendLine("  read      8  HT07  WD07  WS07  ");
            sb1.AppendLine("  read      9  HT08  WD08  WS08  ");
            sb1.AppendLine("  read     10  HT09  WD09  WS09  ");
            sb1.AppendLine("  read     11  HT10  WD10  WS10  ");
            sb1.AppendLine("  read     12  HT11  WD11  WS11  ");
            sb1.AppendLine("  read     13  HT12  WD12  WS12  ");
            sb1.AppendLine("  read     14  HT13 ");
            sb1.AppendLine("                         ");
            sb1.AppendLine("  format    1  ( 2X,I2,I4,I4,I4 )");
            sb1.AppendLine("  format    2  ( 5F10.4, )");
            sb1.AppendLine("  format    3  ( 3F10.4 )");
            sb1.AppendLine("  format    4  ( 3F10.4 )");
            sb1.AppendLine("  format    5  ( 3F10.4 )");
            sb1.AppendLine("  format    6  ( 3F10.4 )");
            sb1.AppendLine("  format    7  ( 3F10.4 )");
            sb1.AppendLine(" ");
            sb1.AppendLine("  format    8  ( 3F10.4 )");
            sb1.AppendLine("  format    9  ( 3F10.4 )");
            sb1.AppendLine("  format   10  ( 3F10.4 )");
            sb1.AppendLine("  format   11  ( 3F10.4 )");
            sb1.AppendLine("  format   12  ( 3F10.4 )");
            sb1.AppendLine("  format   13  ( 3F10.4 )");
            sb1.AppendLine("  format   14  ( 3F10.4 )");
            sb1.AppendLine("");
            sb1.AppendLine("  threshold 0.3");
            sb1.AppendLine("");
            sb1.AppendLine("  range     tt    -30 <=  35  999");
            sb1.AppendLine("  range     ws      0 <   50  999");
            sb1.AppendLine("  range     wd      0 <= 360  999");
            sb1.AppendLine("  range     sa      0 <= 360  999");
            sb1.AppendLine("");
            sb1.AppendLine("  audit     sa");
            sb1.AppendLine("");
            sb1.AppendLine("  no_missing TT02,TT03,TT04,TT05,TT06,TT07,TT08, TT09");
            sb1.AppendLine("  no_missing TT10,TT11,TT12,TT13,TT14");

            File.WriteAllText(Path.Combine(aermetDir, "aermet1.inp"), sb1.ToString());

            // --- aermet2.inp ---
            var sb2 = new StringBuilder();
            sb2.AppendLine("job");
            sb2.AppendLine("  messages  aermet_st2.msg");
            sb2.AppendLine("  report    aermet_st2.rpt");
            sb2.AppendLine("");
            sb2.AppendLine("upperair");
            sb2.AppendLine("  qaout     alb92-93.oqa");
            sb2.AppendLine("");
            sb2.AppendLine("surface");
            sb2.AppendLine("  qaout     14737.oqa");
            sb2.AppendLine("");
            sb2.AppendLine("onsite");
            sb2.AppendLine("  qaout     mcospfl.oqa");
            sb2.AppendLine("");
            sb2.AppendLine("metprep");
            sb2.AppendLine("   xdates    1992/5/1 to 1993/5/19");
            sb2.AppendLine("   location  000001   40.79n  75.14w  5");
            sb2.AppendLine("");
            sb2.AppendLine("** no nws substitution");
            sb2.AppendLine("** norand");
            sb2.AppendLine("");
            sb2.AppendLine("   nws_hgt   wind  6.1");
            sb2.AppendLine("   output    aermet.sfc");
            sb2.AppendLine("   profile   aermet.pfl");
            sb2.AppendLine("");
            sb2.AppendLine("   freq_sect  monthly  2");
            sb2.AppendLine("   sector     1  260  180");
            sb2.AppendLine("   sector     2  180  260");
            sb2.AppendLine("");
            sb2.AppendLine("   site_char      1  1  0.15  1.00  0.10");
            sb2.AppendLine("   site_char      2  1  0.40  1.30  0.10");
            sb2.AppendLine("   site_char      3  1  0.40  0.50  0.10");
            sb2.AppendLine("   site_char      4  1  0.15  0.30  0.20");
            sb2.AppendLine("   site_char      5  1  0.15  0.50  0.20");
            sb2.AppendLine("   site_char      6  1  0.15  0.40  0.30");
            sb2.AppendLine("   site_char      7  1  0.15  0.30  0.30");
            sb2.AppendLine("   site_char      8  1  0.15  0.40  0.30");
            sb2.AppendLine("   site_char      9  1  0.15  0.80  0.20");
            sb2.AppendLine("   site_char     10  1  0.15  2.00  0.20");
            sb2.AppendLine("   site_char     11  1  0.15  0.40  0.20");
            sb2.AppendLine("   site_char     12  1  0.15  1.00  0.10");
            sb2.AppendLine("");
            sb2.AppendLine("   site_char      1  2  0.15  1.00  0.30");
            sb2.AppendLine("   site_char      2  2  0.40  1.30  0.30");
            sb2.AppendLine("   site_char      3  2  0.40  0.50  0.30");
            sb2.AppendLine("   site_char      4  2  0.15  0.30  0.50");
            sb2.AppendLine("   site_char      5  2  0.15  0.50  0.50");
            sb2.AppendLine("   site_char      6  2  0.15  0.40  0.60");
            sb2.AppendLine("   site_char      7  2  0.15  0.30  0.60");
            sb2.AppendLine("   site_char      8  2  0.15  0.40  0.60");
            sb2.AppendLine("   site_char      9  2  0.15  0.80  0.50");
            sb2.AppendLine("   site_char     10  2  0.15  2.00  0.50");
            sb2.AppendLine("   site_char     11  2  0.15  0.40  0.50");
            sb2.AppendLine("   site_char     12  2  0.15  1.00  0.30");

            File.WriteAllText(Path.Combine(aermetDir, "aermet2.inp"), sb2.ToString());
        }

        private static void ExtractBuildingSize(
            BuildingFootprint b,
            out double centerX,
            out double centerY,
            out double length,
            out double width)
        {
            var env = b.Polygon.EnvelopeInternal;

            centerX = (env.MinX + env.MaxX) / 2.0;
            centerY = (env.MinY + env.MaxY) / 2.0;

            length = Math.Abs(env.MaxX - env.MinX);
            width = Math.Abs(env.MaxY - env.MinY);

            if (length < 1.0) length = 1.0;
            if (width < 1.0) width = 1.0;
        }


        private static void GenerateAermodInput(
            string aermodDir,
            MPoint sourcePoint,
            AermodSourceParameters sourceParams,
            List<BuildingFootprint> buildings)
        {
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;

            // --- CO (Control) Pathway ---
            sb.AppendLine("CO STARTING");
            sb.AppendLine("   TITLEONE CO2 Dispersion With Buildings");
            sb.AppendLine("   MODELOPT FLAT CONC DOWNWASH");  // <-- важливо
            sb.AppendLine("   AVERTIME 1 3 24 PERIOD");
            sb.AppendLine("   POLLUTID CO2");
            sb.AppendLine("   RUNORNOT run");
            sb.AppendLine("   ERRORFIL ERRORS.OUT");
            sb.AppendLine("CO FINISHED");
            sb.AppendLine("");

            // --- SO (Source) Pathway ---
            sb.AppendLine("SO STARTING");
            double baseElevation = 0.0;

            sb.AppendLine(string.Format(ci,
                "   LOCATION  STACK1  POINT  {0:F1}  {1:F1}  {2:F1}",
                sourcePoint.X, sourcePoint.Y, baseElevation));

            sb.AppendLine(string.Format(ci,
                "   SRCPARAM  STACK1  {0:F1}  {1:F1}  {2:F1}  {3:F1}  {4:F1}",
                sourceParams.EmissionRate,
                sourceParams.StackHeight,
                sourceParams.StackTemp,
                sourceParams.StackVelocity,
                sourceParams.StackDiameter));

            sb.AppendLine("   SRCGROUP  ALL");
            sb.AppendLine("");

            // --- BUILDINGS (AERMOD primitive format) ---
            sb.AppendLine("   ** BUILDING DOWNWASH DATA **");

            foreach (var b in buildings)
            {
                ExtractBuildingSize(b, out double cx, out double cy, out double len, out double wid);

                sb.AppendLine(string.Format(ci,
                    "   BUILDHGT STACK1 {0:F1}", b.HeightMeters));

                sb.AppendLine(string.Format(ci,
                    "   BUILDWID {0} {1:F1}", b.Id, wid));

                sb.AppendLine(string.Format(ci,
                    "   BUILDLEN {0} {1:F1}", b.Id, len));

                // AERMOD needs building location also
                sb.AppendLine(string.Format(ci,
                    "   BUILDLOC {0} {1:F1} {2:F1}", b.Id, cx, cy));
            }

            sb.AppendLine("SO FINISHED");
            sb.AppendLine("");

            // --- RE (Receptors) ---
            sb.AppendLine("RE STARTING");
            sb.AppendLine("   GRIDPOLR POLR1 STA");
            sb.AppendLine("   GRIDPOLR POLR1 ORIG STACK1");
            sb.AppendLine("   GRIDPOLR POLR1 DIST 100. 300. 500. 750. 1000. 2000. 3000. 5000.");
            sb.AppendLine("   GRIDPOLR POLR1 GDIR 16 1.0 22.5");
            sb.AppendLine("   GRIDPOLR POLR1 ELEV 1 0.0");
            sb.AppendLine("   GRIDPOLR POLR1 END");
            sb.AppendLine("RE FINISHED");
            sb.AppendLine("");

            // --- ME (Met) ---
            sb.AppendLine("ME STARTING");
            sb.AppendLine(@"   SURFFILE  ..\aermet\aermet.sfc   free");
            sb.AppendLine(@"   PROFFILE  ..\aermet\aermet.pfl   free");
            sb.AppendLine("ME FINISHED");
            sb.AppendLine("");

            // --- OU (Output) ---
            sb.AppendLine("OU STARTING");
            sb.AppendLine("   RECTABLE  allave first-second");
            sb.AppendLine("   MAXTABLE  allave    400");
            sb.AppendLine("   SUMMFILE  aermod.sum");
            sb.AppendLine("OU FINISHED");

            File.WriteAllText(Path.Combine(aermodDir, "aermod.inp"), sb.ToString());
        }

    }
}
