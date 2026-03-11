using System.ComponentModel;
using System.Text.Json;
using EstimatorMcp.Web.Services;
using ModelContextProtocol.Server;

namespace EstimatorMcp.Web.Tools;

[McpServerToolType]
public sealed class CalculateEstimateTool(ICatalogDataProvider dataProvider, ILogger<CalculateEstimateTool> logger)
{
    public class FeatureEstimateInput
    {
        [Description("The feature ID from the catalog (must match exactly with a feature ID from GetCatalogFeatures)")]
        public string FeatureId { get; set; } = string.Empty;

        [Description("The T-shirt size for this feature. MUST be one of these exact values: XS, S, M, L, XL.")]
        public string Size { get; set; } = string.Empty;
    }

    private static readonly Dictionary<string, decimal> SizeMultipliers = new(StringComparer.OrdinalIgnoreCase)
    {
        { "XS", 1m / 5m }, { "S", 2m / 5m }, { "M", 1m }, { "L", 8m / 5m }, { "XL", 13m / 5m }
    };

    [McpServerTool, Description("Calculates time estimates for a list of features with specified T-shirt sizes. Returns total hours per role and detailed breakdown per feature, including Copilot productivity multipliers. Input must be a JSON array where each item has 'featureId' (exact ID from catalog) and 'size' (must be exactly one of: XS, S, M, L, XL).")]
    public async Task<string> CalculateEstimate(
        [Description("Array of feature estimates. Each item must have: 'featureId' (string) and 'size' (string, XS/S/M/L/XL). Example: [{\"featureId\": \"basic-crud\", \"size\": \"M\"}]")]
        List<FeatureEstimateInput> features)
    {
        try
        {
            if (features == null || features.Count == 0)
                return "Error: No features provided.";

            var catalogData = await dataProvider.LoadCatalogAsync();
            var catalogLookup = catalogData.Catalog.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
            var rolesLookup = catalogData.AllRoles.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);

            var errors = new List<string>();
            foreach (var feature in features)
            {
                if (string.IsNullOrWhiteSpace(feature.FeatureId))
                    errors.Add("One or more features have missing featureId");
                else if (!catalogLookup.ContainsKey(feature.FeatureId))
                    errors.Add($"Feature ID '{feature.FeatureId}' not found in catalog");

                if (string.IsNullOrWhiteSpace(feature.Size))
                    errors.Add($"Feature '{feature.FeatureId}' has missing size");
                else if (!SizeMultipliers.ContainsKey(feature.Size))
                    errors.Add($"Feature '{feature.FeatureId}' has invalid size '{feature.Size}'. Must be one of: XS, S, M, L, XL");
            }

            if (errors.Count > 0)
                return $"Validation errors:\n{string.Join("\n", errors)}";

            var featureEstimates = new List<object>();
            var roleNonAiTotals = new Dictionary<string, decimal>();
            var roleAiAdjustedTotals = new Dictionary<string, decimal>();

            foreach (var feature in features)
            {
                var catalogEntry = catalogLookup[feature.FeatureId];
                var sizeMultiplier = SizeMultipliers[feature.Size.ToUpper()];
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
                        sizeMultiplier,
                        nonAiHours = Math.Round(nonAiHours, 1),
                        copilotMultiplier = role.CopilotMultiplier,
                        aiAdjustedHours = Math.Round(aiAdjustedHours, 1)
                    };

                    roleNonAiTotals.TryAdd(mediumEstimate.RoleId, 0);
                    roleAiAdjustedTotals.TryAdd(mediumEstimate.RoleId, 0);
                    roleNonAiTotals[mediumEstimate.RoleId] += nonAiHours;
                    roleAiAdjustedTotals[mediumEstimate.RoleId] += aiAdjustedHours;
                }

                featureEstimates.Add(new { featureId = feature.FeatureId, featureName = catalogEntry.Name, size = feature.Size.ToUpper(), catalogEntry.Category, roleEstimates = featureRoleEstimates });
            }

            var roleSummaries = roleNonAiTotals.Keys.Select(roleId =>
            {
                var role = rolesLookup[roleId];
                var nonAiHours = Math.Round(roleNonAiTotals[roleId], 1);
                var aiAdjustedHours = Math.Round(roleAiAdjustedTotals[roleId], 1);
                return new { roleId, roleName = role.Name, nonAiHours, nonAiDays = Math.Round(nonAiHours / 8m, 1), copilotMultiplier = role.CopilotMultiplier, aiAdjustedHours, aiAdjustedDays = Math.Round(aiAdjustedHours / 8m, 1) };
            }).OrderByDescending(r => r.aiAdjustedHours).ToList();

            var totalNonAiHours = Math.Round(roleNonAiTotals.Values.Sum(), 1);
            var totalAiAdjustedHours = Math.Round(roleAiAdjustedTotals.Values.Sum(), 1);

            var result = new
            {
                timestamp = catalogData.Timestamp,
                summary = new
                {
                    totalFeatures = features.Count,
                    roleSummaries,
                    overallNonAiHours = totalNonAiHours,
                    overallNonAiDays = Math.Round(totalNonAiHours / 8m, 1),
                    overallAiAdjustedHours = totalAiAdjustedHours,
                    overallAiAdjustedDays = Math.Round(totalAiAdjustedHours / 8m, 1)
                },
                featureDetails = featureEstimates
            };

            logger.LogInformation("[CalculateEstimateTool] Calculated estimates for {Count} features, total {Hours} hours", features.Count, totalAiAdjustedHours);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CalculateEstimateTool] Error calculating estimate");
            return $"Error calculating estimate: {ex.Message}";
        }
    }
}
