using CatalogCli.Services;
using EstimatorMcp.Models;
using Xunit;

namespace EstimatorMcp.Tests.Cli;

public class TsvRoundtripTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public TsvRoundtripTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static CatalogData BuildCatalog()
    {
        var catalog = new CatalogData
        {
            TechStacks =
            [
                new TechStack
                {
                    Id = "salesforce", Name = "Salesforce", Description = "Salesforce CRM",
                    Roles = [ new Role { Id = "sf-dev", Name = "SF Developer", CopilotMultiplier = 0.7m } ]
                }
            ],
            GlobalRoles =
            [
                new Role { Id = "em", Name = "Engagement Manager", CopilotMultiplier = 1.0m },
                new Role { Id = "qa", Name = "QA Engineer", CopilotMultiplier = 0.65m }
            ],
            Catalog =
            [
                new CatalogEntry
                {
                    Id = "sf-apex", Name = "Apex Class", Description = "Custom Apex class", Category = "feature",
                    TechStack = "salesforce", Tags = ["apex", "backend"],
                    MediumEstimates = [ new MediumEstimate { RoleId = "sf-dev", Hours = 16 }, new MediumEstimate { RoleId = "em", Hours = 2 } ]
                },
                new CatalogEntry
                {
                    Id = "shared-ci", Name = "CI/CD Pipeline", Description = "Build pipeline", Category = "devops",
                    Tags = ["devops"],
                    MediumEstimates = [ new MediumEstimate { RoleId = "qa", Hours = 8 } ]
                }
            ]
        };
        catalog.PopulateRoleTechStackIds();
        return catalog;
    }

    // ── Export ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Export_WritesTechStacksTsv()
    {
        var catalog = BuildCatalog();
        var exporter = new TsvExporter();

        exporter.ExportTechStacks(catalog.TechStacks, Path.Combine(_tempDir, "techstacks.tsv"));

        var lines = File.ReadAllLines(Path.Combine(_tempDir, "techstacks.tsv"));
        Assert.Equal("Id\tName\tDescription", lines[0]);
        Assert.Contains(lines, l => l.StartsWith("salesforce\t"));
    }

    [Fact]
    public void Export_WritesRolesTsvWithTechStackIdColumn()
    {
        var catalog = BuildCatalog();
        var exporter = new TsvExporter();

        exporter.ExportRoles(catalog.AllRoles, Path.Combine(_tempDir, "roles.tsv"));

        var lines = File.ReadAllLines(Path.Combine(_tempDir, "roles.tsv"));
        Assert.Equal("Id\tName\tDescription\tCopilotMultiplier\tTechStackId", lines[0]);

        var sfDevLine = lines.Single(l => l.StartsWith("sf-dev\t"));
        Assert.EndsWith("\tsalesforce", sfDevLine);

        var emLine = lines.Single(l => l.StartsWith("em\t"));
        Assert.EndsWith("\t", emLine); // TechStackId is empty for global roles
    }

    [Fact]
    public void Export_WritesEntriesTsvWithTechStackAndTagsColumns()
    {
        var catalog = BuildCatalog();
        var exporter = new TsvExporter();

        exporter.ExportEntries(catalog, Path.Combine(_tempDir, "entries.tsv"));

        var lines = File.ReadAllLines(Path.Combine(_tempDir, "entries.tsv"));
        var header = lines[0].Split('\t');
        Assert.Equal("Id", header[0]);
        Assert.Equal("TechStack", header[4]);
        Assert.Equal("Tags", header[5]);

        Assert.Contains(lines, l => l.Contains("salesforce"));
        Assert.Contains(lines, l => l.Contains("apex;backend"));
    }

    // ── Import ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Import_ParsesTechStacksFile()
    {
        var content = "Id\tName\tDescription\nsalesforce\tSalesforce\tCRM platform\n";
        File.WriteAllText(Path.Combine(_tempDir, "techstacks.tsv"), content);

        var errors = new List<ValidationError>();
        var result = new TsvImporter().ImportTechStacks(Path.Combine(_tempDir, "techstacks.tsv"), errors);

        Assert.Empty(errors);
        Assert.Single(result);
        Assert.Equal("salesforce", result[0].Id);
        Assert.Equal("Salesforce", result[0].Name);
    }

    [Fact]
    public void Import_ParsesRolesWithTechStackId()
    {
        var content = "Id\tName\tDescription\tCopilotMultiplier\tTechStackId\nsf-dev\tSF Developer\tApex dev\t0.7\tsalesforce\nem\tEM\t\t1.0\t\n";
        File.WriteAllText(Path.Combine(_tempDir, "roles.tsv"), content);

        var validTechStacks = new HashSet<string> { "salesforce" };
        var errors = new List<ValidationError>();
        var result = new TsvImporter().ImportRoles(Path.Combine(_tempDir, "roles.tsv"), validTechStacks, errors);

        Assert.Empty(errors);
        Assert.Equal(2, result.Count);

        var sfDev = result.Single(r => r.Id == "sf-dev");
        Assert.Equal("salesforce", sfDev.TechStackId);

        var em = result.Single(r => r.Id == "em");
        Assert.Null(em.TechStackId);
    }

    [Fact]
    public void Import_ReportsErrorForUnknownTechStackIdOnRole()
    {
        var content = "Id\tName\tDescription\tCopilotMultiplier\tTechStackId\nsf-dev\tSF Developer\t\t0.7\tunknown-stack\n";
        File.WriteAllText(Path.Combine(_tempDir, "roles.tsv"), content);

        var validTechStacks = new HashSet<string> { "salesforce" };
        var errors = new List<ValidationError>();
        new TsvImporter().ImportRoles(Path.Combine(_tempDir, "roles.tsv"), validTechStacks, errors);

        Assert.Single(errors);
        Assert.Contains("unknown-stack", errors[0].Message);
    }

    // ── Roundtrip ──────────────────────────────────────────────────────────────

    [Fact]
    public void ExportThenImport_PreservesAllData()
    {
        var catalog = BuildCatalog();
        var exporter = new TsvExporter();
        var importer = new TsvImporter();

        var techStacksPath = Path.Combine(_tempDir, "techstacks.tsv");
        var rolesPath = Path.Combine(_tempDir, "roles.tsv");
        var entriesPath = Path.Combine(_tempDir, "entries.tsv");

        exporter.ExportTechStacks(catalog.TechStacks, techStacksPath);
        exporter.ExportRoles(catalog.AllRoles, rolesPath);
        exporter.ExportEntries(catalog, entriesPath);

        var errors = new List<ValidationError>();
        var importedTechStacks = importer.ImportTechStacks(techStacksPath, errors);
        var validTechStackIds = importedTechStacks.Select(ts => ts.Id).ToHashSet();
        var importedRoles = importer.ImportRoles(rolesPath, validTechStackIds, errors);
        var validRoleIds = importedRoles.Select(r => r.Id).ToHashSet();
        var importedEntries = importer.ImportEntries(entriesPath, validRoleIds, errors);

        Assert.Empty(errors);

        // TechStacks
        Assert.Single(importedTechStacks);
        Assert.Equal("salesforce", importedTechStacks[0].Id);

        // Roles
        Assert.Equal(3, importedRoles.Count); // sf-dev, em, qa
        Assert.Contains(importedRoles, r => r.Id == "sf-dev" && r.TechStackId == "salesforce");
        Assert.Contains(importedRoles, r => r.Id == "em" && r.TechStackId == null);

        // Entries
        Assert.Equal(2, importedEntries.Count);
        var apex = importedEntries.Single(e => e.Id == "sf-apex");
        Assert.Equal("salesforce", apex.TechStack);
        Assert.Equal(["apex", "backend"], apex.Tags);
        Assert.Equal(2, apex.MediumEstimates.Count);
    }
}
