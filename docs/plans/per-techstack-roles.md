# Per-TechStack Roles Implementation Plan

## Overview

This document outlines the plan for implementing per-techstack roles in the Estimator MCP system. Currently, roles are global across all techstacks, but different technology platforms often require different roles or role configurations (e.g., "Salesforce Developer" vs ".NET Developer", different AI productivity multipliers per stack).

## Current State

### Roles Today
- Roles are defined globally in `CatalogData.Roles`
- All catalog entries reference these global roles via `MediumEstimate.RoleId`
- Role properties: `Id`, `Name`, `Description`, `CopilotMultiplier`
- Current roles: architect, dev, devops, em, qa, security, ux

### TechStacks Today
- `TechStack` is a simple string property on `CatalogEntry`
- Current techstacks: salesforce, aws, azure, dotnet, python
- No relationship between techstacks and roles

### Pain Points
1. Role names don't reflect techstack context (e.g., "Developer" instead of "Salesforce Developer")
2. Copilot multipliers may vary by techstack (AI assists .NET differently than Salesforce)
3. Some techstacks need unique roles (e.g., Salesforce Admin, AWS Solutions Architect)
4. No way to define techstack-specific role sets

## Proposed Solution

### Design Goals
1. Support techstack-specific role definitions
2. Allow shared/global roles that span multiple techstacks
3. Maintain backwards compatibility with existing catalogs
4. Enable different Copilot multipliers per techstack-role combination
5. Minimize disruption to existing workflows

### Data Model Changes

#### Option A: TechStack-Scoped Roles (Recommended)

Create a new `TechStack` entity that owns its roles:

```csharp
// New model: TechStack.cs
public class TechStack
{
    public string Id { get; set; }           // e.g., "salesforce", "dotnet"
    public string Name { get; set; }         // e.g., "Salesforce", ".NET"
    public string? Description { get; set; }
    public List<Role> Roles { get; set; } = [];
}

// Updated CatalogData.cs
public class CatalogData
{
    public string Version { get; set; } = "2.0";
    public DateTimeOffset Timestamp { get; set; }
    public List<TechStack> TechStacks { get; set; } = [];  // NEW
    public List<Role> GlobalRoles { get; set; } = [];      // Renamed, for shared roles
    public List<CatalogEntry> Catalog { get; set; } = [];

    // Computed: All roles (global + techstack-specific)
    [JsonIgnore]
    public IEnumerable<Role> AllRoles => GlobalRoles
        .Concat(TechStacks.SelectMany(ts => ts.Roles));
}

// Updated Role.cs
public class Role
{
    public string Id { get; set; }              // e.g., "sf-dev", "dotnet-dev"
    public string Name { get; set; }            // e.g., "Salesforce Developer"
    public string? Description { get; set; }
    public decimal CopilotMultiplier { get; set; } = 1.0m;
    public string? TechStackId { get; set; }    // NEW: null = global role
}
```

#### JSON Structure (v2.0)

```json
{
  "version": "2.0",
  "timestamp": "2026-01-22T10:00:00Z",
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
        },
        {
          "id": "sf-architect",
          "name": "Salesforce Architect",
          "description": "Solution design, integration patterns",
          "copilotMultiplier": 0.80
        }
      ]
    },
    {
      "id": "dotnet",
      "name": ".NET",
      "description": ".NET platform (ASP.NET Core, Blazor, etc.)",
      "roles": [
        {
          "id": "dotnet-dev",
          "name": ".NET Developer",
          "description": "C#, ASP.NET Core, Blazor development",
          "copilotMultiplier": 0.55
        },
        {
          "id": "dotnet-architect",
          "name": ".NET Architect",
          "description": "Solution architecture, cloud patterns",
          "copilotMultiplier": 0.75
        }
      ]
    }
  ],
  "globalRoles": [
    {
      "id": "em",
      "name": "Engagement Manager",
      "description": "Project coordination, client communication",
      "copilotMultiplier": 1.0
    },
    {
      "id": "qa",
      "name": "QA Engineer",
      "description": "Test planning and execution",
      "copilotMultiplier": 0.65
    }
  ],
  "catalog": [
    {
      "id": "sf-apex-class-development",
      "name": "Apex Class Development",
      "techStack": "salesforce",
      "mediumEstimates": [
        { "roleId": "sf-dev", "hours": 16 },
        { "roleId": "sf-architect", "hours": 4 },
        { "roleId": "em", "hours": 2 },
        { "roleId": "qa", "hours": 6 }
      ]
    }
  ]
}
```

### Role Resolution Rules

