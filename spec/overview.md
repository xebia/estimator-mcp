# MCP Server System for Project Estimate Generation

1. 🎯 Project Goal
To create a system for generating project estimates in a consulting context by managing a catalog of tasks/features (function points) with associated effort, cost profiles, and team roles, and allowing users to create project estimates based on this catalog.

2. 📝 Key Features and Requirements
The MCP Server system must support the following capabilities:

| Feature Area | Requirement/Description |
|--------------|-------------------------|
| Catalog Management | • Define and maintain a central catalog of features/work items.<br>• Each catalog entry includes ID, Name, Description, Category, and role estimates.<br>• Each catalog entry stores Medium (M) baseline hours per role, with other sizes (XS, S, L, XL) auto-calculated using Fibonacci scaling.<br>• Catalog stored as JSON with timestamp-based versioning (automatic version history). |
| Estimate Generation | • LLM collects task/feature descriptions from user (using t-shirt sizing).<br>• MCP server receives selected tasks and role specifications.<br>• Server returns breakdown of **estimated time per role per task** in hours/days.<br>• Output is time-based only; cost calculations handled externally via rate sheets. |
| Team/Staffing Model | • The system accounts for various implementation roles (e.g., Developer, DevOps Engineer).<br>• Supports fractional roles, specifically for Engagement Manager responsible for coordination.<br>• Time estimates are per-role so external systems can build staffing/scheduling plans. |
| Non-Functional Requirements | • Non-functional requirements (tooling, testing, deployment) handled as percentage uplift across the project (time burden applied to all tasks). |
| Tooling & Implementation | • LLM-collaborative interface: AI interviews user to collect task descriptions and t-shirt sizing.<br>• For MVP: AI feeds tasks to MCP server and returns time estimates to user (no external integrations). |

3. 👥 Roles and Responsibilities (Implementation Roles)
The estimation process tracks effort allocation across the following implementation roles:

| Role Name | Key Responsibilities | Time Estimates |
|-----------|---------------------|----------------|
| Developer | Implementing function points/features (back-end, front-end, business logic). | Per-task hours/days from catalog. |
| DevOps Engineer | Infrastructure, deployment, CI/CD pipelines, environment setup. | Per-task hours/days from catalog. |
| Engagement Manager | Project coordination, Product Owner liaison, team communication. | Per-task allocation (fractional or full-time as % of core tasks). |

**Note:** User roles (Catalog Manager, Estimator) and security/access control are deferred to a future phase. For MVP, catalog is file-based with automatic version history.

4. 💻 Technical Considerations

A. Data Model
The catalog contains features/work items directly with role time estimates:

- **Feature/Task (Catalog Entry):** ID, Name, Description, Category (optional tags/domains).
- **T-Shirt Sizing Mapping:** Each feature has a Medium (M) baseline, with other sizes (XS, S, L, XL) calculated using Fibonacci scaling.
- **Role-Time Mapping:** For each catalog entry and sizing level, estimated hours/days per role (Developer, DevOps Engineer, Engagement Manager).
- **Productivity Adjustment:** Copilot-enhanced productivity multiplier is defined per role and applied to all tasks for that role.

B. Catalog Storage
- JSON format stored on disk.
- Filename includes timestamp (e.g., `catalog-2025-12-12T14-30-00Z.json`) for automatic version history.
- No database, security, or user roles in MVP; all future enhancements.

C. MCP Server Interface
The MCP server exposes tools with detailed descriptions (used by LLM for tool selection):

1. **get_catalog_features** (tool): Returns a list of all available features from the catalog, including feature ID, name, and description. This allows the AI to present options to the user or search for relevant features.
   - **Input**: Optional category filter
   - **Output**: Array of objects with `id`, `name`, `description`, and `category` fields

2. **calculate_estimate** (tool): Accepts a list of features (with feature IDs and t-shirt sizes) and returns the calculated time estimates per role for the entire project.
   - **Input**: Array of objects with `featureId` and `size` (XS, S, M, L, XL)
   - **Output**: Object with total hours per role and breakdown per feature/role
   
