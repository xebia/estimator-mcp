using System.ComponentModel;
using System.Text.Json;
using EstimatorMcp.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CatalogCli.Commands;

public class MigrateCommand : Command<MigrateCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-i|--input <PATH>")]
        [Description("Path to the input catalog JSON file (v1.0 or v2.0)")]
        public string InputPath { get; set; } = string.Empty;

        [CommandOption("-o|--output <PATH>")]
        [Description("Output path for the migrated catalog JSON file")]
        public string OutputPath { get; set; } = string.Empty;

        [CommandOption("-f|--force")]
        [Description("Overwrite existing output file without prompting")]
        public bool Force { get; set; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(InputPath))
                return ValidationResult.Error("Input path (-i) is required");

            if (string.IsNullOrWhiteSpace(OutputPath))
                return ValidationResult.Error("Output path (-o) is required");

            return ValidationResult.Success();
        }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!File.Exists(settings.InputPath))
        {
            AnsiConsole.MarkupLine($"[red]Error: Input file not found: {Markup.Escape(settings.InputPath)}[/]");
            return 1;
        }

        // Load and migrate
        CatalogData catalog;
        string originalVersion;
        try
        {
            var json = File.ReadAllText(settings.InputPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // Peek at original version before migration
            using var doc = JsonDocument.Parse(json);
            originalVersion = doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() ?? "1.0" : "1.0";

            catalog = CatalogData.DeserializeWithMigration(json, options);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error reading catalog: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        if (originalVersion == catalog.Version)
        {
            AnsiConsole.MarkupLine($"[yellow]Catalog is already version {originalVersion} — no migration needed.[/]");
            AnsiConsole.MarkupLine("[dim]Output will still be written in canonical v2.0 format.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"Migrating [yellow]{originalVersion}[/] → [green]{catalog.Version}[/]");
        }

        // Summary of what was loaded
        AnsiConsole.WriteLine();
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Item")
            .AddColumn(new TableColumn("Count").Centered());

        table.AddRow("TechStacks", catalog.TechStacks.Count.ToString());
        table.AddRow("Global Roles", catalog.GlobalRoles.Count.ToString());
        table.AddRow("TechStack-specific Roles", catalog.TechStacks.Sum(ts => ts.Roles.Count).ToString());
        table.AddRow("Catalog Entries", catalog.Catalog.Count.ToString());

        AnsiConsole.Write(table);

        // Check for existing output file
        if (File.Exists(settings.OutputPath) && !settings.Force)
        {
            AnsiConsole.MarkupLine($"\n[yellow]Output file already exists: {Markup.Escape(settings.OutputPath)}[/]");
            if (!AnsiConsole.Confirm("Overwrite?", false))
            {
                AnsiConsole.MarkupLine("[dim]Migration cancelled[/]");
                return 0;
            }
        }

        // Write migrated catalog
        try
        {
            var outputDirectory = Path.GetDirectoryName(settings.OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var outputJson = JsonSerializer.Serialize(catalog, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            File.WriteAllText(settings.OutputPath, outputJson);
            AnsiConsole.MarkupLine($"\n[green]Migrated catalog written to: {Markup.Escape(settings.OutputPath)}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error writing output: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}
