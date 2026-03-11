using EstimatorMcp.Models;

namespace EstimatorMcp.Web.Services;

public interface ICatalogDataProvider
{
    Task<CatalogData> LoadCatalogAsync();
    Task SaveCatalogAsync(CatalogData catalog);

    // TechStacks
    Task<List<TechStack>> GetTechStacksAsync();
    Task<TechStack?> GetTechStackAsync(string id);
    Task SaveTechStackAsync(TechStack techStack);
    /// <summary>Deletes a techstack and its roles. Throws ReferentialIntegrityException if catalog entries reference this techstack.</summary>
    Task DeleteTechStackAsync(string id);

    // Roles
    Task<List<Role>> GetRolesAsync();
    Task<Role?> GetRoleAsync(string id);
    Task<List<Role>> GetGlobalRolesAsync();
    Task<List<Role>> GetRolesForTechStackAsync(string techStackId);
    Task<List<Role>> GetAvailableRolesForEntryAsync(string? techStackId);
    Task SaveRoleAsync(Role role);
    /// <summary>Deletes a role. Throws ReferentialIntegrityException if the role is referenced by catalog entries.</summary>
    Task DeleteRoleAsync(string id);

    // Catalog Entries
    Task<List<CatalogEntry>> GetCatalogEntriesAsync();
    Task<CatalogEntry?> GetCatalogEntryAsync(string id);
    /// <summary>Saves a catalog entry. Throws InvalidRoleReferenceException if any RoleIds in estimates don't exist.</summary>
    Task SaveCatalogEntryAsync(CatalogEntry entry);
    Task DeleteCatalogEntryAsync(string id);

    // Referential Integrity
    Task<bool> IsRoleReferencedAsync(string roleId);
    Task<List<CatalogEntry>> GetEntriesReferencingRoleAsync(string roleId);
    Task<List<string>> ValidateRoleReferencesAsync(CatalogEntry entry);
}
