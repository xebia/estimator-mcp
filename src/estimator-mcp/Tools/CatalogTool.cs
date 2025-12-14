using System.ComponentModel;
using System.Text.Json;
using EstimatorMcp.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace EstimatorMcp.Tools;

/// <summary>
/// MCP tool that provides catalog features for project estimation.
/// </summary>
[McpServerToolType]
public sealed class CatalogTool(IConfiguration configuration, ILogger<CatalogTool> logger)
{
    private readonly string _catalogPath = GetCatalogPath(configuration);

    private static string GetCatalogPath(IConfiguration configuration)
    {
        // Check environment variable first
        var envPath = Environment.GetEnvironmentVariable("ESTIMATOR_CATALOG_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            return envPath;
        }

        // Check configuration
        var configPath = configuration["EstimatorMcp:CatalogPath"];
        if (!string.IsNullOrEmpty(configPath))
        {
            return configPath;
        }

        // Default: resolve relative to the CatalogEditor project location
        // From bin/Debug/net10.0, go up to repo root, then to CatalogEditor
        var assemblyLocation = AppContext.BaseDirectory;
        return Path.Combine(assemblyLocation, "..", "..", "..", "..", "..", "src", "CatalogEditor", "CatalogEditor", "CatalogEditor", "data", "catalogs");
    }

    private string GetLatestCatalogFile()
    {
        var fullPath = Path.GetFullPath(_catalogPath);
        
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Catalog directory not found at {fullPath}");
        }

        var catalogFiles = Directory.GetFiles(fullPath, "catalog-*.json")
            .Where(f => !f.EndsWith("temp.json", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f)
            .ToList();

        if (catalogFiles.Count == 0)
        {
            throw new FileNotFoundException($"No catalog files found in {fullPath}");
        }

        return catalogFiles[0];
    }

    [McpServerTool, Description("Returns the list of all catalog features with their IDs, names, descriptions, and categories. Optionally filter by category.")]
    public async Task<string> GetCatalogFeatures(
        [Description("Optional category filter (e.g., 'feature', 'infrastructure'). If not provided, returns all features.")] string? category = null)
    {
        try
        {
            var catalogFile = GetLatestCatalogFile();
            logger.LogInformation("[CatalogTool.GetCatalogFeatures] Loading catalog from {FilePath}", catalogFile);

            var json = await File.ReadAllTextAsync(catalogFile);
            var catalogData = JsonSerializer.Deserialize<CatalogData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (catalogData == null)
            {
                return "Error: Failed to parse catalog data";
            }

            var features = catalogData.Catalog;
            
            // Apply category filter if provided
            if (!string.IsNullOrEmpty(category))
            {
                features = features.Where(f => f.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var result = new
            {
                catalogFile = Path.GetFileName(catalogFile),
                timestamp = catalogData.Timestamp,
                totalFeatures = features.Count,
                features = features.Select(f => new
                {
                    id = f.Id,
                    name = f.Name,
                    description = f.Description,
                    category = f.Category
                }).ToList()
            };

            logger.LogInformation("[CatalogTool.GetCatalogFeatures] Returned {Count} features", features.Count);
            
            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CatalogTool.GetCatalogFeatures] Error loading catalog");
            return $"Error loading catalog: {ex.Message}";
        }
    }
}
