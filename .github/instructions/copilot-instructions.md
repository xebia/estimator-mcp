# Copilot Instructions for Estimator MCP

## Project Overview

**Estimator MCP** is a Model Context Protocol (MCP) server that generates software project time estimates. It manages a catalog of work items (features, tasks) mapped to implementation roles with effort estimates, then serves estimates via MCP tools to LLM-based interfaces.

**Key Goal:** Enable AI agents to collect task/feature descriptions from users, query the catalog, and return per-role, per-task time breakdowns (time-based only; cost calculations are external).

## Architecture & Data Flow

### Single-File JSON Catalog (Time-Series Versioned)

- **Location:** Configurable disk path (supports Kubernetes persistent volumes, local dev, Docker volumes).
- **Format:** `catalog-{ISO8601_TIMESTAMP}.json` (e.g., `catalog-2025-12-12T14-30-00Z.json`).
- **Loading Strategy:** At startup, scan the data directory and load the latest catalog by timestamp (ISO 8601 lexicographic sort order is safe).
- **Version History:** Old catalogs remain on disk; automatic history without database.

**JSON Structure Outline:**
```json
{
  "version": "1.0",
  "timestamp": "2025-12-12T14:30:00Z",
  "roles": [{ "id": "developer", "name": "...", "copilotMultiplier": 0.70 }, ...],
  "features": [{ "id": "basic-crud", "name": "...", "category": "..." }, ...],
  "catalog": [{ "id": "basic-crud", "featureId": "...", "mediumEstimates": [...] }, ...]
}
```

### Roles (with Built-in Copilot Multipliers)

Three main roles; Copilot multiplier applies uniformly across all tasks for that role:

| Role | ID | Copilot Multiplier | Purpose |
|------|----|--------------------|---------|
| Developer | `developer` | 0.70 | Back-end, front-end, business logic implementation |
| DevOps Engineer | `devops` | 0.75 | Infrastructure, CI/CD, deployment |
| Engagement Manager | `em` | 1.0 | Coordination (no AI acceleration assumed) |

### Fibonacci Scaling (Minimal Data Entry)

Catalog stores **only Medium (M) baseline** estimates per feature-role. Other sizes auto-calculate using Fibonacci sequence:

| Size | Multiplier | Formula |
|------|-----------|---------|
| XS | 1/5 = 0.2x | M × 0.2 |
| S | 2/5 = 0.4x | M × 0.4 |
| M | 1.0x | M (baseline stored) |
| L | 8/5 = 1.6x | M × 1.6 |
| XL | 13/5 = 2.6x | M × 2.6 |

**Calculation Formula:**
```
FinalHours = (MediumEstimate × FibonacciMultiplier) × RoleCopilotMultiplier
```

Example: `basic-crud` (M=24 hours) as Size L for Developer (0.70 multiplier):
- 24 × 1.6 × 0.70 = **26.88 hours**

## MCP Tools (What the Server Exposes)

When building the MCP server, implement three main tools:

1. **`instructions`** – Returns markdown guidance for the LLM on how to use the server. Directs LLM to read this first.
2. **`estimate`** – Accepts `{ tasks: [{ featureId: string, tshirtSize: "XS"|"S"|"M"|"L"|"XL" }] }`, returns per-task/per-role hours breakdown.
3. **`catalog-query`** – Search/browse catalog items by ID, name, category, or feature name.

Tool descriptions (visible to LLM) must be detailed; use them to guide AI behavior.

## Key Implementation Patterns

### Deriving Lists from Catalog (C# LINQ)

No separate files needed; extract at runtime:

```csharp
// Extract unique roles
var roles = catalog.Roles.Select(r => new { r.Id, r.Name }).ToList();

// Extract unique features
var features = catalog.Features.Select(f => new { f.Id, f.Name }).ToList();

// Fibonacci multiplier lookup
static decimal GetFibonacciMultiplier(string tshirtSize) => tshirtSize switch
{
  "XS" => 1m / 5m, "S" => 2m / 5m, "M" => 1m, "L" => 8m / 5m, "XL" => 13m / 5m,
  _ => throw new ArgumentException($"Unknown size: {tshirtSize}")
};

// Calculate final hours (Fibonacci × Copilot multiplier)
decimal CalculateHours(CatalogEntry entry, string roleId, string tshirtSize)
{
  var estimate = entry.MediumEstimates.FirstOrDefault(e => e.RoleId == roleId);
  if (estimate == null) return 0;
  var role = catalog.Roles.First(r => r.Id == roleId);
  return (estimate.Hours * GetFibonacciMultiplier(tshirtSize)) * role.CopilotMultiplier;
}
```

### File Loading at Startup

Scan for latest catalog:

```csharp
var catalogDir = Environment.GetEnvironmentVariable("CATALOG_DIR") ?? "./catalogs";
var latestFile = Directory.GetFiles(catalogDir, "catalog-*.json")
  .OrderByDescending(f => Path.GetFileNameWithoutExtension(f))
  .FirstOrDefault();

catalog = JsonSerializer.Deserialize<Catalog>(File.ReadAllText(latestFile));
```

## MVP Scope & Assumptions

- **MVP Focus:** Catalog definition, Fibonacci scaling, MCP tool implementation, basic LLM integration.
- **Time Estimates Only:** Cost/rate calculations are external (future phase).
- **No Security/Auth:** File-based catalog; user roles deferred to future phase.
- **Single Catalog File:** All data in one JSON file; LINQ queries derive lists at runtime.
- **LLM Workflow:** AI collects tasks + t-shirt sizing from user → calls `estimate` tool → returns breakdown to user.

## Not in Scope (Future Phases)

- Cost handling (rate sheets, multi-currency, geo-specific rates).
- User authentication, audit logging, access control.
- Non-functional requirements modeling (% uplift for testing, deployment, etc.).
- Staffing plan and timeline generation (AI responsibility, not MCP).
- Feature dependencies, bill-of-materials.
- Contingency/risk modeling.

## When Developing or Extending

1. **Adding a New Feature to Catalog:** Add entry to `features` list, then create catalog entry with `featureId` and `mediumEstimates` (role-hours pairs). Size calculations are automatic.
2. **Adding a New Role:** Add to `roles` list with `id`, `name`, and `copilotMultiplier`. Update all catalog entries' `mediumEstimates` to include the new role (set hours to 0 if not applicable).
3. **Modifying Copilot Multiplier:** Update `roles[].copilotMultiplier`; all estimates using that role recalculate automatically.
4. **Querying the Catalog:** Always use LINQ patterns above; don't duplicate role/feature lists.

## Key Files

- `spec/overview.md` – High-level goal, requirements, MVP scope.
- `spec/data-structure.md` – Complete JSON schema, Fibonacci scaling math, LINQ patterns.
