using EstimatorMcp.Models;

namespace CatalogEditor.Services;

public interface ICatalogDataProvider
{
    Task<CatalogData> LoadCatalogAsync();
    Task SaveCatalogAsync(CatalogData catalog);
    Task<List<Role>> GetRolesAsync();
    Task<Role?> GetRoleAsync(string id);
    Task SaveRoleAsync(Role role);
    Task DeleteRoleAsync(string id);
    Task<List<CatalogEntry>> GetCatalogEntriesAsync();
    Task<CatalogEntry?> GetCatalogEntryAsync(string id);
    Task SaveCatalogEntryAsync(CatalogEntry entry);
    Task DeleteCatalogEntryAsync(string id);
}
