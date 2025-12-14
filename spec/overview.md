# MCP Server System for Project Estimate Generation

1. 🎯 Project Goal
To create a system for generating project estimates in a consulting context by managing a catalog of tasks/features (function points) with associated effort, cost profiles, and team roles, and allowing users to create project estimates based on this catalog.

2. 📝 Key Features and Requirements
The MCP Server system must support the following capabilities:

| Feature Area | Requirement/Description |
|--------------|-------------------------|
| Catalog Management | • Define and maintain a central catalog of function points/features.<br>• Each item in the catalog must be associable with specific roles (e.g., Developer, DevOps Engineer).<br>• Each catalog entry maps effort in t-shirt sizing (XS, S, M, L, XL) to time estimates per role.<br>• Catalog stored as JSON with timestamp-based versioning (automatic version history). |
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
Catalog entries link features/tasks to roles and time estimates:

- **Feature/Task:** Name, Description, Category (optional tags/domains).
- **T-Shirt Sizing Mapping:** Each feature maps to effort size (XS, S, M, L, XL).
- **Role-Time Mapping:** For each feature and sizing level, estimated hours/days per role (Developer, DevOps Engineer, Engagement Manager).
- **Productivity Adjustment:** Column for Copilot-enhanced productivity multiplier per role-task combo (applied to historical baseline estimates).

B. Catalog Storage
- JSON format stored on disk.
- Filename includes timestamp (e.g., `catalog-2025-12-12T14-30-00Z.json`) for automatic version history.
- No database, security, or user roles in MVP; all future enhancements.

C. MCP Server Interface
The MCP server exposes tools with detailed descriptions (used by LLM for tool selection):

1. **instructions** (tool): Returns markdown document with overall guidance for the LLM on how to use the MCP server.
2. **estimate** (tool): Accepts a list of tasks (with t-shirt sizing) and returns per-task, per-role time breakdown.
3. **catalog-query** (tool): Allows LLM to search/browse available catalog items.
4. Top-level MCP description directs the LLM to invoke the `instructions` tool first.

D. AI/LLM Workflow (MVP)
For the proof-of-concept:
1. LLM interviews user to collect task/feature descriptions and t-shirt sizing.
2. LLM calls MCP server `estimate` tool with selected tasks.
3. MCP server returns time breakdown per role per task.
4. LLM presents results to user; higher-level formatting/staffing plans are AI responsibility.

5. 🗺️ Next Steps

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
