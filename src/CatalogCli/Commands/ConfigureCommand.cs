using System.ComponentModel;
using CatalogCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CatalogCli.Commands;

public class ConfigureCommand : Command<ConfigureCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--server <URL>")]
        [Description("Base URL of the Estimator MCP server (e.g. https://myserver.com)")]
        public string? Server { get; set; }

        [CommandOption("--token <TOKEN>")]
        [Description("Bearer token for API authentication")]
        public string? Token { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var config = CliConfig.Load();

        if (settings.Server is null && settings.Token is null)
        {
            // Show current settings
            AnsiConsole.MarkupLine("[bold]Current configuration[/]");
            AnsiConsole.MarkupLine($"  Config file : [dim]{Markup.Escape(CliConfig.ConfigFilePath)}[/]");
            AnsiConsole.MarkupLine($"  Server URL  : {(string.IsNullOrEmpty(config.ServerUrl) ? "[dim](not set)[/]" : Markup.Escape(config.ServerUrl))}");
            AnsiConsole.MarkupLine($"  Token       : {(string.IsNullOrEmpty(config.Token) ? "[dim](not set)[/]" : "[dim]***[/]")}");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Override per-command: --server URL  --token TOKEN[/]");
            AnsiConsole.MarkupLine("[dim]Or via environment:   ESTIMATOR_API_URL  ESTIMATOR_API_TOKEN[/]");
            return 0;
        }

        if (settings.Server is not null)
            config.ServerUrl = settings.Server;

        if (settings.Token is not null)
            config.Token = settings.Token;

        config.Save();
        AnsiConsole.MarkupLine($"[green]Configuration saved to: {Markup.Escape(CliConfig.ConfigFilePath)}[/]");
        return 0;
    }
}
