using Newtonsoft.Json;

public class Tags
{
    [JsonProperty("addr:housenumber")]
    public string Housenumber { get; set; }

    [JsonProperty("addr:postcode")]
    public string Postcode { get; set; }

    [JsonProperty("addr:street")]
    public string Street { get; set; }

    [JsonProperty("building:levels")]
    public string Levels { get; set; }

    public string Name { get; set; }
}