using System.ComponentModel;
using CatalogCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CatalogCli.Commands;

public class ExportCommand : AsyncCommand<ExportCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-o|--output <DIRECTORY>")]
        [Description("Output directory for TSV files")]
        public string OutputDirectory { get; set; } = string.Empty;

        [CommandOption("-f|--force")]
        [Description("Overwrite existing files without prompting")]
        public bool Force { get; set; }

        [CommandOption("--server <URL>")]
        [Description("API server URL (overrides config and ESTIMATOR_API_URL)")]
        public string? Server { get; set; }

        [CommandOption("--token <TOKEN>")]
        [Description("Bearer token (overrides config and ESTIMATOR_API_TOKEN)")]
        public string? Token { get; set; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(OutputDirectory))
                return ValidationResult.Error("Output directory (-o) is required");

            return ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
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

        // Create output directory if needed
        if (!Directory.Exists(settings.OutputDirectory))
        {
            Directory.CreateDirectory(settings.OutputDirectory);
            AnsiConsole.MarkupLine($"[dim]Created directory: {Markup.Escape(settings.OutputDirectory)}[/]");
        }

        var techStacksPath = Path.Combine(settings.OutputDirectory, "techstacks.tsv");
        var rolesPath = Path.Combine(settings.OutputDirectory, "roles.tsv");
        var entriesPath = Path.Combine(settings.OutputDirectory, "entries.tsv");

        // Check for existing files
        if (!settings.Force)
        {
            var existingFiles = new List<string>();
            if (File.Exists(techStacksPath)) existingFiles.Add(techStacksPath);
            if (File.Exists(rolesPath)) existingFiles.Add(rolesPath);
            if (File.Exists(entriesPath)) existingFiles.Add(entriesPath);

            if (existingFiles.Count > 0)
            {
                AnsiConsole.MarkupLine("[yellow]The following files already exist:[/]");
                foreach (var file in existingFiles)
                    AnsiConsole.MarkupLine($"  - {Markup.Escape(file)}");

                if (!AnsiConsole.Confirm("Overwrite existing files?", false))
                {
                    AnsiConsole.MarkupLine("[dim]Export cancelled[/]");
                    return 0;
                }
            }
        }

        using var client = new ApiClient(serverUrl, token);

        try
        {
            AnsiConsole.MarkupLine($"[dim]Connecting to {Markup.Escape(serverUrl)}...[/]");

            var techStacksTsv = await client.GetTsvAsync("api/catalog/export/tsv/techstacks");
            File.WriteAllText(techStacksPath, techStacksTsv);
            AnsiConsole.MarkupLine($"[green]Exported techstacks to: {Markup.Escape(techStacksPath)}[/]");

            var rolesTsv = await client.GetTsvAsync("api/catalog/export/tsv/roles");
            File.WriteAllText(rolesPath, rolesTsv);
            AnsiConsole.MarkupLine($"[green]Exported roles to: {Markup.Escape(rolesPath)}[/]");

            var entriesTsv = await client.GetTsvAsync("api/catalog/export/tsv/entries");
            File.WriteAllText(entriesPath, entriesTsv);
            AnsiConsole.MarkupLine($"[green]Exported entries to: {Markup.Escape(entriesPath)}[/]");

            // Count lines (excluding header) for summary
            var techStackCount = CountDataRows(techStacksTsv);
            var roleCount = CountDataRows(rolesTsv);
            var entryCount = CountDataRows(entriesTsv);

            AnsiConsole.WriteLine();
            var table = new Spectre.Console.Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Item")
                .AddColumn(new TableColumn("Count").Centered());

            table.AddRow("TechStacks", techStackCount.ToString());
            table.AddRow("Roles", roleCount.ToString());
            table.AddRow("Entries", entryCount.ToString());
            AnsiConsole.Write(table);

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }

    private static int CountDataRows(string tsv) =>
        tsv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length - 1; // subtract header
}
