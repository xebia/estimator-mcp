using System.ComponentModel;
using System.Text.Json;
using CatalogCli.Services;
using EstimatorMcp.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CatalogCli.Commands;

public class ImportCommand : Command<ImportCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--roles <PATH>")]
        [Description("Path to the roles TSV file")]
        public string RolesPath { get; set; } = string.Empty;

        [CommandOption("--entries <PATH>")]
        [Description("Path to the entries TSV file")]
        public string EntriesPath { get; set; } = string.Empty;

        [CommandOption("-o|--output <PATH>")]
        [Description("Output path for the catalog JSON file")]
        public string OutputPath { get; set; } = string.Empty;

        [CommandOption("--validate-only")]
        [Description("Validate without writing output")]
        public bool ValidateOnly { get; set; }

        [CommandOption("-f|--force")]
        [Description("Overwrite existing output file without prompting")]
        public bool Force { get; set; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(RolesPath))
                return ValidationResult.Error("Roles file path (--roles) is required");

            if (string.IsNullOrWhiteSpace(EntriesPath))
                return ValidationResult.Error("Entries file path (--entries) is required");

            if (string.IsNullOrWhiteSpace(OutputPath))
                return ValidationResult.Error("Output path (-o) is required");

            return ValidationResult.Success();
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var validationService = new ValidationService();
        var importer = new TsvImporter();
        var errors = new List<ValidationError>();

        // Check files exist
        if (!validationService.ValidateFiles(settings.RolesPath, settings.EntriesPath, out var fileErrors))
        {
            validationService.DisplayErrors(fileErrors);
            return 1;
        }

        // Import roles first
        AnsiConsole.MarkupLine("[dim]Reading roles.tsv...[/]");
        var roles = importer.ImportRoles(settings.RolesPath, errors);

        // Build valid role IDs set for entry validation
        var validRoleIds = new HashSet<string>(roles.Select(r => r.Id), StringComparer.Ordinal);

        // Import entries
        AnsiConsole.MarkupLine("[dim]Reading entries.tsv...[/]");
        var entries = importer.ImportEntries(settings.EntriesPath, validRoleIds, errors);

        // Display any errors
        if (errors.Count > 0)
        {
            validationService.DisplayErrors(errors);
            return 1;
        }

        // Show what was parsed
        AnsiConsole.WriteLine();
        var summaryTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Item")
            .AddColumn(new TableColumn("Count").Centered());

        summaryTable.AddRow("Roles", roles.Count.ToString());
        summaryTable.AddRow("Entries", entries.Count.ToString());

        AnsiConsole.Write(summaryTable);
        AnsiConsole.MarkupLine("\n[green]Validation passed[/]");

        // If validate-only, stop here
        if (settings.ValidateOnly)
        {
            AnsiConsole.MarkupLine("[dim]Validate-only mode - no output written[/]");
            return 0;
        }

        // Check for existing output file
        if (File.Exists(settings.OutputPath) && !settings.Force)
        {
            AnsiConsole.MarkupLine($"[yellow]Output file already exists: {Markup.Escape(settings.OutputPath)}[/]");
            if (!AnsiConsole.Confirm("Overwrite existing file?", false))
            {
                AnsiConsole.MarkupLine("[dim]Import cancelled[/]");
                return 0;
            }
        }

        // Create catalog
        var catalog = new CatalogData
        {
            Version = "1.0",
            Timestamp = DateTime.UtcNow,
            Roles = roles.OrderBy(r => r.Id, StringComparer.Ordinal).ToList(),
            Catalog = entries
                .OrderBy(e => e.Category, StringComparer.Ordinal)
                .ThenBy(e => e.Id, StringComparer.Ordinal)
                .ToList()
        };

        // Also sort MediumEstimates within each entry
        foreach (var entry in catalog.Catalog)
        {
            entry.MediumEstimates = entry.MediumEstimates
                .OrderBy(e => e.RoleId, StringComparer.Ordinal)
                .ToList();
        }

        // Write output
        try
        {
            var outputDirectory = Path.GetDirectoryName(settings.OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var json = JsonSerializer.Serialize(catalog, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            File.WriteAllText(settings.OutputPath, json);
            AnsiConsole.MarkupLine($"\n[green]Catalog written to: {Markup.Escape(settings.OutputPath)}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error writing output: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}
