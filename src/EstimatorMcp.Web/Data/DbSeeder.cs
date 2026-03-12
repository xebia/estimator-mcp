using System.Text.Json;
using EstimatorMcp.Models;
using EstimatorMcp.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace EstimatorMcp.Web.Data;

public static class DbSeeder
{
    public static async Task SeedFromJsonIfEmptyAsync(IServiceProvider services, IConfiguration configuration)
    {
        var context = services.GetRequiredService<AppDbContext>();

        if (await context.TechStacks.AnyAsync() || await context.CatalogEntries.AnyAsync())
            return;

        var catalogPath = configuration["CatalogSeedPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "data", "catalogs");

        if (!Directory.Exists(catalogPath))
        {
            // Try the path relative to the solution for development
            var devPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "CatalogEditor", "CatalogEditor", "CatalogEditor", "data", "catalogs");
            devPath = Path.GetFullPath(devPath);
            if (Directory.Exists(devPath))
                catalogPath = devPath;
            else
                return;
        }

        var files = Directory.GetFiles(catalogPath, "catalog-*.json")
            .Where(f => !f.EndsWith("temp.json", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f)
            .ToList();

        if (files.Count == 0)
            return;

        var json = await File.ReadAllTextAsync(files[0]);
        var catalogData = CatalogData.DeserializeWithMigration(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var provider = services.GetRequiredService<ICatalogDataProvider>();
        await provider.SaveCatalogAsync(catalogData);
    }
}