1. **Catalog entries** can reference:
   - Roles from their assigned techstack (via `TechStackId`)
   - Global roles (where `TechStackId` is null)

2. **Validation rules**:
   - Entry with `TechStack = "salesforce"` can use `sf-dev`, `sf-admin`, `em`, `qa`
   - Entry with `TechStack = "salesforce"` cannot use `dotnet-dev`
   - Entry with `TechStack = null` (shared) can only use global roles

3. **Role ID uniqueness**:
   - Role IDs must be globally unique (across all techstacks)
   - Convention: prefix techstack roles with stack abbreviation (`sf-`, `dotnet-`, `aws-`)

---

## Implementation Plan

### Phase 1: Data Model & Migration

**Files to modify:**
- `src/EstimatorMcp.Models/Role.cs` - Add `TechStackId` property
- `src/EstimatorMcp.Models/TechStack.cs` - NEW file
- `src/EstimatorMcp.Models/CatalogData.cs` - Add `TechStacks`, rename `Roles` to `GlobalRoles`

**Tasks:**
1. Create `TechStack.cs` model class
2. Update `Role.cs` with optional `TechStackId` property
3. Update `CatalogData.cs`:
   - Add `TechStacks` property
   - Rename `Roles` to `GlobalRoles`
   - Add `AllRoles` computed property
   - Update version to "2.0"
4. Create migration utility to convert v1.0 catalogs to v2.0:
   - Move all existing roles to `GlobalRoles`
   - Extract unique techstack values from entries and create `TechStacks` (with empty role lists initially)

### Phase 2: Catalog Editor Updates

**Files to modify:**
- `src/CatalogEditor/.../Services/ICatalogDataProvider.cs`
- `src/CatalogEditor/.../Services/JsonCatalogDataProvider.cs`
- `src/CatalogEditor/.../Components/Pages/Roles.razor`
- `src/CatalogEditor/.../Components/Pages/RoleEdit.razor`
- `src/CatalogEditor/.../Components/Pages/CatalogEdit.razor`

**New files:**
- `src/CatalogEditor/.../Components/Pages/TechStacks.razor`
- `src/CatalogEditor/.../Components/Pages/TechStackEdit.razor`

**Tasks:**

1. **Update ICatalogDataProvider interface:**
   ```csharp
   // TechStack operations
   Task<List<TechStack>> GetTechStacksAsync();
   Task<TechStack?> GetTechStackAsync(string id);
   Task SaveTechStackAsync(TechStack techStack);
   Task DeleteTechStackAsync(string id);

   // Updated role operations
   Task<List<Role>> GetRolesAsync(string? techStackId = null);
   Task<List<Role>> GetGlobalRolesAsync();
   Task<List<Role>> GetRolesForTechStackAsync(string techStackId);
   Task<List<Role>> GetAvailableRolesForEntryAsync(string? techStackId);
   ```

2. **Create TechStacks management pages:**
   - List page showing all techstacks with their role counts
   - Edit page for creating/editing techstacks
   - Nested role management within techstack context

3. **Update Roles pages:**
   - Add filter/grouping by techstack
   - Show techstack badge on each role
   - Allow creating roles as global or techstack-specific

4. **Update CatalogEdit.razor:**
   - When editing an entry, only show roles available for that techstack
   - Role dropdown shows: techstack roles + global roles
   - Validate role assignments on save

### Phase 3: CLI Tool Updates

**Files to modify:**
- `src/CatalogCli/Commands/ExportCommand.cs`
- `src/CatalogCli/Commands/ImportCommand.cs`
- `src/CatalogCli/TsvExporter.cs`
- `src/CatalogCli/TsvImporter.cs`

**Tasks:**

1. **Update TSV export format:**
   - New file: `techstacks.tsv` with columns: `Id`, `Name`, `Description`
   - Update `roles.tsv` with new column: `TechStackId` (empty for global)
   - `entries.tsv` unchanged (role columns remain dynamic)

2. **Update TSV import:**
   - Parse `techstacks.tsv` first
   - Validate role `TechStackId` references
   - Validate entry role references match techstack constraints

3. **Add migration command:**
   ```bash
   catalog-cli migrate --input catalog-v1.json --output catalog-v2.json
   ```

### Phase 4: MCP Server Updates

**Files to modify:**
- `src/estimator-mcp/Tools/CatalogTool.cs`
- `src/estimator-mcp/Tools/CalculateEstimateTool.cs`

**Tasks:**

1. **Update CatalogTool:**
   - Enhance `GetCatalogTechStacks` to return techstack details including their roles
   - Add new tool: `GetRolesForTechStack(string techStackId)`
   - Update tool descriptions for LLM guidance

