namespace CatalogEditor.Models;

public class CatalogData
{
    public string Version { get; set; } = "1.0";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<Role> Roles { get; set; } = new();
    public List<CatalogEntry> Catalog { get; set; } = new();
}
