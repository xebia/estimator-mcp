namespace EstimatorMcp.Web.Data;

public class TechStackEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<RoleEntity> Roles { get; set; } = [];
    public List<CatalogEntryEntity> Entries { get; set; } = [];
}

public class RoleEntity
{
    public string Id { get; set; } = string.Empty;
    public string? TechStackId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal CopilotMultiplier { get; set; } = 1.0m;
    public TechStackEntity? TechStack { get; set; }
    public List<EntryEstimateEntity> Estimates { get; set; } = [];
}

public class CatalogEntryEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? TechStackId { get; set; }
    public string? TagsJson { get; set; }
    public TechStackEntity? TechStack { get; set; }
    public List<EntryEstimateEntity> Estimates { get; set; } = [];
}

public class EntryEstimateEntity
{
    public string EntryId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public CatalogEntryEntity Entry { get; set; } = null!;
    public RoleEntity Role { get; set; } = null!;
}

public class CatalogVersionEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
}
