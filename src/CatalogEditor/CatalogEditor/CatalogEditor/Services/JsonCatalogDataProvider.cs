using System.Text.Json;
using System.Text.Json.Serialization;
using CatalogEditor.Models;

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
            catalog.Roles.Remove(role);
            await SaveCatalogAsync(catalog);
        }
    }

    // Countables
    public async Task<List<Countable>> GetCountablesAsync()
    {
        var catalog = await LoadCatalogAsync();
        return catalog.Countables;
    }

    public async Task<Countable?> GetCountableAsync(string id)
    {
        var catalog = await LoadCatalogAsync();
        return catalog.Countables.FirstOrDefault(c => c.Id == id);
    }

    public async Task SaveCountableAsync(Countable countable)
    {
        var catalog = await LoadCatalogAsync();
        var existing = catalog.Countables.FirstOrDefault(c => c.Id == countable.Id);
        if (existing != null)
        {
            catalog.Countables.Remove(existing);
        }
        catalog.Countables.Add(countable);
        await SaveCatalogAsync(catalog);
    }

    public async Task DeleteCountableAsync(string id)
    {
        var catalog = await LoadCatalogAsync();
        var countable = catalog.Countables.FirstOrDefault(c => c.Id == id);
        if (countable != null)
        {
            catalog.Countables.Remove(countable);
            await SaveCatalogAsync(catalog);
        }
    }

    // Features
    public async Task<List<Feature>> GetFeaturesAsync()
    {
        var catalog = await LoadCatalogAsync();
        return catalog.Features;
    }

    public async Task<Feature?> GetFeatureAsync(string id)
    {
        var catalog = await LoadCatalogAsync();
        return catalog.Features.FirstOrDefault(f => f.Id == id);
    }

    public async Task SaveFeatureAsync(Feature feature)
    {
        var catalog = await LoadCatalogAsync();
        var existing = catalog.Features.FirstOrDefault(f => f.Id == feature.Id);
        if (existing != null)
        {
            catalog.Features.Remove(existing);
        }
        catalog.Features.Add(feature);
        await SaveCatalogAsync(catalog);
    }

    public async Task DeleteFeatureAsync(string id)
    {
        var catalog = await LoadCatalogAsync();
        var feature = catalog.Features.FirstOrDefault(f => f.Id == id);
        if (feature != null)
        {
            catalog.Features.Remove(feature);
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
}
