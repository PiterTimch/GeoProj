using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GeoProj.Models
{
    public class OsmRoot
    {
        public double version { get; set; }
        public string generator { get; set; }
        public Osm3s osm3s { get; set; }
        public List<Element> elements { get; set; }
    }
}
