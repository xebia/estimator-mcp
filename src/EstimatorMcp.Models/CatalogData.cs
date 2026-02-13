using System.Text.Json;
using System.Text.Json.Serialization;

namespace EstimatorMcp.Models;

public class CatalogData
{
    public string Version { get; set; } = "2.0";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<TechStack> TechStacks { get; set; } = [];
    public List<Role> GlobalRoles { get; set; } = [];
    public List<CatalogEntry> Catalog { get; set; } = [];

    /// <summary>
    /// All roles (global + techstack-specific). TechStackId is populated on each role.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<Role> AllRoles => GlobalRoles
        .Concat(TechStacks.SelectMany(ts => ts.Roles));

    /// <summary>
    /// Returns the roles available for a catalog entry with the given techstack:
    /// the techstack's own roles plus all global roles.
    /// If techStackId is null, only global roles are returned.
    /// </summary>
    public List<Role> GetAvailableRolesForTechStack(string? techStackId)
    {
        var roles = new List<Role>(GlobalRoles);
        if (!string.IsNullOrEmpty(techStackId))
        {
            var ts = TechStacks.FirstOrDefault(t => t.Id == techStackId);
            if (ts != null)
                roles.AddRange(ts.Roles);
        }
        return roles;
    }

    /// <summary>
    /// Ensures TechStackId is populated on all roles based on their parent TechStack.
    /// Call this after deserialization.
    /// </summary>
    public void PopulateRoleTechStackIds()
    {
        foreach (var role in GlobalRoles)
            role.TechStackId = null;

        foreach (var ts in TechStacks)
        {
            foreach (var role in ts.Roles)
                role.TechStackId = ts.Id;
        }
    }

    /// <summary>
    /// Deserializes a catalog JSON string, automatically migrating v1.0 format to v2.0.
    /// </summary>
    public static CatalogData DeserializeWithMigration(string json, JsonSerializerOptions options)
    {
        // Peek at the JSON to detect v1.0 format (has "roles" but not "globalRoles")
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var hasRoles = root.TryGetProperty("roles", out var rolesElement);
        var hasGlobalRoles = root.TryGetProperty("globalRoles", out _);

        var catalog = JsonSerializer.Deserialize<CatalogData>(json, options) ?? new CatalogData();

        // v1.0 migration: "roles" → GlobalRoles, extract TechStacks from entries
        if (hasRoles && !hasGlobalRoles)
        {
            var v1Roles = JsonSerializer.Deserialize<List<Role>>(rolesElement.GetRawText(), options) ?? [];
            catalog.GlobalRoles = v1Roles;
            catalog.Version = "2.0";

            // Create TechStack entries from unique TechStack values in catalog entries
            var techStackIds = catalog.Catalog
                .Where(e => !string.IsNullOrEmpty(e.TechStack))
                .Select(e => e.TechStack!)
                .Distinct()
                .OrderBy(ts => ts);

            catalog.TechStacks = techStackIds.Select(ts => new TechStack
            {
                Id = ts,
                Name = ts,
                Description = string.Empty
            }).ToList();
        }

        catalog.PopulateRoleTechStackIds();
        return catalog;
    }
}
