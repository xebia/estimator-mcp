using System.Text.Json;
using EstimatorMcp.Models;
using Xunit;

namespace EstimatorMcp.Tests.Models;

public class CatalogDataMigrationTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    // ── v1.0 migration ─────────────────────────────────────────────────────────

    [Fact]
    public void DeserializeWithMigration_V1_MovesRolesToGlobalRoles()
    {
        var json = """
            {
              "version": "1.0",
              "timestamp": "2025-01-01T00:00:00Z",
              "roles": [
                { "id": "dev", "name": "Developer", "copilotMultiplier": 0.7 },
                { "id": "em",  "name": "Engagement Manager", "copilotMultiplier": 1.0 }
              ],
              "catalog": []
            }
            """;

        var result = CatalogData.DeserializeWithMigration(json, Options);

        Assert.Equal("2.0", result.Version);
        Assert.Equal(2, result.GlobalRoles.Count);
        Assert.Contains(result.GlobalRoles, r => r.Id == "dev");
        Assert.Contains(result.GlobalRoles, r => r.Id == "em");
        Assert.Empty(result.TechStacks);
    }

    [Fact]
    public void DeserializeWithMigration_V1_ExtractsTechStacksFromEntries()
    {
        var json = """
            {
              "version": "1.0",
              "roles": [],
              "catalog": [
                { "id": "f1", "name": "F1", "techStack": "salesforce", "mediumEstimates": [] },
                { "id": "f2", "name": "F2", "techStack": "dotnet",     "mediumEstimates": [] },
                { "id": "f3", "name": "F3", "techStack": "salesforce", "mediumEstimates": [] },
                { "id": "f4", "name": "F4", "mediumEstimates": [] }
              ]
            }
            """;

        var result = CatalogData.DeserializeWithMigration(json, Options);

        Assert.Equal(2, result.TechStacks.Count);
        Assert.Contains(result.TechStacks, ts => ts.Id == "salesforce");
        Assert.Contains(result.TechStacks, ts => ts.Id == "dotnet");
    }

    [Fact]
    public void DeserializeWithMigration_V1_TechStacksHaveEmptyRoleLists()
    {
        var json = """
            {
              "version": "1.0",
              "roles": [],
              "catalog": [
                { "id": "f1", "name": "F1", "techStack": "salesforce", "mediumEstimates": [] }
              ]
            }
            """;

        var result = CatalogData.DeserializeWithMigration(json, Options);

        var sf = result.TechStacks.Single(ts => ts.Id == "salesforce");
        Assert.Empty(sf.Roles);
    }

    // ── v2.0 pass-through ──────────────────────────────────────────────────────

    [Fact]
    public void DeserializeWithMigration_V2_LoadsWithoutMigration()
    {
        var json = """
            {
              "version": "2.0",
              "techStacks": [
                {
                  "id": "salesforce",
                  "name": "Salesforce",
                  "roles": [
                    { "id": "sf-dev", "name": "SF Developer", "copilotMultiplier": 0.7 }
                  ]
                }
              ],
              "globalRoles": [
                { "id": "em", "name": "Engagement Manager", "copilotMultiplier": 1.0 }
              ],
              "catalog": []
            }
            """;

        var result = CatalogData.DeserializeWithMigration(json, Options);

        Assert.Equal("2.0", result.Version);
        Assert.Single(result.TechStacks);
        Assert.Single(result.GlobalRoles);
        Assert.Equal("em", result.GlobalRoles[0].Id);
    }

    [Fact]
    public void DeserializeWithMigration_V2_PopulatesTechStackIdOnRoles()
    {
        var json = """
            {
              "version": "2.0",
              "techStacks": [
                {
                  "id": "salesforce",
                  "name": "Salesforce",
                  "roles": [
                    { "id": "sf-dev", "name": "SF Developer", "copilotMultiplier": 0.7 }
                  ]
                }
              ],
              "globalRoles": [
                { "id": "em", "name": "Engagement Manager", "copilotMultiplier": 1.0 }
              ],
              "catalog": []
            }
            """;

        var result = CatalogData.DeserializeWithMigration(json, Options);

        var sfDev = result.TechStacks[0].Roles[0];
        Assert.Equal("salesforce", sfDev.TechStackId);

        var em = result.GlobalRoles[0];
        Assert.Null(em.TechStackId);
    }
}
