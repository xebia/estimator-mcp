using System.Globalization;
using System.Text;
using EstimatorMcp.Models;

namespace EstimatorMcp.Web.Api;

/// <summary>
/// Serializes catalog data to TSV format compatible with CatalogCli import.
/// </summary>
public static class TsvSerializer
{
    public static string SerializeTechStacks(IEnumerable<TechStack> techStacks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id\tName\tDescription");

        foreach (var ts in techStacks.OrderBy(ts => ts.Id, StringComparer.Ordinal))
            sb.AppendLine(Join(ts.Id, ts.Name, ts.Description));

        return sb.ToString();
    }

    public static string SerializeRoles(IEnumerable<Role> roles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id\tName\tDescription\tCopilotMultiplier\tTechStackId");

        foreach (var role in roles.OrderBy(r => r.Id, StringComparer.Ordinal))
            sb.AppendLine(Join(role.Id, role.Name, role.Description,
                role.CopilotMultiplier.ToString(CultureInfo.InvariantCulture),
                role.TechStackId ?? string.Empty));

        return sb.ToString();
    }

    public static string SerializeEntries(CatalogData catalog)
    {
        var roleIds = catalog.AllRoles
            .Select(r => r.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        var header = new List<string> { "Id", "Name", "Description", "Category", "TechStack", "Tags" };
        header.AddRange(roleIds);
        sb.AppendLine(string.Join("\t", header));

        foreach (var entry in catalog.Catalog
            .OrderBy(e => e.Category, StringComparer.Ordinal)
            .ThenBy(e => e.Id, StringComparer.Ordinal))
        {
            var row = new List<string>
            {
                Escape(entry.Id),
                Escape(entry.Name),
                Escape(entry.Description),
                Escape(entry.Category),
                Escape(entry.TechStack ?? string.Empty),
                Escape(entry.Tags is { Count: > 0 } ? string.Join(";", entry.Tags) : string.Empty)
            };

            var hoursLookup = entry.MediumEstimates.ToDictionary(m => m.RoleId, m => m.Hours);
            foreach (var roleId in roleIds)
                row.Add(hoursLookup.TryGetValue(roleId, out var h) ? h.ToString(CultureInfo.InvariantCulture) : string.Empty);

            sb.AppendLine(string.Join("\t", row));
        }

        return sb.ToString();
    }

    private static string Join(params string[] fields) =>
        string.Join("\t", fields.Select(Escape));

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains('\t') || value.Contains('\n') || value.Contains('\r') || value.Contains('"'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
