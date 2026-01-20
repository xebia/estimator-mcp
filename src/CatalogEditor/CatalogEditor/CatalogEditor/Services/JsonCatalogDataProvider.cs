using System.Text.Json;
using System.Text.Json.Serialization;
using EstimatorMcp.Models;

namespace CatalogEditor.Services;

public class JsonCatalogDataProvider : ICatalogDataProvider
{
    private readonly string _dataDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private CatalogData? _cachedCatalog;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonCatalogDataProvider(IConfiguration configuration)
    {
        _dataDirectory = configuration["CatalogDataPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "data", "catalogs");
        Directory.CreateDirectory(_dataDirectory);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<CatalogData> LoadCatalogAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_cachedCatalog != null)
                return _cachedCatalog;

            var latestFile = GetLatestCatalogFile();
            if (latestFile != null && File.Exists(latestFile))
            {
                var json = await File.ReadAllTextAsync(latestFile);
                _cachedCatalog = JsonSerializer.Deserialize<CatalogData>(json, _jsonOptions) ?? new CatalogData();
            }
            else
            {
                _cachedCatalog = new CatalogData();
            }

            return _cachedCatalog;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveCatalogAsync(CatalogData catalog)
    {
        await _lock.WaitAsync();
        try
        {
            catalog.Timestamp = DateTime.UtcNow;
            var fileName = $"catalog-{catalog.Timestamp:yyyy-MM-ddTHH-mm-ssZ}.json";
            var filePath = Path.Combine(_dataDirectory, fileName);

            var json = JsonSerializer.Serialize(catalog, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            _cachedCatalog = catalog;
        }
        finally
        {
            _lock.Release();
        }
    }

    private string? GetLatestCatalogFile()
    {
        var files = Directory.GetFiles(_dataDirectory, "catalog-*.json");
        return files.OrderByDescending(f => f).FirstOrDefault();
    }

    // Roles
    public async Task<List<Role>> GetRolesAsync()
    {
        var catalog = await LoadCatalogAsync();
        return catalog.Roles;
    }

    public async Task<Role?> GetRoleAsync(string id)
    {
        var catalog = await LoadCatalogAsync();
        return catalog.Roles.FirstOrDefault(r => r.Id == id);
    }

    public async Task SaveRoleAsync(Role role)
    {
        var catalog = await LoadCatalogAsync();
        var existing = catalog.Roles.FirstOrDefault(r => r.Id == role.Id);
        if (existing != null)
        {
            catalog.Roles.Remove(existing);
        }
        catalog.Roles.Add(role);
        await SaveCatalogAsync(catalog);
    }

    public async Task DeleteRoleAsync(string id)
    {
        var catalog = await LoadCatalogAsync();
        var role = catalog.Roles.FirstOrDefault(r => r.Id == id);
        if (role != null)
        {
            // Check referential integrity
            var referencingEntries = catalog.Catalog
                .Where(e => e.MediumEstimates.Any(m => m.RoleId == id))
                .Select(e => e.Name)
                .ToList();

            if (referencingEntries.Any())
            {
                throw new ReferentialIntegrityException("Role", id, referencingEntries);
            }

            catalog.Roles.Remove(role);
            await SaveCatalogAsync(catalog);
        }
    }

    // Catalog Entries
    public async Task<List<CatalogEntry>> GetCatalogEntriesAsync()
    {
        var catalog = await LoadCatalogAsync();
        return catalog.Catalog;
    }

    public async Task<CatalogEntry?> GetCatalogEntryAsync(string id)
    {
        var catalog = await LoadCatalogAsync();
        return catalog.Catalog.FirstOrDefault(e => e.Id == id);
    }

    public async Task SaveCatalogEntryAsync(CatalogEntry entry)
    {
        var catalog = await LoadCatalogAsync();

        // Validate role references
        var roleIds = catalog.Roles.Select(r => r.Id).ToHashSet();
        var invalidRoleIds = entry.MediumEstimates
            .Select(m => m.RoleId)
            .Where(id => !roleIds.Contains(id))
            .Distinct()
            .ToList();

        if (invalidRoleIds.Any())
        {
            throw new InvalidRoleReferenceException(invalidRoleIds);
        }

        var existing = catalog.Catalog.FirstOrDefault(e => e.Id == entry.Id);
        if (existing != null)
        {
            catalog.Catalog.Remove(existing);
        }
        catalog.Catalog.Add(entry);
        await SaveCatalogAsync(catalog);
    }

    public async Task DeleteCatalogEntryAsync(string id)
    {
        var catalog = await LoadCatalogAsync();
        var entry = catalog.Catalog.FirstOrDefault(e => e.Id == id);
        if (entry != null)
        {
            catalog.Catalog.Remove(entry);
            await SaveCatalogAsync(catalog);
        }
    }

    // Referential Integrity
    public async Task<bool> IsRoleReferencedAsync(string roleId)
    {
        var catalog = await LoadCatalogAsync();
        return catalog.Catalog.Any(e => e.MediumEstimates.Any(m => m.RoleId == roleId));
    }

    public async Task<List<CatalogEntry>> GetEntriesReferencingRoleAsync(string roleId)
    {
        var catalog = await LoadCatalogAsync();
        return catalog.Catalog
            .Where(e => e.MediumEstimates.Any(m => m.RoleId == roleId))
            .ToList();
    }

    public async Task<List<string>> ValidateRoleReferencesAsync(CatalogEntry entry)
    {
        var catalog = await LoadCatalogAsync();
        var roleIds = catalog.Roles.Select(r => r.Id).ToHashSet();
        return entry.MediumEstimates
            .Select(m => m.RoleId)
            .Where(id => !roleIds.Contains(id))
            .Distinct()
            .ToList();
    }
}
