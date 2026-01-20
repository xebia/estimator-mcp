using System.ComponentModel;
using System.Text.Json;
using CatalogCli.Services;
using EstimatorMcp.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CatalogCli.Commands;

public class ExportCommand : Command<ExportCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-i|--input <PATH>")]
        [Description("Path to the input catalog JSON file")]
        public string InputPath { get; set; } = string.Empty;

        [CommandOption("-o|--output <DIRECTORY>")]
        [Description("Output directory for TSV files")]
        public string OutputDirectory { get; set; } = string.Empty;

        [CommandOption("-f|--force")]
        [Description("Overwrite existing files without prompting")]
        public bool Force { get; set; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(InputPath))
                return ValidationResult.Error("Input path (-i) is required");

            if (string.IsNullOrWhiteSpace(OutputDirectory))
                return ValidationResult.Error("Output directory (-o) is required");

            return ValidationResult.Success();
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        // Validate input file exists
        if (!File.Exists(settings.InputPath))
        {
            AnsiConsole.MarkupLine($"[red]Error: Input file not found: {Markup.Escape(settings.InputPath)}[/]");
            return 1;
        }

        // Create output directory if needed
        if (!Directory.Exists(settings.OutputDirectory))
        {
            Directory.CreateDirectory(settings.OutputDirectory);
            AnsiConsole.MarkupLine($"[dim]Created directory: {Markup.Escape(settings.OutputDirectory)}[/]");
        }

        var rolesPath = Path.Combine(settings.OutputDirectory, "roles.tsv");
        var entriesPath = Path.Combine(settings.OutputDirectory, "entries.tsv");

        // Check for existing files
        if (!settings.Force)
        {
            var existingFiles = new List<string>();
            if (File.Exists(rolesPath)) existingFiles.Add(rolesPath);
            if (File.Exists(entriesPath)) existingFiles.Add(entriesPath);

            if (existingFiles.Count > 0)
            {
                AnsiConsole.MarkupLine("[yellow]The following files already exist:[/]");
                foreach (var file in existingFiles)
                {
                    AnsiConsole.MarkupLine($"  - {Markup.Escape(file)}");
                }

                if (!AnsiConsole.Confirm("Overwrite existing files?", false))
                {
                    AnsiConsole.MarkupLine("[dim]Export cancelled[/]");
                    return 0;
                }
            }
        }

        // Load catalog JSON
        CatalogData catalog;
        try
        {
            var json = File.ReadAllText(settings.InputPath);
            catalog = JsonSerializer.Deserialize<CatalogData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize catalog");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error reading catalog JSON: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        // Export to TSV
        var exporter = new TsvExporter();

        try
        {
            exporter.ExportRoles(catalog.Roles, rolesPath);
            AnsiConsole.MarkupLine($"[green]Exported roles to: {Markup.Escape(rolesPath)}[/]");

            exporter.ExportEntries(catalog, entriesPath);
            AnsiConsole.MarkupLine($"[green]Exported entries to: {Markup.Escape(entriesPath)}[/]");

            // Summary
            AnsiConsole.WriteLine();
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Item")
                .AddColumn(new TableColumn("Count").Centered());

            table.AddRow("Roles", catalog.Roles.Count.ToString());
            table.AddRow("Entries", catalog.Catalog.Count.ToString());

            AnsiConsole.Write(table);

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error exporting: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}
