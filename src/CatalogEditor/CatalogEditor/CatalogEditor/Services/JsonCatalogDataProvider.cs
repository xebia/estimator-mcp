using System.Text.Json;
using System.Text.Json.Serialization;
using EstimatorMcp.Models;

namespace CatalogEditor.Services;

public class JsonCatalogDataProvider : ICatalogDataProvider
{
    private readonly string _dataDirectory;
    private readonly JsonSerializerOptions _writeOptions;
    private readonly JsonSerializerOptions _readOptions;
    private CatalogData? _cachedCatalog;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonCatalogDataProvider(IConfiguration configuration)
    {
        _dataDirectory = configuration["CatalogDataPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "data", "catalogs");
        Directory.CreateDirectory(_dataDirectory);

        _writeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        _readOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
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
                _cachedCatalog = CatalogData.DeserializeWithMigration(json, _readOptions);
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

            var json = JsonSerializer.Serialize(catalog, _writeOptions);
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

    // TechStacks
    public async Task<List<TechStack>> GetTechStacksAsync()
    {
        var catalog = await LoadCatalogAsync();
        return catalog.TechStacks;
    }

    public async Task<TechStack?> GetTechStackAsync(string id)
    {
        var catalog = await LoadCatalogAsync();
        return catalog.TechStacks.FirstOrDefault(ts => ts.Id == id);
    }

    public async Task SaveTechStackAsync(TechStack techStack)
    {
        var catalog = await LoadCatalogAsync();
        var existing = catalog.TechStacks.FirstOrDefault(ts => ts.Id == techStack.Id);
        if (existing != null)
        {
            // Preserve existing roles when updating metadata
            techStack.Roles = existing.Roles;
            catalog.TechStacks.Remove(existing);
        }
        catalog.TechStacks.Add(techStack);
        await SaveCatalogAsync(catalog);
    }

    public async Task DeleteTechStackAsync(string id)
    {
        var catalog = await LoadCatalogAsync();
        var techStack = catalog.TechStacks.FirstOrDefault(ts => ts.Id == id);
        if (techStack == null) return;

        var referencingEntries = catalog.Catalog
            .Where(e => e.TechStack == id)
            .Select(e => e.Name)
            .ToList();

        if (referencingEntries.Any())
        {
            throw new ReferentialIntegrityException("TechStack", id, referencingEntries);
        }

        catalog.TechStacks.Remove(techStack);
        await SaveCatalogAsync(catalog);
    }

    // Roles
    public async Task<List<Role>> GetRolesAsync()
    {
        var catalog = await LoadCatalogAsync();
        return catalog.AllRoles.ToList();
    }

    public async Task<Role?> GetRoleAsync(string id)
    {
        var catalog = await LoadCatalogAsync();
        return catalog.AllRoles.FirstOrDefault(r => r.Id == id);
    }

    public async Task<List<Role>> GetGlobalRolesAsync()
    {
        var catalog = await LoadCatalogAsync();
        return catalog.GlobalRoles;
    }

    public async Task<List<Role>> GetRolesForTechStackAsync(string techStackId)
    {
        var catalog = await LoadCatalogAsync();
        var techStack = catalog.TechStacks.FirstOrDefault(ts => ts.Id == techStackId);
        return techStack?.Roles ?? [];
    }

    public async Task<List<Role>> GetAvailableRolesForEntryAsync(string? techStackId)
    {
        var catalog = await LoadCatalogAsync();
        return catalog.GetAvailableRolesForTechStack(techStackId);
    }

    public async Task SaveRoleAsync(Role role)
    {
        var catalog = await LoadCatalogAsync();

        if (string.IsNullOrEmpty(role.TechStackId))
        {
            // Save as global role
            var existing = catalog.GlobalRoles.FirstOrDefault(r => r.Id == role.Id);
            if (existing != null) catalog.GlobalRoles.Remove(existing);
            catalog.GlobalRoles.Add(role);
        }
        else
        {
            // Save into the matching techstack's roles list
            var techStack = catalog.TechStacks.FirstOrDefault(ts => ts.Id == role.TechStackId)
                ?? throw new InvalidOperationException($"TechStack '{role.TechStackId}' not found");

            var existing = techStack.Roles.FirstOrDefault(r => r.Id == role.Id);
            if (existing != null) techStack.Roles.Remove(existing);
            techStack.Roles.Add(role);
        }

        await SaveCatalogAsync(catalog);
    }

    public async Task DeleteRoleAsync(string id)
    {
        var catalog = await LoadCatalogAsync();

        // Check referential integrity first
        var referencingEntries = catalog.Catalog
            .Where(e => e.MediumEstimates.Any(m => m.RoleId == id))
            .Select(e => e.Name)
            .ToList();

        if (referencingEntries.Any())
        {
            throw new ReferentialIntegrityException("Role", id, referencingEntries);
        }

        // Try global roles
        var globalRole = catalog.GlobalRoles.FirstOrDefault(r => r.Id == id);
        if (globalRole != null)
        {
            catalog.GlobalRoles.Remove(globalRole);
            await SaveCatalogAsync(catalog);
            return;
        }

        // Try techstack roles
        foreach (var techStack in catalog.TechStacks)
        {
            var tsRole = techStack.Roles.FirstOrDefault(r => r.Id == id);
            if (tsRole != null)
            {
                techStack.Roles.Remove(tsRole);
                await SaveCatalogAsync(catalog);
                return;
            }
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
        var roleIds = catalog.AllRoles.Select(r => r.Id).ToHashSet();
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
        var roleIds = catalog.AllRoles.Select(r => r.Id).ToHashSet();
        return entry.MediumEstimates
            .Select(m => m.RoleId)
            .Where(id => !roleIds.Contains(id))
            .Distinct()
            .ToList();
    }
}
