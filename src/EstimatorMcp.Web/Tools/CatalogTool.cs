using System.ComponentModel;
using System.Text.Json;
using EstimatorMcp.Web.Services;
using ModelContextProtocol.Server;

namespace EstimatorMcp.Web.Tools;

[McpServerToolType]
public sealed class CatalogTool(ICatalogDataProvider dataProvider, ILogger<CatalogTool> logger)
{
    [McpServerTool, Description("Returns the list of all catalog features with their IDs, names, descriptions, and categories. Optionally filter by category, tech stack, or tags.")]
    public async Task<string> GetCatalogFeatures(
        [Description("Optional category filter (e.g., 'feature', 'infrastructure'). If not provided, returns all features.")] string? category = null,
        [Description("Optional tech stack filter (e.g., 'salesforce', 'blazor-azure', 'nodejs', 'shared'). Filters features by technology platform.")] string? techStack = null,
        [Description("Optional tag filter (e.g., 'apex', 'frontend', 'api'). Returns features that include this tag.")] string? tag = null)
    {
        try
        {
            var catalogData = await dataProvider.LoadCatalogAsync();

            var features = catalogData.Catalog.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(category))
                features = features.Where(f => f.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(techStack))
                features = features.Where(f => f.TechStack != null && f.TechStack.Equals(techStack, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(tag))
                features = features.Where(f => f.Tags != null && f.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)));

            var featureList = features.ToList();

            var result = new
            {
                timestamp = catalogData.Timestamp,
                totalFeatures = featureList.Count,
                appliedFilters = new { category, techStack, tag },
                features = featureList.Select(f => new { f.Id, f.Name, f.Description, f.Category, f.TechStack, f.Tags }).ToList()
            };

            logger.LogInformation("[CatalogTool] Returned {Count} features", featureList.Count);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CatalogTool] Error loading catalog");
            return $"Error loading catalog: {ex.Message}";
        }
    }

    [McpServerTool, Description("Returns detailed information about all available tech stacks in the catalog, including their roles, feature counts, and descriptions.")]
    public async Task<string> GetCatalogTechStacks()
    {
        try
        {
            var catalogData = await dataProvider.LoadCatalogAsync();

            var techStackDetails = catalogData.TechStacks.Select(ts =>
            {
                var features = catalogData.Catalog.Where(f => f.TechStack == ts.Id).ToList();
                return new
                {
                    ts.Id, ts.Name, ts.Description,
                    roleCount = ts.Roles.Count,
                    roles = ts.Roles.Select(r => new { r.Id, r.Name, r.Description, r.CopilotMultiplier }).ToList(),
                    featureCount = features.Count,
                    categories = features.Select(f => f.Category).Distinct().OrderBy(c => c).ToList()
                };
            }).OrderBy(ts => ts.Name).ToList();

            var result = new
            {
                timestamp = catalogData.Timestamp,
                version = catalogData.Version,
                totalTechStacks = catalogData.TechStacks.Count,
                totalGlobalRoles = catalogData.GlobalRoles.Count,
                globalRoles = catalogData.GlobalRoles.Select(r => new { r.Id, r.Name, r.Description, r.CopilotMultiplier }).ToList(),
                techStacks = techStackDetails
            };

            logger.LogInformation("[CatalogTool] Returned {Count} tech stacks", techStackDetails.Count);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CatalogTool] Error loading catalog");
            return $"Error loading catalog: {ex.Message}";
        }
    }

    [McpServerTool, Description("Returns all roles available for a specific tech stack, including both the tech stack's specific roles and global roles that can be used with any tech stack.")]
    public async Task<string> GetRolesForTechStack(
        [Description("The tech stack ID to get roles for (e.g., 'salesforce', 'dotnet', 'azure')")] string techStackId)
    {
        try
        {
            var catalogData = await dataProvider.LoadCatalogAsync();
            var techStack = catalogData.TechStacks.FirstOrDefault(ts => ts.Id.Equals(techStackId, StringComparison.OrdinalIgnoreCase));

            if (techStack == null)
            {
                return JsonSerializer.Serialize(new
                {
                    error = $"Tech stack '{techStackId}' not found",
                    availableTechStacks = catalogData.TechStacks.Select(ts => ts.Id).ToList()
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var availableRoles = catalogData.GetAvailableRolesForTechStack(techStackId);

            var result = new
            {
                techStack = new { techStack.Id, techStack.Name, techStack.Description },
                totalRoles = availableRoles.Count,
                techStackSpecificRoles = techStack.Roles.Select(r => new { r.Id, r.Name, r.Description, r.CopilotMultiplier, scope = "techstack" }).ToList(),
                globalRoles = catalogData.GlobalRoles.Select(r => new { r.Id, r.Name, r.Description, r.CopilotMultiplier, scope = "global" }).ToList()
            };

            logger.LogInformation("[CatalogTool] Returned {Count} roles for {TechStackId}", availableRoles.Count, techStackId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CatalogTool] Error loading catalog");
            return $"Error loading catalog: {ex.Message}";
        }
    }
}
