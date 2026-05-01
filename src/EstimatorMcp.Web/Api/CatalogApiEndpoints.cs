using System.Text.Json;
using EstimatorMcp.Models;
using EstimatorMcp.Web.Services;

namespace EstimatorMcp.Web.Api;

public static class CatalogApiEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void MapCatalogApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/catalog").RequireAuthorization("BearerOnly");

        // GET /api/catalog/export — full catalog as JSON
        group.MapGet("/export", async (ICatalogDataProvider provider) =>
        {
            var catalog = await provider.LoadCatalogAsync();
            return Results.Json(catalog, JsonOptions);
        });

        // POST /api/catalog/import — accepts CatalogData JSON, saves as new version
        group.MapPost("/import", async (CatalogData catalog, ICatalogDataProvider provider) =>
        {
            if (catalog.Catalog is null || catalog.GlobalRoles is null || catalog.TechStacks is null)
                return Results.BadRequest(new { error = "Invalid catalog structure." });

            catalog.PopulateRoleTechStackIds();

            try
            {
                await provider.SaveCatalogAsync(catalog);
                return Results.Ok(new { message = "Catalog imported successfully.", entries = catalog.Catalog.Count });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // GET /api/catalog/export/tsv/techstacks
        group.MapGet("/export/tsv/techstacks", async (ICatalogDataProvider provider) =>
        {
            var techStacks = await provider.GetTechStacksAsync();
            var tsv = TsvSerializer.SerializeTechStacks(techStacks);
            return Results.Text(tsv, "text/tab-separated-values; charset=utf-8");
        });

        // GET /api/catalog/export/tsv/roles
        group.MapGet("/export/tsv/roles", async (ICatalogDataProvider provider) =>
        {
            var roles = await provider.GetRolesAsync();
            var tsv = TsvSerializer.SerializeRoles(roles);
            return Results.Text(tsv, "text/tab-separated-values; charset=utf-8");
        });

        // GET /api/catalog/export/tsv/entries
        group.MapGet("/export/tsv/entries", async (ICatalogDataProvider provider) =>
        {
            var catalog = await provider.LoadCatalogAsync();
            var tsv = TsvSerializer.SerializeEntries(catalog);
            return Results.Text(tsv, "text/tab-separated-values; charset=utf-8");
        });
    }
}
