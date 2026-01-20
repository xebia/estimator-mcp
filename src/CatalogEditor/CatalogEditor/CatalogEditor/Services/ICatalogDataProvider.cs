using EstimatorMcp.Models;

namespace CatalogEditor.Services;

public interface ICatalogDataProvider
{
    Task<CatalogData> LoadCatalogAsync();
    Task SaveCatalogAsync(CatalogData catalog);

    // Roles
    Task<List<Role>> GetRolesAsync();
    Task<Role?> GetRoleAsync(string id);
    Task SaveRoleAsync(Role role);
    /// <summary>
    /// Deletes a role. Throws ReferentialIntegrityException if the role is referenced by catalog entries.
    /// </summary>
    Task DeleteRoleAsync(string id);

    // Catalog Entries
    Task<List<CatalogEntry>> GetCatalogEntriesAsync();
    Task<CatalogEntry?> GetCatalogEntryAsync(string id);
    /// <summary>
    /// Saves a catalog entry. Throws InvalidRoleReferenceException if any RoleIds in estimates don't exist.
    /// </summary>
    Task SaveCatalogEntryAsync(CatalogEntry entry);
    Task DeleteCatalogEntryAsync(string id);

    // Referential Integrity
    Task<bool> IsRoleReferencedAsync(string roleId);
    Task<List<CatalogEntry>> GetEntriesReferencingRoleAsync(string roleId);
    Task<List<string>> ValidateRoleReferencesAsync(CatalogEntry entry);
}
