namespace EstimatorMcp.Models;

public class CatalogEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Technology stack identifier (e.g., "salesforce", "blazor-azure", "nodejs", "shared").
    /// Used for filtering features by project type.
    /// </summary>
    public string? TechStack { get; set; }
    
    /// <summary>
    /// Flexible tags for additional categorization (e.g., ["apex", "backend", "api"]).
    /// Enables multi-dimensional filtering and better searchability.
    /// </summary>
    public List<string>? Tags { get; set; }
    
    public List<MediumEstimate> MediumEstimates { get; set; } = new();
}

public class MediumEstimate
{
    public string RoleId { get; set; } = string.Empty;
    public decimal Hours { get; set; }
}