3. **get_instructions** (tool): Returns markdown document with overall guidance for the LLM on how to use the MCP server and interact with users to gather project requirements.
   - **Input**: None
   - **Output**: Markdown-formatted instructions

4. Top-level MCP description directs the LLM to invoke the `get_instructions` tool first.

D. AI/LLM Workflow (MVP)
For the proof-of-concept:
1. LLM calls `get_instructions` tool to understand how to assist the user.
2. LLM calls `get_catalog_features` to retrieve available features from the catalog.
3. LLM interviews user to understand project scope, discussing features that match catalog items.
4. LLM helps user select appropriate features and assign t-shirt sizes (XS, S, M, L, XL) based on project complexity.
5. LLM calls `calculate_estimate` tool with the list of selected features and sizes.
6. MCP server returns time breakdown per role per feature, plus totals.
7. LLM presents results to user with appropriate formatting; higher-level staffing plans and scheduling are AI responsibility.

5. �️ MCP Tool Specifications

### Tool: `get_catalog_features`

**Purpose:** Retrieve all available features from the catalog for the AI to present to users.

**Input Schema:**
```json
{
  "category": "string (optional)"  // Filter by category (e.g., "feature", "integration", "devops")
}
```

**Output Schema:**
```json
{
  "features": [
    {
      "id": "basic-crud",
      "name": "Basic CRUD Feature",
      "description": "Implement create, read, update, delete operations for a domain entity",
      "category": "feature"
    },
    // ... more features
  ]
}
```

### Tool: `calculate_estimate`

**Purpose:** Calculate time estimates for a project based on selected features and their sizes.

**Input Schema:**
```json
{
  "features": [
    {
      "featureId": "basic-crud",
      "size": "L"  // One of: XS, S, M, L, XL
    },
    {
      "featureId": "api-integration",
      "size": "M"
    }
    // ... more feature selections
  ]
}
```

**Output Schema:**
```json
{
  "totalsByRole": {
    "developer": {
      "hours": 156.8,
      "days": 19.6  // Based on 8-hour days
    },
    "devops": {
      "hours": 24.0,
      "days": 3.0
    },
    "em": {
      "hours": 9.8,
      "days": 1.225
    }
  },
  "breakdown": [
    {
      "featureId": "basic-crud",
      "featureName": "Basic CRUD Feature",
      "size": "L",
      "estimates": {
        "developer": {
          "baseHours": 38.4,  // M baseline (24) * L multiplier (1.6)
          "copilotMultiplier": 0.70,
          "finalHours": 26.88
        },
        "devops": {
          "baseHours": 6.4,
          "copilotMultiplier": 0.75,
          "finalHours": 4.8
        },
        "em": {
          "baseHours": 3.2,
          "copilotMultiplier": 1.0,
          "finalHours": 3.2
        }
      }
    }
    // ... breakdown for each feature
  ]
}
```

### Tool: `get_instructions`

**Purpose:** Provide guidance to the AI on how to interact with users and use the MCP server.

**Input Schema:** None

**Output:** Markdown document with:
- Overview of the estimation process
- How to help users identify and size features
- Best practices for conducting estimation interviews
- Example conversations and outputs
- Guidance on presenting results

6. �🗺️ Next Steps

**Phase 1 (MVP):**
1. **Catalog Definition:** Conduct exercise to identify core tasks/features with historical time estimates per role; apply Copilot productivity multipliers.
2. **T-Shirt Sizing Model:** Define mapping from XS–XL sizing to concrete hours/days for each role-task combo.
3. **Data Model Finalization:** Detailed schema for JSON catalog storage and version naming.
4. **MCP Server Development:**
   - Implement `instructions`, `estimate`, and `catalog-query` tools.
   - Load versioned catalog JSON and serve time-based estimates.
5. **LLM Integration & PoC:** Test AI interface for task collection and estimate generation.

**Future Phases:**
- Cost handling (rate sheets, currency, multi-region rates).
- User authentication, role-based access, and audit logging.
- Non-functional requirements modeling (% uplift for testing, deployment, etc.).
- Staffing plan and timeline generation (external AI responsibility).
- Export formats (PDF, CSV, API integration).
- Contingency/risk modeling.
- Feature dependencies and bill-of-materials tracking (if needed).
