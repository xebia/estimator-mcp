namespace CatalogEditor.Models;

public class CatalogEntry
{
    public string Id { get; set; } = string.Empty;
    public string FeatureId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<MediumEstimate> MediumEstimates { get; set; } = new();
}

public class MediumEstimate
{
    public string RoleId { get; set; } = string.Empty;
    public decimal Hours { get; set; }
}
