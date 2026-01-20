using System.Globalization;
using System.Text;
using EstimatorMcp.Models;

namespace CatalogCli.Services;

public class TsvImporter
{
    public List<Role> ImportRoles(string filePath, List<ValidationError> errors)
    {
        var roles = new List<Role>();
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);

        if (lines.Length == 0)
        {
            errors.Add(new ValidationError("roles.tsv", 0, "File is empty"));
            return roles;
        }

        var header = ParseTsvLine(lines[0]);
        var expectedHeader = new[] { "Id", "Name", "Description", "CopilotMultiplier" };

        if (!ValidateHeader(header, expectedHeader, "roles.tsv", errors))
            return roles;

        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 1; i < lines.Length; i++)
        {
            var rowNumber = i + 1; // 1-based for user display
            var line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseTsvLine(line);

            if (fields.Length < 4)
            {
                errors.Add(new ValidationError("roles.tsv", rowNumber, $"Expected 4 columns, found {fields.Length}"));
                continue;
            }

            var id = fields[0].Trim();
            var name = fields[1].Trim();
            var description = fields[2].Trim();
            var multiplierStr = fields[3].Trim();

            // Validate required fields
            if (string.IsNullOrEmpty(id))
            {
                errors.Add(new ValidationError("roles.tsv", rowNumber, "Id is required"));
                continue;
            }

            if (string.IsNullOrEmpty(name))
            {
                errors.Add(new ValidationError("roles.tsv", rowNumber, $"Name is required for role '{id}'"));
            }

            // Check for duplicate IDs
            if (!seenIds.Add(id))
            {
                errors.Add(new ValidationError("roles.tsv", rowNumber, $"Duplicate role Id '{id}'"));
                continue;
            }

            // Validate and parse CopilotMultiplier
            if (!decimal.TryParse(multiplierStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var multiplier))
            {
                errors.Add(new ValidationError("roles.tsv", rowNumber, $"Invalid CopilotMultiplier '{multiplierStr}' for role '{id}'"));
                continue;
            }

            roles.Add(new Role
            {
                Id = id,
                Name = name,
                Description = description,
                CopilotMultiplier = multiplier
            });
        }

        return roles;
    }

    public List<CatalogEntry> ImportEntries(string filePath, HashSet<string> validRoleIds, List<ValidationError> errors)
    {
        var entries = new List<CatalogEntry>();
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);

        if (lines.Length == 0)
        {
            errors.Add(new ValidationError("entries.tsv", 0, "File is empty"));
            return entries;
        }

        var header = ParseTsvLine(lines[0]);

        // Validate minimum header columns
        if (header.Length < 4)
        {
            errors.Add(new ValidationError("entries.tsv", 1, "Header must have at least 4 columns (Id, Name, Description, Category)"));
            return entries;
        }

        // Fixed columns
        var fixedColumns = new[] { "Id", "Name", "Description", "Category" };
        for (int i = 0; i < fixedColumns.Length; i++)
        {
            if (!string.Equals(header[i], fixedColumns[i], StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ValidationError("entries.tsv", 1, $"Expected column '{fixedColumns[i]}' at position {i + 1}, found '{header[i]}'"));
            }
        }

        // Role columns start at index 4
        var roleColumns = header.Skip(4).ToArray();

        // Validate role columns exist in roles.tsv
        foreach (var roleColumn in roleColumns)
        {
            if (!validRoleIds.Contains(roleColumn))
            {
                errors.Add(new ValidationError("entries.tsv", 1, $"Role column '{roleColumn}' not found in roles.tsv"));
            }
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 1; i < lines.Length; i++)
        {
            var rowNumber = i + 1;
            var line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseTsvLine(line);

            if (fields.Length < 4)
            {
                errors.Add(new ValidationError("entries.tsv", rowNumber, $"Expected at least 4 columns, found {fields.Length}"));
                continue;
            }

            var id = fields[0].Trim();
            var name = fields[1].Trim();
            var description = fields[2].Trim();
            var category = fields[3].Trim();

            // Validate required fields
            if (string.IsNullOrEmpty(id))
            {
                errors.Add(new ValidationError("entries.tsv", rowNumber, "Id is required"));
                continue;
            }

            if (string.IsNullOrEmpty(name))
            {
                errors.Add(new ValidationError("entries.tsv", rowNumber, $"Name is required for entry '{id}'"));
            }

            // Check for duplicate IDs
            if (!seenIds.Add(id))
            {
                errors.Add(new ValidationError("entries.tsv", rowNumber, $"Duplicate entry Id '{id}'"));
                continue;
            }

            var estimates = new List<MediumEstimate>();

            // Parse role estimates
            for (int j = 0; j < roleColumns.Length; j++)
            {
                var fieldIndex = 4 + j;
                var hoursStr = fieldIndex < fields.Length ? fields[fieldIndex].Trim() : string.Empty;

                if (string.IsNullOrEmpty(hoursStr))
                    continue;

                if (!decimal.TryParse(hoursStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var hours))
                {
                    errors.Add(new ValidationError("entries.tsv", rowNumber, $"Invalid hours '{hoursStr}' for role '{roleColumns[j]}' in entry '{id}'"));
                    continue;
                }

                estimates.Add(new MediumEstimate
                {
                    RoleId = roleColumns[j],
                    Hours = hours
                });
            }

            entries.Add(new CatalogEntry
            {
                Id = id,
                Name = name,
                Description = description,
                Category = category,
                MediumEstimates = estimates
            });
        }

        return entries;
    }

    private static bool ValidateHeader(string[] actual, string[] expected, string filename, List<ValidationError> errors)
    {
        if (actual.Length < expected.Length)
        {
            errors.Add(new ValidationError(filename, 1, $"Expected {expected.Length} columns, found {actual.Length}"));
            return false;
        }

        for (int i = 0; i < expected.Length; i++)
        {
            if (!string.Equals(actual[i], expected[i], StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ValidationError(filename, 1, $"Expected column '{expected[i]}' at position {i + 1}, found '{actual[i]}'"));
                return false;
            }
        }

        return true;
    }

    private static string[] ParseTsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < line.Length)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Check for escaped quote
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i += 2;
                    }
                    else
                    {
                        inQuotes = false;
                        i++;
                    }
                }
                else
                {
                    current.Append(c);
                    i++;
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                    i++;
                }
                else if (c == '\t')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                    i++;
                }
                else
                {
                    current.Append(c);
                    i++;
                }
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}

public record ValidationError(string File, int Row, string Message);
