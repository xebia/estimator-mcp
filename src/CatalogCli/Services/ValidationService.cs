using Spectre.Console;

namespace CatalogCli.Services;

public class ValidationService
{
    public void DisplayErrors(List<ValidationError> errors)
    {
        if (errors.Count == 0)
            return;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("File").Centered())
            .AddColumn(new TableColumn("Row").Centered())
            .AddColumn("Error");

        foreach (var error in errors.OrderBy(e => e.File).ThenBy(e => e.Row))
        {
            var rowDisplay = error.Row == 0 ? "-" : error.Row.ToString();
            table.AddRow(
                $"[yellow]{error.File}[/]",
                $"[cyan]{rowDisplay}[/]",
                $"[red]{Markup.Escape(error.Message)}[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[red]Found {errors.Count} validation error(s)[/]");
    }

    public bool ValidateFiles(string rolesPath, string entriesPath, out List<ValidationError> errors)
    {
        errors = new List<ValidationError>();

        if (!File.Exists(rolesPath))
        {
            errors.Add(new ValidationError(Path.GetFileName(rolesPath), 0, $"File not found: {rolesPath}"));
        }

        if (!File.Exists(entriesPath))
        {
            errors.Add(new ValidationError(Path.GetFileName(entriesPath), 0, $"File not found: {entriesPath}"));
        }

        return errors.Count == 0;
    }
}
