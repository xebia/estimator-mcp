# Data Structure Specification

## Overview

The MCP Server stores all configuration and catalog data in a single **JSON file** persisted on disk. The file is named with an ISO 8601 timestamp to enable automatic version history (e.g., `catalog-2025-12-12T14-30-00Z.json`).

A single JSON file simplifies deployment (one file to manage), enables versioning by filename, and allows runtime code (C#) to extract derived lists (roles, countables, features) using LINQ queries rather than requiring separate files.

## File Storage

- **Location:** Configurable via environment variable or config file (to support Kubernetes persistent volumes, Docker volumes, local dev paths, etc.).
- **Naming:** `catalog-{ISO8601_TIMESTAMP}.json` (e.g., `catalog-2025-12-12T14-30-00Z.json`).
- **Format:** JSON with UTF-8 encoding.
- **Version History:** Old catalog files remain on disk; the MCP server loads the latest by timestamp at startup.

## JSON Schema

### Root Structure

```json
{
  "version": "1.0",
  "timestamp": "2025-12-12T14:30:00Z",
  "roles": [...],
  "countables": [...],
  "features": [...],
  "catalog": [...]
}
```

### Roles

A list of implementation roles used in the project. Each role includes a Copilot productivity multiplier applied uniformly across all tasks for that role.

```json
"roles": [
  {
    "id": "developer",
    "name": "Developer",
    "description": "Software developer implementing features and business logic",
    "copilotMultiplier": 0.70
  },
  {
    "id": "devops",
    "name": "DevOps Engineer",
    "description": "Infrastructure, deployment, and CI/CD specialist",
    "copilotMultiplier": 0.75
  },
  {
    "id": "em",
    "name": "Engagement Manager",
    "description": "Project coordination and stakeholder communication",
    "copilotMultiplier": 1.0
  }
]
```

**Fields:**
- `id` (string): Unique identifier for the role (lowercase, no spaces).
- `name` (string): Display name for the role.
- `description` (string): Human-readable description of responsibilities.
- `copilotMultiplier` (number): Multiplier for Copilot-enhanced productivity applied to all tasks for this role (0.7 = 30% faster with Copilot, 1.0 = no AI acceleration).

### Countables

Items that can be counted/measured independently and contribute to project scope (e.g., user stories, API endpoints, database tables, deployment environments). These provide granularity for estimation.

```json
"countables": [
  {
    "id": "user-story",
    "name": "User Story",
    "description": "A discrete feature or requirement from the product backlog",
    "category": "requirements"
  },
  {
    "id": "api-endpoint",
    "name": "API Endpoint",
    "description": "A REST API endpoint or GraphQL query/mutation",
    "category": "api"
  },
  {
    "id": "db-table",
    "name": "Database Table",
    "description": "A new database table with CRUD operations",
    "category": "data"
  },
  {
    "id": "deployment-env",
    "name": "Deployment Environment",
    "description": "A deployment target (dev, staging, prod, etc.)",
    "category": "infrastructure"
  },
  {
    "id": "integration",
    "name": "Third-Party Integration",
    "description": "Integration with an external service or API",
    "category": "integration"
  }
]
```

**Fields:**
- `id` (string): Unique identifier for the countable type.
- `name` (string): Display name.
- `description` (string): Description of what this countable represents.
- `category` (string): Optional grouping (e.g., "requirements", "api", "data", "infrastructure", "integration").

### Features

A list of typical work items (features, tasks, or technical activities) that appear in the catalog. Each feature can be sized with t-shirt sizing and mapped to roles.

```json
"features": [
  {
    "id": "basic-crud",
    "name": "Basic CRUD Feature",
    "description": "Implement create, read, update, delete operations for a domain entity",
    "category": "feature"
  },
  {
    "id": "api-integration",
    "name": "Third-Party API Integration",
    "description": "Integrate with a third-party REST API",
    "category": "integration"
  },
  {
    "id": "ci-cd-pipeline",
    "name": "CI/CD Pipeline Setup",
    "description": "Configure automated build, test, and deployment pipeline",
    "category": "devops"
  },
  {
    "id": "database-migration",
    "name": "Database Schema Migration",
    "description": "Plan and execute a database schema change",
    "category": "data"
  },
  {
    "id": "testing-suite",
    "name": "Comprehensive Test Suite",
    "description": "Write unit, integration, and end-to-end tests",
    "category": "qa"
  },
  {
    "id": "performance-optimization",
    "name": "Performance Optimization",
    "description": "Optimize code or infrastructure for performance improvements",
    "category": "devops"
  }
]
```

**Fields:**
- `id` (string): Unique identifier for the feature.
- `name` (string): Display name.
- `description` (string): Detailed description of the work involved.
- `category` (string): Optional grouping (e.g., "feature", "integration", "devops", "data", "qa").

### Catalog

The main catalog that maps features to role time estimates. Instead of storing all five t-shirt sizes (XS, S, M, L, XL), the catalog stores only the **"M" (Medium) baseline estimate** for each feature-role combination. The other sizes are **auto-calculated using Fibonacci scaling** to minimize data entry and maintenance.

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
    "id": "basic-crud",
    "featureId": "basic-crud",
    "name": "Basic CRUD Feature",
    "description": "Implement create, read, update, delete operations for a domain entity",
    "mediumEstimates": [
      {
        "roleId": "developer",
        "hours": 24
      },
      {
        "roleId": "devops",
        "hours": 4
      },
      {
        "roleId": "em",
        "hours": 2
      }
    ]
  },
  {
    "id": "api-integration",
    "featureId": "api-integration",
    "name": "Third-Party API Integration",
    "description": "Integrate with a third-party REST API",
    "mediumEstimates": [
      {
        "roleId": "developer",
        "hours": 16
      },
      {
        "roleId": "devops",
        "hours": 4
      },
      {
        "roleId": "em",
        "hours": 1
      }
    ]
  },
  {
    "id": "ci-cd-pipeline",
    "featureId": "ci-cd-pipeline",
    "name": "CI/CD Pipeline Setup",
    "description": "Configure automated build, test, and deployment pipeline",
    "mediumEstimates": [
      {
        "roleId": "developer",
        "hours": 4
      },
      {
        "roleId": "devops",
        "hours": 16
      },
      {
        "roleId": "em",
        "hours": 2
      }
    ]
  }
]
```

**Fields (per Catalog Entry):**
- `id` (string): Unique identifier for this feature (no size suffix, since size is calculated).
- `featureId` (string): Reference to a feature from the `features` list.
- `name` (string): Display name.
- `description` (string): Detailed scope description (applies to all sizes).
- `mediumEstimates` (array): Array of role estimates for the Medium (M) baseline only.

**Fields (per Medium Estimate):**
- `roleId` (string): Reference to a role from the `roles` list.
- `hours` (number): Estimated hours for Medium (M) size (baseline from which other sizes are calculated).

## Deriving Lists and Calculating Sizes via LINQ

In C#, extract derived lists from the catalog using LINQ and auto-calculate all sizes from the Medium baseline:

```csharp
// Extract unique roles
var roles = catalog.Roles.Select(r => new { r.Id, r.Name }).ToList();

