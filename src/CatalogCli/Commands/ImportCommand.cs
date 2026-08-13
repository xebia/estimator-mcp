using System.ComponentModel;
using CatalogCli.Services;
using EstimatorMcp.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CatalogCli.Commands;

public class ImportCommand : AsyncCommand<ImportCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--techstacks <PATH>")]
        [Description("Path to the techstacks TSV file")]
        public string TechStacksPath { get; set; } = string.Empty;

        [CommandOption("--roles <PATH>")]
        [Description("Path to the roles TSV file")]
        public string RolesPath { get; set; } = string.Empty;

        [CommandOption("--entries <PATH>")]
        [Description("Path to the entries TSV file")]
        public string EntriesPath { get; set; } = string.Empty;

        [CommandOption("--validate-only")]
        [Description("Validate TSV files without uploading to the server")]
        public bool ValidateOnly { get; set; }

        [CommandOption("--server <URL>")]
        [Description("API server URL (overrides config and ESTIMATOR_API_URL)")]
        public string? Server { get; set; }

        [CommandOption("--token <TOKEN>")]
        [Description("Bearer token (overrides config and ESTIMATOR_API_TOKEN)")]
        public string? Token { get; set; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(TechStacksPath))
                return ValidationResult.Error("TechStacks file path (--techstacks) is required");

            if (string.IsNullOrWhiteSpace(RolesPath))
                return ValidationResult.Error("Roles file path (--roles) is required");

            if (string.IsNullOrWhiteSpace(EntriesPath))
                return ValidationResult.Error("Entries file path (--entries) is required");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var validationService = new ValidationService();
        var importer = new TsvImporter();
        var errors = new List<ValidationError>();

        // Check files exist
        if (!File.Exists(settings.TechStacksPath))
        {
            AnsiConsole.MarkupLine($"[red]TechStacks file not found: {Markup.Escape(settings.TechStacksPath)}[/]");
            return 1;
        }
        if (!File.Exists(settings.RolesPath))
        {
            AnsiConsole.MarkupLine($"[red]Roles file not found: {Markup.Escape(settings.RolesPath)}[/]");
            return 1;
        }
        if (!File.Exists(settings.EntriesPath))
        {
            AnsiConsole.MarkupLine($"[red]Entries file not found: {Markup.Escape(settings.EntriesPath)}[/]");
            return 1;
        }

        // Parse TSV files
        AnsiConsole.MarkupLine("[dim]Reading techstacks.tsv...[/]");
        var techStacks = importer.ImportTechStacks(settings.TechStacksPath, errors);
        var validTechStackIds = new HashSet<string>(techStacks.Select(ts => ts.Id), StringComparer.Ordinal);

        AnsiConsole.MarkupLine("[dim]Reading roles.tsv...[/]");
        var roles = importer.ImportRoles(settings.RolesPath, validTechStackIds, errors);
        var validRoleIds = new HashSet<string>(roles.Select(r => r.Id), StringComparer.Ordinal);

        AnsiConsole.MarkupLine("[dim]Reading entries.tsv...[/]");
        var entries = importer.ImportEntries(settings.EntriesPath, validRoleIds, errors);

        if (errors.Count > 0)
        {
            validationService.DisplayErrors(errors);
            return 1;
        }

        // Organize into catalog structure
        var globalRoles = new List<Role>();
        var techStackRolesMap = new Dictionary<string, List<Role>>();

        foreach (var role in roles)
        {
            if (string.IsNullOrEmpty(role.TechStackId))
                globalRoles.Add(role);
            else
            {
                if (!techStackRolesMap.ContainsKey(role.TechStackId))
                    techStackRolesMap[role.TechStackId] = [];
                techStackRolesMap[role.TechStackId].Add(role);
            }
        }

        foreach (var techStack in techStacks)
        {
            if (techStackRolesMap.TryGetValue(techStack.Id, out var tsRoles))
                techStack.Roles = tsRoles.OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
        }

        var catalog = new CatalogData
        {
            Version = "2.0",
            Timestamp = DateTime.UtcNow,
            TechStacks = techStacks.OrderBy(ts => ts.Id, StringComparer.Ordinal).ToList(),
            GlobalRoles = globalRoles.OrderBy(r => r.Id, StringComparer.Ordinal).ToList(),
            Catalog = entries
                .OrderBy(e => e.Category, StringComparer.Ordinal)
                .ThenBy(e => e.Id, StringComparer.Ordinal)
                .ToList()
        };

        foreach (var entry in catalog.Catalog)
            entry.MediumEstimates = entry.MediumEstimates.OrderBy(e => e.RoleId, StringComparer.Ordinal).ToList();

        // Summary
        AnsiConsole.WriteLine();
        var summaryTable = new Spectre.Console.Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Item")
            .AddColumn(new TableColumn("Count").Centered());

        summaryTable.AddRow("TechStacks", techStacks.Count.ToString());
        summaryTable.AddRow("Roles (Total)", roles.Count.ToString());
        summaryTable.AddRow("  - Global", globalRoles.Count.ToString());
        summaryTable.AddRow("  - TechStack-specific", (roles.Count - globalRoles.Count).ToString());
        summaryTable.AddRow("Entries", entries.Count.ToString());
        AnsiConsole.Write(summaryTable);
        AnsiConsole.MarkupLine("\n[green]Validation passed[/]");

        if (settings.ValidateOnly)
        {
            AnsiConsole.MarkupLine("[dim]Validate-only mode — nothing uploaded[/]");
            return 0;
        }

        // Resolve API credentials
        var (serverUrl, token) = CliConfig.Resolve(settings.Server, settings.Token);

        if (string.IsNullOrEmpty(serverUrl))
        {
            AnsiConsole.MarkupLine("[red]Server URL is not configured.[/]");
            AnsiConsole.MarkupLine("[dim]Run: catalogcli configure --server <URL> --token <TOKEN>[/]");
            return 1;
        }

        if (string.IsNullOrEmpty(token))
        {
            AnsiConsole.MarkupLine("[red]API token is not configured.[/]");
            AnsiConsole.MarkupLine("[dim]Run: catalogcli configure --server <URL> --token <TOKEN>[/]");
            return 1;
        }

        // Upload to server
        using var client = new ApiClient(serverUrl, token);

        try
        {
            AnsiConsole.MarkupLine($"[dim]Uploading to {Markup.Escape(serverUrl)}...[/]");
            await client.ImportCatalogAsync(catalog);
            AnsiConsole.MarkupLine("[green]Catalog imported successfully.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Upload failed: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}
