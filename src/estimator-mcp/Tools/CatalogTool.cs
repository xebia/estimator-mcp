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

    [McpServerTool, Description("Returns the list of all catalog features with their IDs, names, descriptions, and categories. Optionally filter by category, tech stack, or tags.")]
    public async Task<string> GetCatalogFeatures(
        [Description("Optional category filter (e.g., 'feature', 'infrastructure'). If not provided, returns all features.")] string? category = null,
        [Description("Optional tech stack filter (e.g., 'salesforce', 'blazor-azure', 'nodejs', 'shared'). Filters features by technology platform.")] string? techStack = null,
        [Description("Optional tag filter (e.g., 'apex', 'frontend', 'api'). Returns features that include this tag.")] string? tag = null)
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

            var features = catalogData.Catalog.AsEnumerable();
            
            // Apply category filter if provided
            if (!string.IsNullOrWhiteSpace(category))
            {
                features = features.Where(f => f.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
                logger.LogInformation("[CatalogTool.GetCatalogFeatures] Applied category filter: {Category}", category);
            }

            // Apply tech stack filter if provided
            if (!string.IsNullOrWhiteSpace(techStack))
            {
                features = features.Where(f => f.TechStack != null && 
                                             f.TechStack.Equals(techStack, StringComparison.OrdinalIgnoreCase));
                logger.LogInformation("[CatalogTool.GetCatalogFeatures] Applied tech stack filter: {TechStack}", techStack);
            }

            // Apply tag filter if provided
            if (!string.IsNullOrWhiteSpace(tag))
            {
                features = features.Where(f => f.Tags != null && 
                                             f.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)));
                logger.LogInformation("[CatalogTool.GetCatalogFeatures] Applied tag filter: {Tag}", tag);
            }

            var featureList = features.ToList();

            var result = new
            {
                catalogFile = Path.GetFileName(catalogFile),
                timestamp = catalogData.Timestamp,
                totalFeatures = featureList.Count,
                appliedFilters = new
                {
                    category = category,
                    techStack = techStack,
                    tag = tag
                },
                features = featureList.Select(f => new
                {
                    id = f.Id,
                    name = f.Name,
                    description = f.Description,
                    category = f.Category,
                    techStack = f.TechStack,
                    tags = f.Tags
                }).ToList()
            };

            logger.LogInformation("[CatalogTool.GetCatalogFeatures] Returned {Count} features", featureList.Count);
            
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

    [McpServerTool, Description("Returns a summary of all available tech stacks in the catalog with feature counts and descriptions.")]
    public async Task<string> GetCatalogTechStacks()
    {
        try
        {
            var catalogFile = GetLatestCatalogFile();
            logger.LogInformation("[CatalogTool.GetCatalogTechStacks] Loading catalog from {FilePath}", catalogFile);

            var json = await File.ReadAllTextAsync(catalogFile);
            var catalogData = JsonSerializer.Deserialize<CatalogData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (catalogData == null)
            {
                return "Error: Failed to parse catalog data";
            }

            // Group features by tech stack and count them
            var techStackGroups = catalogData.Catalog
                .GroupBy(f => f.TechStack ?? "unspecified")
                .Select(g => new
                {
                    techStack = g.Key,
                    featureCount = g.Count(),
                    categories = g.Select(f => f.Category).Distinct().OrderBy(c => c).ToList(),
                    sampleFeatures = g.Take(3).Select(f => new { f.Id, f.Name }).ToList()
                })
                .OrderByDescending(g => g.featureCount)
                .ToList();

            var result = new
            {
                catalogFile = Path.GetFileName(catalogFile),
                timestamp = catalogData.Timestamp,
                totalTechStacks = techStackGroups.Count,
                totalFeatures = catalogData.Catalog.Count,
                techStacks = techStackGroups
            };

            logger.LogInformation("[CatalogTool.GetCatalogTechStacks] Returned {Count} tech stacks", techStackGroups.Count);
            
            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CatalogTool.GetCatalogTechStacks] Error loading catalog");
            return $"Error loading catalog: {ex.Message}";
        }
    }
}
