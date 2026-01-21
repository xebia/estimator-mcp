using System.Globalization;
using System.Text;
using EstimatorMcp.Models;

namespace CatalogCli.Services;

public class TsvExporter
{
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public void ExportRoles(IEnumerable<Role> roles, string outputPath)
    {
        var sortedRoles = roles.OrderBy(r => r.Id, StringComparer.Ordinal);

        using var writer = new StreamWriter(outputPath, false, Utf8WithBom);

        // Header
        writer.WriteLine("Id\tName\tDescription\tCopilotMultiplier");

        // Data rows
        foreach (var role in sortedRoles)
        {
            writer.WriteLine(string.Join("\t",
                EscapeField(role.Id),
                EscapeField(role.Name),
                EscapeField(role.Description),
                role.CopilotMultiplier.ToString(CultureInfo.InvariantCulture)));
        }
    }

    public void ExportEntries(CatalogData catalog, string outputPath)
    {
        // Get all role IDs sorted alphabetically
        var roleIds = catalog.Roles
            .Select(r => r.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        // Sort entries by Category, then Id
        var sortedEntries = catalog.Catalog
            .OrderBy(e => e.Category, StringComparer.Ordinal)
            .ThenBy(e => e.Id, StringComparer.Ordinal);

        using var writer = new StreamWriter(outputPath, false, Utf8WithBom);

        // Header: fixed columns + new fields + role columns
        var headerParts = new List<string> { "Id", "Name", "Description", "Category", "TechStack", "Tags" };
        headerParts.AddRange(roleIds);
        writer.WriteLine(string.Join("\t", headerParts));

        // Data rows
        foreach (var entry in sortedEntries)
        {
            var rowParts = new List<string>
            {
                EscapeField(entry.Id),
                EscapeField(entry.Name),
                EscapeField(entry.Description),
                EscapeField(entry.Category),
                EscapeField(entry.TechStack ?? string.Empty),
                EscapeField(entry.Tags != null ? string.Join(";", entry.Tags) : string.Empty)  // Semicolon-separated
            };

            // Build a lookup of RoleId -> Hours for this entry
            var hoursLookup = entry.MediumEstimates.ToDictionary(e => e.RoleId, e => e.Hours);

            // Add hours for each role column (empty if no estimate)
            foreach (var roleId in roleIds)
            {
                if (hoursLookup.TryGetValue(roleId, out var hours))
                {
                    rowParts.Add(hours.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    rowParts.Add(string.Empty);
                }
            }

            writer.WriteLine(string.Join("\t", rowParts));
        }
    }

    private static string EscapeField(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // If field contains tabs, newlines, or quotes, wrap in quotes and escape internal quotes
        if (value.Contains('\t') || value.Contains('\n') || value.Contains('\r') || value.Contains('"'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
