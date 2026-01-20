using CatalogCli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("catalogcli");

    config.AddCommand<ExportCommand>("export")
        .WithDescription("Export catalog JSON to TSV files for Excel editing")
        .WithExample("export", "-i", "catalog.json", "-o", "./output/");

    config.AddCommand<ImportCommand>("import")
        .WithDescription("Import TSV files back to catalog JSON format")
        .WithExample("import", "--roles", "roles.tsv", "--entries", "entries.tsv", "-o", "catalog.json");
});

return app.Run(args);
