using EstimatorMcp.Models;
using Xunit;

namespace EstimatorMcp.Tests.Models;

public class RoleResolutionTests
{
    private static CatalogData BuildCatalog()
    {
        var catalog = new CatalogData
        {
            GlobalRoles =
            [
                new Role { Id = "em",  Name = "Engagement Manager", CopilotMultiplier = 1.0m },
                new Role { Id = "qa",  Name = "QA Engineer",        CopilotMultiplier = 0.65m }
            ],
            TechStacks =
            [
                new TechStack
                {
                    Id = "salesforce",
                    Name = "Salesforce",
                    Roles =
                    [
                        new Role { Id = "sf-dev",   Name = "SF Developer",  CopilotMultiplier = 0.7m },
                        new Role { Id = "sf-admin", Name = "SF Admin",       CopilotMultiplier = 0.85m }
                    ]
                },
                new TechStack
                {
                    Id = "dotnet",
                    Name = ".NET",
                    Roles =
                    [
                        new Role { Id = "dotnet-dev", Name = ".NET Developer", CopilotMultiplier = 0.55m }
                    ]
                }
            ]
        };
        catalog.PopulateRoleTechStackIds();
        return catalog;
    }

    // ── AllRoles ───────────────────────────────────────────────────────────────

    [Fact]
    public void AllRoles_ReturnsGlobalAndAllTechStackRoles()
    {
        var catalog = BuildCatalog();
        var all = catalog.AllRoles.ToList();

        Assert.Equal(5, all.Count);
        Assert.Contains(all, r => r.Id == "em");
        Assert.Contains(all, r => r.Id == "qa");
        Assert.Contains(all, r => r.Id == "sf-dev");
        Assert.Contains(all, r => r.Id == "sf-admin");
        Assert.Contains(all, r => r.Id == "dotnet-dev");
    }

    // ── GetAvailableRolesForTechStack ──────────────────────────────────────────

    [Fact]
    public void GetAvailableRolesForTechStack_Salesforce_ReturnsSalesforceAndGlobalRoles()
    {
        var catalog = BuildCatalog();
        var roles = catalog.GetAvailableRolesForTechStack("salesforce");

        Assert.Equal(4, roles.Count); // 2 global + 2 salesforce
        Assert.Contains(roles, r => r.Id == "em");
        Assert.Contains(roles, r => r.Id == "qa");
        Assert.Contains(roles, r => r.Id == "sf-dev");
        Assert.Contains(roles, r => r.Id == "sf-admin");
        Assert.DoesNotContain(roles, r => r.Id == "dotnet-dev");
    }

    [Fact]
    public void GetAvailableRolesForTechStack_Dotnet_ReturnsDotnetAndGlobalRoles()
    {
        var catalog = BuildCatalog();
        var roles = catalog.GetAvailableRolesForTechStack("dotnet");

        Assert.Equal(3, roles.Count); // 2 global + 1 dotnet
        Assert.Contains(roles, r => r.Id == "dotnet-dev");
        Assert.DoesNotContain(roles, r => r.Id == "sf-dev");
    }

    [Fact]
    public void GetAvailableRolesForTechStack_NullTechStack_ReturnsOnlyGlobalRoles()
    {
        var catalog = BuildCatalog();
        var roles = catalog.GetAvailableRolesForTechStack(null);

        Assert.Equal(2, roles.Count);
        Assert.Contains(roles, r => r.Id == "em");
        Assert.Contains(roles, r => r.Id == "qa");
    }

    [Fact]
    public void GetAvailableRolesForTechStack_UnknownTechStack_ReturnsOnlyGlobalRoles()
    {
        var catalog = BuildCatalog();
        var roles = catalog.GetAvailableRolesForTechStack("unknown-stack");

        Assert.Equal(2, roles.Count);
        Assert.All(roles, r => Assert.Null(r.TechStackId));
    }

    // ── PopulateRoleTechStackIds ───────────────────────────────────────────────

    [Fact]
    public void PopulateRoleTechStackIds_SetsCorrectTechStackIdOnNestedRoles()
    {
        var catalog = BuildCatalog();

        var sfDev = catalog.TechStacks.Single(ts => ts.Id == "salesforce").Roles.Single(r => r.Id == "sf-dev");
        Assert.Equal("salesforce", sfDev.TechStackId);

        var dotnetDev = catalog.TechStacks.Single(ts => ts.Id == "dotnet").Roles.Single(r => r.Id == "dotnet-dev");
        Assert.Equal("dotnet", dotnetDev.TechStackId);
    }

    [Fact]
    public void PopulateRoleTechStackIds_SetsNullOnGlobalRoles()
    {
        var catalog = BuildCatalog();

        Assert.All(catalog.GlobalRoles, r => Assert.Null(r.TechStackId));
    }
}