2. **Update CalculateEstimateTool:**
   - Update role lookup to handle both global and techstack-specific roles
   - Validate role assignments match entry techstack
   - Include techstack info in output

3. **Update instructions.md:**
   - Document the techstack-role relationship
   - Provide examples of querying roles by techstack
   - Update estimate calculation examples

### Phase 5: Testing & Documentation

**Tasks:**

1. **Unit tests:**
   - Role resolution logic
   - Validation rules (techstack role constraints)
   - Migration from v1.0 to v2.0
   - TSV import/export with techstacks

2. **Integration tests:**
   - End-to-end catalog editing workflow
   - MCP tool responses
   - CLI import/export roundtrip

3. **Documentation updates:**
   - Update `spec/data-structure.md` with v2.0 schema
   - Update `CLAUDE.md` with new data model
   - Update `spec/overview.md` with new MCP tool specs
   - Create migration guide for existing users

---

## Backwards Compatibility

### Catalog File Versioning

1. **Version detection:** Check `version` field in JSON
2. **Auto-migration:** When loading v1.0 catalog:
   - Treat all roles as global roles
   - Create techstack entries from unique `CatalogEntry.TechStack` values
   - Log warning about migration
3. **Save as v2.0:** Always save in new format

### API Compatibility

1. **GetCatalogFeatures:** Unchanged - still filters by techstack string
2. **CalculateEstimate:** Unchanged - role IDs still work the same way
3. **New tools:** Additive - don't break existing integrations

---

## Role Naming Conventions

### Recommended Role ID Format
```
{techstack-prefix}-{role-type}
```

Examples:
- `sf-dev` - Salesforce Developer
- `sf-admin` - Salesforce Administrator
- `sf-architect` - Salesforce Architect
- `dotnet-dev` - .NET Developer
- `dotnet-architect` - .NET Architect
- `aws-devops` - AWS DevOps Engineer
- `aws-architect` - AWS Solutions Architect

### Global Roles (no prefix)
- `em` - Engagement Manager
- `pm` - Project Manager
- `qa` - QA Engineer (if same across stacks)
- `ux` - UX Designer

---

## Open Questions

1. **Role inheritance?** Should techstack roles be able to inherit from a base role template?
   - Pros: Less duplication, consistent defaults
   - Cons: Added complexity, harder to understand

2. **Multiple techstacks per entry?** Should entries support multiple techstacks?
   - Current: Single `TechStack` string
   - Consideration: Some features span multiple stacks (e.g., integration work)
   - Recommendation: Keep single techstack, use tags for cross-cutting concerns

3. **Default techstack?** Should there be a concept of a "default" or "any" techstack for shared features?
   - Current: `TechStack = null` means shared/any
   - Recommendation: Keep this pattern, document clearly

4. **Migration strategy for existing data?**
   - Option A: Automatic migration on load (recommended)
   - Option B: Manual migration command required
   - Option C: Support both v1 and v2 indefinitely

---

## Success Criteria

1. Each techstack can define its own set of roles
2. Global roles are available across all techstacks
3. Catalog entries validate role assignments against their techstack
4. MCP tools expose techstack-role relationships to AI consumers
5. CLI tools support the new data structure for import/export
6. Existing v1.0 catalogs continue to work (auto-migrated)
7. Blazor editor provides intuitive UI for managing techstack roles

---

## Appendix: Alternative Designs Considered

### Option B: Role Overrides per TechStack

Instead of separate role definitions, allow techstacks to override properties of global roles:

```json
{
  "roles": [
    { "id": "dev", "name": "Developer", "copilotMultiplier": 0.7 }
  ],
  "techStacks": [
    {
      "id": "salesforce",
      "roleOverrides": [
        { "roleId": "dev", "name": "Salesforce Developer", "copilotMultiplier": 0.75 }
      ]
    }
  ]
}
```

**Rejected because:**
- More complex resolution logic
- Confusing inheritance behavior
- Harder to understand which values are in effect

### Option C: Flat Roles with TechStack Tags

Keep roles flat but add techstack tags to indicate availability:

```json
{
  "roles": [
    { "id": "sf-dev", "name": "SF Developer", "techStacks": ["salesforce"] },
    { "id": "dotnet-dev", "name": ".NET Developer", "techStacks": ["dotnet", "azure"] },
    { "id": "em", "name": "Engagement Manager", "techStacks": ["*"] }
  ]
}
```

**Rejected because:**
- No natural grouping in UI
- Harder to manage techstack metadata
- Less intuitive than nested structure
