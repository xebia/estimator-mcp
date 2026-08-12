using System.Text.Json;
using EstimatorMcp.Models;
using EstimatorMcp.Web.Services;
using EstimatorMcp.Web.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EstimatorMcp.Tests;

public class VersionToolTests
{
    [Fact]
    public async Task GetServerVersion_ReportsServerAndCatalogVersions()
    {
        var catalog = new CatalogData
        {
            Version = "2.0",
            Timestamp = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc),
            Catalog = [new CatalogEntry { Id = "a" }, new CatalogEntry { Id = "b" }],
        };
        var tool = new VersionTool(new StubCatalogProvider(catalog), NullLogger<VersionTool>.Instance);

        using var doc = JsonDocument.Parse(await tool.GetServerVersion());
        var root = doc.RootElement;

        var server = root.GetProperty("server");
        Assert.False(string.IsNullOrWhiteSpace(server.GetProperty("version").GetString()));
        Assert.NotEqual("unknown", server.GetProperty("semanticVersion").GetString());

        var reported = root.GetProperty("catalog");
        Assert.Equal("2.0", reported.GetProperty("schemaVersion").GetString());
        Assert.Equal(2, reported.GetProperty("featureCount").GetInt32());
        Assert.Equal(catalog.Timestamp, reported.GetProperty("timestamp").GetDateTime());
        Assert.Equal(JsonValueKind.Null, reported.GetProperty("error").ValueKind);
    }

    [Fact]
    public async Task GetServerVersion_WhenCatalogUnavailable_StillReportsServerVersion()
    {
        // An agent asking "what am I talking to?" should get an answer even when
        // the catalog is broken — that pairing is itself the diagnostic.
        var tool = new VersionTool(
            new StubCatalogProvider(new IOException("catalog offline")),
            NullLogger<VersionTool>.Instance);

        using var doc = JsonDocument.Parse(await tool.GetServerVersion());
        var root = doc.RootElement;

        Assert.NotEqual("unknown", root.GetProperty("server").GetProperty("semanticVersion").GetString());

        var reported = root.GetProperty("catalog");
        Assert.Contains("catalog offline", reported.GetProperty("error").GetString());
        Assert.Equal(JsonValueKind.Null, reported.GetProperty("schemaVersion").ValueKind);
    }

    /// <summary>Serves one catalog (or one failure); every other member is unused here.</summary>
    private sealed class StubCatalogProvider(object result) : ICatalogDataProvider
    {
        public Task<CatalogData> LoadCatalogAsync() => result switch
        {
            CatalogData data => Task.FromResult(data),
            Exception ex => Task.FromException<CatalogData>(ex),
            _ => throw new InvalidOperationException(),
        };

        private static T NotUsed<T>() => throw new NotSupportedException("Not exercised by these tests.");

        public Task SaveCatalogAsync(CatalogData catalog) => NotUsed<Task>();
        public Task<List<TechStack>> GetTechStacksAsync() => NotUsed<Task<List<TechStack>>>();
        public Task<TechStack?> GetTechStackAsync(string id) => NotUsed<Task<TechStack?>>();
        public Task SaveTechStackAsync(TechStack techStack) => NotUsed<Task>();
        public Task DeleteTechStackAsync(string id) => NotUsed<Task>();
        public Task<List<Role>> GetRolesAsync() => NotUsed<Task<List<Role>>>();
        public Task<Role?> GetRoleAsync(string id) => NotUsed<Task<Role?>>();
        public Task<List<Role>> GetGlobalRolesAsync() => NotUsed<Task<List<Role>>>();
        public Task<List<Role>> GetRolesForTechStackAsync(string techStackId) => NotUsed<Task<List<Role>>>();
        public Task<List<Role>> GetAvailableRolesForEntryAsync(string? techStackId) => NotUsed<Task<List<Role>>>();
        public Task SaveRoleAsync(Role role) => NotUsed<Task>();
        public Task DeleteRoleAsync(string id) => NotUsed<Task>();
        public Task<List<CatalogEntry>> GetCatalogEntriesAsync() => NotUsed<Task<List<CatalogEntry>>>();
        public Task<CatalogEntry?> GetCatalogEntryAsync(string id) => NotUsed<Task<CatalogEntry?>>();
        public Task SaveCatalogEntryAsync(CatalogEntry entry) => NotUsed<Task>();
        public Task DeleteCatalogEntryAsync(string id) => NotUsed<Task>();
        public Task<bool> IsRoleReferencedAsync(string roleId) => NotUsed<Task<bool>>();
        public Task<List<CatalogEntry>> GetEntriesReferencingRoleAsync(string roleId) => NotUsed<Task<List<CatalogEntry>>>();
        public Task<List<string>> ValidateRoleReferencesAsync(CatalogEntry entry) => NotUsed<Task<List<string>>>();
    }
}
