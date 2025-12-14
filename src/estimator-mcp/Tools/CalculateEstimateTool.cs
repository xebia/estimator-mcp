using System.ComponentModel;
using System.Text.Json;
using EstimatorMcp.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace EstimatorMcp.Tools;

/// <summary>
/// MCP tool that calculates project estimates based on catalog features and T-shirt sizes.
/// </summary>
[McpServerToolType]
public sealed class CalculateEstimateTool(IConfiguration configuration, ILogger<CalculateEstimateTool> logger)
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

    /// <summary>
    /// Input model for a feature estimation request.
    /// </summary>
    public class FeatureEstimateInput
    {
        [Description("The feature ID from the catalog (must match exactly with a feature ID from get_catalog_features)")]
        public string FeatureId { get; set; } = string.Empty;

        [Description("The T-shirt size for this feature. MUST be one of these exact values: XS, S, M, L, XL. No other values are allowed.")]
        public string Size { get; set; } = string.Empty;
    }

    [McpServerTool, Description("Calculates time estimates for a list of features with specified T-shirt sizes. Returns total hours per role and detailed breakdown per feature, including Copilot productivity multipliers. Input must be a JSON array where each item has 'featureId' (exact ID from catalog) and 'size' (must be exactly one of: XS, S, M, L, XL). Example: [{\"featureId\": \"basic-crud\", \"size\": \"M\"}, {\"featureId\": \"api-integration\", \"size\": \"S\"}]")]
    public async Task<string> CalculateEstimate(
        [Description("Array of feature estimates. Each item must have: 'featureId' (string, exact match from catalog) and 'size' (string, must be exactly one of: XS, S, M, L, XL). Example: [{\"featureId\": \"basic-crud\", \"size\": \"M\"}, {\"featureId\": \"api-integration\", \"size\": \"S\"}]")]
        List<FeatureEstimateInput> features)
    {
        try
        {
            // Validate input
            if (features == null || features.Count == 0)
            {
                return "Error: No features provided. Please provide at least one feature with featureId and size.";
            }

            // Load catalog
            var catalogFile = GetLatestCatalogFile();
            logger.LogInformation("[CalculateEstimateTool.CalculateEstimate] Loading catalog from {FilePath}", catalogFile);

            var json = await File.ReadAllTextAsync(catalogFile);
            var catalogData = JsonSerializer.Deserialize<CatalogData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (catalogData == null)
            {
                return "Error: Failed to parse catalog data";
            }

            // Build lookup dictionaries
            var catalogLookup = catalogData.Catalog.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
            var rolesLookup = catalogData.Roles.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);

            // Validate all feature IDs and sizes
            var validSizes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "XS", "S", "M", "L", "XL" };
            var errors = new List<string>();

            foreach (var feature in features)
            {
                if (string.IsNullOrWhiteSpace(feature.FeatureId))
                {
                    errors.Add("One or more features have missing featureId");
                }
                else if (!catalogLookup.ContainsKey(feature.FeatureId))
                {
                    errors.Add($"Feature ID '{feature.FeatureId}' not found in catalog");
                }

                if (string.IsNullOrWhiteSpace(feature.Size))
                {
                    errors.Add($"Feature '{feature.FeatureId}' has missing size");
                }
                else if (!validSizes.Contains(feature.Size))
                {
                    errors.Add($"Feature '{feature.FeatureId}' has invalid size '{feature.Size}'. Must be exactly one of: XS, S, M, L, XL");
                }
            }

            if (errors.Count > 0)
            {
                return $"Validation errors:\n{string.Join("\n", errors)}";
            }

            // Calculate size multipliers
            var sizeMultipliers = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                { "XS", 0.5m },
                { "S", 0.75m },
                { "M", 1.0m },
                { "L", 1.5m },
                { "XL", 2.0m }
            };

            // Calculate estimates
            var featureEstimates = new List<object>();
            var roleNonAiTotals = new Dictionary<string, decimal>();
            var roleAiAdjustedTotals = new Dictionary<string, decimal>();

            foreach (var feature in features)
            {
                var catalogEntry = catalogLookup[feature.FeatureId];
                var sizeMultiplier = sizeMultipliers[feature.Size.ToUpper()];
                
                var featureRoleEstimates = new Dictionary<string, object>();

                foreach (var mediumEstimate in catalogEntry.MediumEstimates)
                {
                    var role = rolesLookup[mediumEstimate.RoleId];
                    var nonAiHours = mediumEstimate.Hours * sizeMultiplier;
                    var aiAdjustedHours = nonAiHours * role.CopilotMultiplier;
                    
                    featureRoleEstimates[mediumEstimate.RoleId] = new
                    {
                        roleName = role.Name,
                        baseHours = mediumEstimate.Hours,
                        sizeMultiplier = sizeMultiplier,
                        nonAiHours = Math.Round(nonAiHours, 1),
                        copilotMultiplier = role.CopilotMultiplier,
                        aiAdjustedHours = Math.Round(aiAdjustedHours, 1)
                    };

                    if (!roleNonAiTotals.ContainsKey(mediumEstimate.RoleId))
                    {
                        roleNonAiTotals[mediumEstimate.RoleId] = 0;
                        roleAiAdjustedTotals[mediumEstimate.RoleId] = 0;
                    }
                    roleNonAiTotals[mediumEstimate.RoleId] += nonAiHours;
                    roleAiAdjustedTotals[mediumEstimate.RoleId] += aiAdjustedHours;
                }

                featureEstimates.Add(new
                {
                    featureId = feature.FeatureId,
                    featureName = catalogEntry.Name,
                    size = feature.Size.ToUpper(),
                    category = catalogEntry.Category,
                    roleEstimates = featureRoleEstimates
                });
            }

            // Build role summaries
            var roleSummaries = roleNonAiTotals.Keys.Select(roleId =>
            {
                var role = rolesLookup[roleId];
                var nonAiHours = Math.Round(roleNonAiTotals[roleId], 1);
                var aiAdjustedHours = Math.Round(roleAiAdjustedTotals[roleId], 1);
                var nonAiDays = Math.Round(nonAiHours / 8m, 1);
                var aiAdjustedDays = Math.Round(aiAdjustedHours / 8m, 1);
                
                return new
                {
                    roleId = roleId,
                    roleName = role.Name,
                    nonAiHours = nonAiHours,
                    nonAiDays = nonAiDays,
                    copilotMultiplier = role.CopilotMultiplier,
                    aiAdjustedHours = aiAdjustedHours,
                    aiAdjustedDays = aiAdjustedDays
                };
            }).OrderByDescending(r => r.aiAdjustedHours).ToList();

            var totalNonAiHours = Math.Round(roleNonAiTotals.Values.Sum(), 1);
            var totalAiAdjustedHours = Math.Round(roleAiAdjustedTotals.Values.Sum(), 1);

            // Build result
            var result = new
            {
                catalogFile = Path.GetFileName(catalogFile),
                timestamp = catalogData.Timestamp,
                summary = new
                {
                    totalFeatures = features.Count,
                    roleSummaries = roleSummaries,
                    overallNonAiHours = totalNonAiHours,
                    overallNonAiDays = Math.Round(totalNonAiHours / 8m, 1),
                    overallAiAdjustedHours = totalAiAdjustedHours,
                    overallAiAdjustedDays = Math.Round(totalAiAdjustedHours / 8m, 1)
                },
                featureDetails = featureEstimates
            };

            logger.LogInformation("[CalculateEstimateTool.CalculateEstimate] Calculated estimates for {Count} features, total {Hours} hours", 
                features.Count, 
                result.summary.overallTotalHours);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CalculateEstimateTool.CalculateEstimate] Error calculating estimate");
            return $"Error calculating estimate: {ex.Message}";
        }
    }
}