// Extract unique countables
var countables = catalog.Countables.Select(c => new { c.Id, c.Name }).ToList();

// Extract unique features
var features = catalog.Features.Select(f => new { f.Id, f.Name }).ToList();

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
  var role = catalog.Roles.First(r => r.Id == roleId);
  
  // Apply Fibonacci scaling, then Copilot multiplier
  return (roleEstimate.Hours * multiplier) * role.CopilotMultiplier;
}

// Example: Get estimate for "basic-crud" as Size L, Developer role
var estimate = CalculateHours(
  catalog.Catalog.First(e => e.Id == "basic-crud"),
  "developer",
  "L"
);
// Hours for M: 24, multiplier for L: 1.6, Copilot: 0.70
// Result: 24 * 1.6 * 0.70 = 26.88 hours
```

## File Loading Strategy

1. **Startup:** Scan the data directory for all files matching `catalog-*.json`.
2. **Select Latest:** Load the file with the most recent timestamp (ISO 8601 sort order is lexicographic-safe).
3. **Parse & Cache:** Deserialize the JSON into an in-memory data model.
4. **Runtime:** MCP tools query the in-memory model; no re-reads unless server restarts.

## Example File Path Structure

```
/data/catalogs/
├── catalog-2025-12-01T08-00-00Z.json
├── catalog-2025-12-05T10-15-30Z.json
└── catalog-2025-12-12T14-30-00Z.json  (loaded at startup)
```

## Version and Timestamp Fields

- `version`: Schema version (e.g., "1.0") for future migrations.
- `timestamp`: ISO 8601 UTC creation timestamp of this catalog snapshot.

## Future Enhancements

- **Compaction:** Periodically archive old catalogs (e.g., keep last 30 days, compress older ones).
- **Validation:** JSON Schema validation on load.
- **Change Tracking:** Add `createdAt`, `modifiedAt`, `createdBy` fields to catalog entries for audit trails.
- **Countable Usage:** Track which countables are used by which features/tasks for dependency analysis.
