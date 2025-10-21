using System.Collections.Generic;

public class Element
{
    public string type { get; set; }
    public long id { get; set; }
    public List<long> nodes { get; set; }
    public Tags tags { get; set; }
}