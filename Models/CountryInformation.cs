namespace RedisCaching.Models;

public class CountryInformation
{
    public NameInfo Name { get; set; }
    public string Flag { get; set; }
    public long Population { get; set; }
    public List<string> Capital { get; set; }
}

public record NameInfo(string Common, string Official);