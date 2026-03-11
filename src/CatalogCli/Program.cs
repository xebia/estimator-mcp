using CatalogCli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("catalogcli");

    config.AddCommand<ConfigureCommand>("configure")
        .WithDescription("Save server URL and API token to local config")
        .WithExample("configure", "--server", "https://myserver.com", "--token", "YOUR_TOKEN")
        .WithExample("configure");

    config.AddCommand<ExportCommand>("export")
        .WithDescription("Export catalog from server to TSV files for Excel editing")
        .WithExample("export", "-o", "./output/")
        .WithExample("export", "-o", "./output/", "--server", "https://myserver.com", "--token", "YOUR_TOKEN");

    config.AddCommand<ImportCommand>("import")
        .WithDescription("Import TSV files and upload catalog to server")
        .WithExample("import", "--techstacks", "techstacks.tsv", "--roles", "roles.tsv", "--entries", "entries.tsv")
        .WithExample("import", "--techstacks", "techstacks.tsv", "--roles", "roles.tsv", "--entries", "entries.tsv", "--validate-only");

    config.AddCommand<MigrateCommand>("migrate")
        .WithDescription("Migrate a v1.0 catalog JSON to v2.0 format (local file operation)")
        .WithExample("migrate", "-i", "catalog-v1.json", "-o", "catalog-v2.json");
});

return app.Run(args);
