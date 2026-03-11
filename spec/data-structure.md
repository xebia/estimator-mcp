# Data Structure Specification

## Overview

The MCP Server stores all configuration and catalog data in a single **JSON file** persisted on disk. The file is named with an ISO 8601 timestamp to enable automatic version history (e.g., `catalog-2026-01-22T10-00-00Z.json`).

A single JSON file simplifies deployment (one file to manage), enables versioning by filename, and allows runtime code (C#) to extract derived lists (roles, features) using LINQ queries rather than requiring separate files.

## File Storage

- **Location:** Configurable via environment variable or config file (to support Kubernetes persistent volumes, Docker volumes, local dev paths, etc.).
- **Naming:** `catalog-{ISO8601_TIMESTAMP}.json` (e.g., `catalog-2026-01-22T10-00-00Z.json`).
- **Format:** JSON with UTF-8 encoding.
- **Version History:** Old catalog files remain on disk; the MCP server loads the latest by timestamp at startup.

## JSON Schema

### Root Structure

```json
{
  "version": "2.0",
  "timestamp": "2026-01-22T10:00:00Z",
  "techStacks": [...],
  "globalRoles": [...],
  "catalog": [...]
}
```

### Tech Stacks

A list of technology platforms, each with its own set of implementation roles. Tech stack roles are scoped to that platform and carry a Copilot productivity multiplier.

```json
"techStacks": [
  {
    "id": "salesforce",
    "name": "Salesforce",
    "description": "Salesforce CRM platform",
    "roles": [
      {
        "id": "sf-dev",
        "name": "Salesforce Developer",
        "description": "Apex, LWC, Flows development",
        "copilotMultiplier": 0.70
      },
      {
        "id": "sf-admin",
        "name": "Salesforce Admin",
        "description": "Configuration, user management",
        "copilotMultiplier": 0.85
      }
    ]
  }
]
```

**Fields (per TechStack):**
- `id` (string): Unique identifier for the tech stack (lowercase, no spaces).
- `name` (string): Display name for the tech stack.
- `description` (string): Human-readable description of the platform.
- `roles` (array): List of roles scoped to this tech stack.

### Global Roles

Roles that are not specific to any tech stack and are shared across all catalog entries (e.g., Engagement Manager, QA Engineer).

```json
"globalRoles": [
  {
    "id": "em",
    "name": "Engagement Manager",
    "description": "Project coordination and stakeholder communication",
    "copilotMultiplier": 1.0
  },
  {
    "id": "qa",
    "name": "QA Engineer",
    "description": "Test planning and execution",
    "copilotMultiplier": 0.65
  }
]
```

**Fields (per Role, both TechStack and Global):**
- `id` (string): Unique identifier for the role (lowercase, with optional stack prefix for tech stack roles).
- `name` (string): Display name for the role.
- `description` (string): Human-readable description of responsibilities.
- `copilotMultiplier` (number): Multiplier for Copilot-enhanced productivity applied to all tasks for this role (0.7 = 30% faster with Copilot, 1.0 = no AI acceleration).

### Role Naming Conventions

- **Global roles:** No prefix (e.g., `em`, `qa`, `ux`).
- **Tech stack roles:** Prefixed with the stack abbreviation (e.g., `sf-dev`, `dotnet-dev`, `aws-architect`).

### Catalog

The catalog contains features/work items with role time estimates. Each catalog entry represents a distinct feature or task scoped to a specific tech stack. Instead of storing all five t-shirt sizes (XS, S, M, L, XL), the catalog stores only the **"M" (Medium) baseline estimate** for each feature-role combination. The other sizes are **auto-calculated using Fibonacci scaling** to minimize data entry and maintenance.

#### Fibonacci Scaling

The t-shirt sizes follow the Fibonacci sequence for proportional estimation:

| T-Shirt Size | Fibonacci Index | Relative Scale |
|--------------|-----------------|----------------|
| XS           | 1               | 1x             |
| S            | 2               | 2x             |
| M            | 5               | 5x (baseline)  |
| L            | 8               | 8x             |
| XL           | 13              | 13x            |

**Calculation:** Given a Medium (M) baseline estimate in hours, other sizes are calculated as:
- XS = M × (1/5)
- S = M × (2/5)
- M = M × 1
- L = M × (8/5)
- XL = M × (13/5)

#### Catalog Entry Example

```json
"catalog": [
  {
    "id": "sf-apex-class",
    "name": "Apex Class Development",
    "description": "Custom Apex class with unit tests",
    "category": "feature",
    "techStack": "salesforce",
    "tags": ["salesforce", "apex", "backend"],
    "mediumEstimates": [
      { "roleId": "sf-dev", "hours": 16 },
      { "roleId": "em", "hours": 2 },
      { "roleId": "qa", "hours": 6 }
    ]
  },
  {
    "id": "sf-api-integration",
    "name": "Third-Party API Integration",
    "description": "Integrate with a third-party REST API from Salesforce",
    "category": "integration",
    "techStack": "salesforce",
    "tags": ["salesforce", "api", "integration"],
    "mediumEstimates": [
      { "roleId": "sf-dev", "hours": 12 },
      { "roleId": "sf-admin", "hours": 4 },
      { "roleId": "em", "hours": 1 },
      { "roleId": "qa", "hours": 4 }
    ]
  }
]
```

**Fields (per Catalog Entry):**
- `id` (string): Unique identifier for this feature (no size suffix, since size is calculated).
- `name` (string): Display name.
- `description` (string): Detailed scope description (applies to all sizes).
- `category` (string): Optional grouping (e.g., "feature", "integration", "devops", "data", "qa").
- `techStack` (string): Reference to a tech stack `id` from the `techStacks` list.
- `tags` (array of strings): Optional tags for filtering and cross-referencing (e.g., platform, layer, domain).
- `mediumEstimates` (array): Array of role estimates for the Medium (M) baseline only.

**Fields (per Medium Estimate):**
- `roleId` (string): Reference to a role from either `globalRoles` or the relevant tech stack's `roles` list.
- `hours` (number): Estimated hours for Medium (M) size (baseline from which other sizes are calculated).

## Deriving Lists and Calculating Sizes via LINQ

In C#, extract derived lists from the catalog using LINQ and auto-calculate all sizes from the Medium baseline. The `AllRoles` helper property combines `GlobalRoles` with all tech stack roles:

```csharp
// All roles: global roles + all tech stack roles flattened
var allRoles = catalog.GlobalRoles.Concat(catalog.TechStacks.SelectMany(ts => ts.Roles)).ToList();

// Extract unique tech stacks
var techStacks = catalog.TechStacks.Select(ts => new { ts.Id, ts.Name, ts.Description }).ToList();

// Extract unique features from catalog entries
var features = catalog.Catalog.Select(f => new { f.Id, f.Name, f.Category, f.TechStack, f.Tags }).ToList();

// Fibonacci scaling factors (relative to Medium = 5)
static decimal GetFibonacciMultiplier(string tshirtSize) => tshirtSize switch
{
  "XS" => 1m / 5m,   // 0.2x
  "S" => 2m / 5m,    // 0.4x
  "M" => 1m,         // 1x
  "L" => 8m / 5m,    // 1.6x
  "XL" => 13m / 5m,  // 2.6x
  _ => throw new ArgumentException($"Unknown size: {tshirtSize}")
};

// Calculate hours for a given feature, role, and size
decimal CalculateHours(CatalogEntry entry, string roleId, string tshirtSize)
{
  var roleEstimate = entry.MediumEstimates.FirstOrDefault(e => e.RoleId == roleId);
  if (roleEstimate == null) return 0;

  var multiplier = GetFibonacciMultiplier(tshirtSize);
  var role = catalog.AllRoles.First(r => r.Id == roleId);

  // Apply Fibonacci scaling, then Copilot multiplier
  return (roleEstimate.Hours * multiplier) * role.CopilotMultiplier;
}

// Example: Get estimate for "sf-apex-class" as Size L, Salesforce Developer role
var estimate = CalculateHours(
  catalog.Catalog.First(e => e.Id == "sf-apex-class"),
  "sf-dev",
  "L"
);
// Hours for M: 16, multiplier for L: 1.6, Copilot: 0.70
// Result: 16 * 1.6 * 0.70 = 17.92 hours
```

## File Loading Strategy

1. **Startup:** Scan the data directory for all files matching `catalog-*.json`.
2. **Select Latest:** Load the file with the most recent timestamp (ISO 8601 sort order is lexicographic-safe).
3. **Parse & Cache:** Deserialize the JSON into an in-memory data model, applying migration if the file is v1.0.
4. **Runtime:** MCP tools query the in-memory model.

```
/data/catalogs/
├── catalog-2025-12-01T08-00-00Z.json
├── catalog-2025-12-05T10-15-30Z.json
└── catalog-2026-01-22T10-00-00Z.json  (loaded at startup)
```

## v1.0 → v2.0 Migration

The `DeserializeWithMigration` method automatically migrates v1.0 catalog files to the v2.0 schema on load. No manual file conversion is required.

**Migration steps performed automatically:**

1. **`roles` → `globalRoles`:** The flat `roles` array from v1.0 is moved to `globalRoles` unchanged, since v1.0 roles had no tech stack affiliation.
2. **TechStack extraction:** Unique `techStack` values referenced in catalog entries are used to create stub `TechStack` objects in `techStacks`. Catalog entries that lacked a `techStack` field in v1.0 receive `null` (treated as tech stack-agnostic).
3. **Version bump:** The `version` field is updated from `"1.0"` to `"2.0"` in the migrated in-memory model (the source file on disk is not modified).

```csharp
CatalogData DeserializeWithMigration(string json)
{
    var doc = JsonDocument.Parse(json);
    var version = doc.RootElement.GetProperty("version").GetString();

    if (version == "1.0")
    {
        var v1 = JsonSerializer.Deserialize<CatalogDataV1>(json);
        return new CatalogData
        {
            Version = "2.0",
            Timestamp = v1.Timestamp,
            GlobalRoles = v1.Roles,   // flat roles become global roles
            TechStacks = [],          // no tech stacks in v1.0
            Catalog = v1.Catalog
        };
    }

    return JsonSerializer.Deserialize<CatalogData>(json)!;
}
```

## Version and Timestamp Fields

- `version`: Schema version (e.g., "2.0") for future migrations.
- `timestamp`: ISO 8601 UTC creation timestamp of this catalog snapshot.

## Future Enhancements

- **Compaction:** Periodically archive old catalogs (e.g., keep last 30 days, compress older ones).
- **Validation:** JSON Schema validation on load.
- **Change Tracking:** Add `createdAt`, `modifiedAt`, `createdBy` fields to catalog entries for audit trails.
