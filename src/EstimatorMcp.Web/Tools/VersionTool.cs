using System.ComponentModel;
using System.Text.Json;
using EstimatorMcp.Web.Services;
using ModelContextProtocol.Server;

namespace EstimatorMcp.Web.Tools;

[McpServerToolType]
public sealed class VersionTool(ICatalogDataProvider dataProvider, ILogger<VersionTool> logger)
{
    [McpServerTool, Description("Returns the version of this Estimator MCP server and of the catalog data it is serving. Use this to check whether the server or its catalog has been updated, and to report the exact build when raising an issue. The server version follows semantic versioning and describes the MCP tool surface; the catalog timestamp changes independently whenever estimate data is edited, so identical inputs can produce different estimates without the server version changing.")]
    public async Task<string> GetServerVersion()
    {
        var version = ServerVersionInfo.Current;

        // Catalog data is versioned separately from the server (see VERSIONING.md),
        // so report both: an agent comparing estimates across time needs to know
        // which one moved.
        string? catalogSchemaVersion = null;
        DateTime? catalogTimestamp = null;
        int? featureCount = null;
        string? catalogError = null;

        try
        {
            var catalog = await dataProvider.LoadCatalogAsync();
            catalogSchemaVersion = catalog.Version;
            catalogTimestamp = catalog.Timestamp;
            featureCount = catalog.Catalog.Count;
        }
        catch (Exception ex)
        {
            // The server version is still worth returning even if the catalog is
            // unreachable — that combination is itself a useful diagnostic.
            logger.LogError(ex, "[VersionTool] Error loading catalog for version report");
            catalogError = ex.Message;
        }

        var result = new
        {
            server = new
            {
                version = version.Full,
                semanticVersion = version.Semantic,
                commit = version.Commit,
            },
            catalog = new
            {
                schemaVersion = catalogSchemaVersion,
                timestamp = catalogTimestamp,
                featureCount,
                error = catalogError,
            },
        };

        logger.LogInformation("[VersionTool] Reported server version {Version}", version.Full);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }
}
