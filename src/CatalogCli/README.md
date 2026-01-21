# Catalog CLI Tool

Command-line tool for importing and exporting project estimation catalog data between JSON and TSV (tab-separated values) formats.

## Overview

The Catalog CLI enables bulk editing of catalog features using spreadsheet applications like Excel or Google Sheets. It supports:
- Exporting catalog JSON to TSV files for Excel editing
- Importing TSV files back to catalog JSON format
- Validation of catalog data and role references
- **Tech stack categorization** (e.g., Salesforce, Blazor/Azure, Node.js)
- **Tag-based organization** (e.g., frontend, backend, api)

---

## Installation

### Prerequisites
- .NET 10 SDK or later
- Excel or compatible spreadsheet application

### Build
```bash
cd src/CatalogCli
dotnet build
```

---

## File Format Specification

### 1. roles.tsv

Tab-separated file defining team roles and AI productivity multipliers.

**Format:**
```
Id	Name	Description	CopilotMultiplier
```

**Columns:**
| Column | Type | Required | Description | Example |
|--------|------|----------|-------------|---------|
| `Id` | String | ? Yes | Unique role identifier (lowercase, no spaces) | `dev` |
| `Name` | String | ? Yes | Display name | `Developer` |
| `Description` | String | ?? Optional | Role description | `Full-stack developer with modern tooling` |
| `CopilotMultiplier` | Decimal | ? Yes | AI productivity multiplier (0.0-1.0, where 0.6 = 40% faster) | `0.6` |

**Example:**
```tsv
Id	Name	Description	CopilotMultiplier
architect	Solution Architect	Technical architecture and system design	0.8
dev	Developer	Full-stack developer with modern tooling	0.6
devops	DevOps engineer	Infrastructure and deployment automation	0.7
em	Engagement manager	Manages client relationships	1.0
qa	QA Engineer	Quality assurance and test automation	0.65
security	Security Engineer	Security assessment and implementation	0.85
ux	UX Designer	User experience and interface design	0.9
```

---

### 2. entries.tsv

Tab-separated file defining catalog features with effort estimates per role.

**Format:**
```
Id	Name	Description	Category	TechStack	Tags	[role1]	[role2]	...
```

**Fixed Columns (in this exact order):**
| Column | Type | Required | Description | Example |
|--------|------|----------|-------------|---------|
| `Id` | String | ? Yes | Unique feature identifier | `salesforce-apex-class` |
| `Name` | String | ? Yes | Display name | `Apex Class Development` |
| `Description` | String | ?? Optional | Detailed description | `Custom Apex class with unit tests` |
| `Category` | String | ?? Optional | Broad grouping | `feature`, `backend`, `devops` |
| `TechStack` | String | ?? Optional | Technology platform | `salesforce`, `blazor-azure`, `shared` |
| `Tags` | String | ?? Optional | Semicolon-separated tags | `salesforce;apex;backend` |

**Role Columns (dynamic):**
- One column per role (from `roles.tsv`)
- Contains decimal hours for Medium (M) size estimate
- Empty cell = role not involved in this feature

**Example:**
```tsv
Id	Name	Description	Category	TechStack	Tags	architect	dev	devops	em	qa	security	ux
salesforce-apex-class	Apex Class Development	Custom Apex class with unit tests	feature	salesforce	salesforce;apex;backend	4	16	0	2	6	2	0
blazor-data-grid	Advanced Data Grid	Filterable, sortable data grid	feature	blazor-azure	frontend;data;ui-component	0	32	0	0.5	0	0	8
user-authentication	User Authentication	OAuth2/OIDC authentication	feature	shared	authentication;security;backend	0	30	0	1	0	8	0
```

---

## Usage

### Export Catalog to TSV

Export an existing JSON catalog to TSV files for editing:

```bash
dotnet run -- export -i <input.json> -o <output-directory>
```

**Example:**
```bash
dotnet run -- export \
  -i "../CatalogEditor/CatalogEditor/CatalogEditor/data/catalogs/catalog-2025-12-14T20-10-24Z.json" \
  -o "./tsv-export/"
```

**Output:**
- `./tsv-export/roles.tsv` - Team roles
- `./tsv-export/entries.tsv` - Catalog features

---

### Edit in Excel

1. **Open TSV files** in Excel (File ? Open ? Select TSV file)
2. **Edit data** as needed
3. **Important:** Keep the column order exactly as exported
4. **Add new entries** by adding rows (use existing entries as templates)
5. **Tags:** Use semicolons to separate tags: `salesforce;apex;backend`
6. **TechStack:** Use one of: `salesforce`, `blazor-azure`, `dotnet`, `nodejs`, `shared`, or leave empty
7. **Save as Tab Delimited Text** (`.tsv` or `.txt` with tab delimiter)

**?? Common Mistakes to Avoid:**
- ? Don't use commas to separate tags (use semicolons: `;`)
- ? Don't reorder columns
- ? Don't add or remove role columns (edit `roles.tsv` first if needed)
- ? Don't use spaces in IDs (use hyphens: `my-feature-id`)

---

### Import TSV Back to JSON

Import edited TSV files to create a new catalog JSON:

```bash
dotnet run -- import --roles <roles.tsv> --entries <entries.tsv> -o <output.json>
```

**Example:**
```bash
dotnet run -- import \
  --roles "./tsv-export/roles.tsv" \
  --entries "./tsv-export/entries.tsv" \
  -o "./updated-catalog.json"
```

**What happens:**
1. ? Validates file format and headers
2. ? Checks all role references exist
3. ? Parses semicolon-separated tags into arrays
4. ? Reports validation errors with file, row, and message
5. ? Creates timestamped JSON catalog if validation passes

**Example Output:**
```
Reading roles.tsv...
Reading entries.tsv...

???????????????????
? Item    ? Count ?
???????????????????
? Roles   ?   7   ?
? Entries ?  54   ?
???????????????????

Validation passed ?

Catalog written to: ./updated-catalog.json
```

---

## Importing Excel Files (.xlsx)

Excel files must be converted to TSV format before importing. Use this PowerShell script:

### Step 1: Export Excel to TSV

Create a script `convert-excel-to-tsv.ps1`:

```powershell
# Convert Excel to TSV format for CLI import
$excelPath = "C:\path\to\your\catalog.xlsx"
$outputDir = "C:\path\to\output"

# Create output directory
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

# Export Excel to TSV using COM
$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$wb = $excel.Workbooks.Open($excelPath)
$ws = $wb.Worksheets.Item(1)
$ws.SaveAs("$outputDir\temp-catalog.tsv", 42) # 42 = Tab-delimited format
$excel.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null

Write-Host "Exported to: $outputDir\temp-catalog.tsv"
```

### Step 2: Reorder Columns (if needed)

If your Excel has columns in a different order than the CLI expects, use the conversion script provided in the Downloads folder:

```powershell
.\convert-excel-to-tsv.ps1
```

This script:
- Reads your Excel export
- Reorders columns to: `Id, Name, Description, Category, TechStack, Tags, [roles...]`
- Creates separate `roles.tsv` and `entries.tsv` files
- Ready for CLI import

### Step 3: Import to JSON

```bash
dotnet run -- import --roles "output/roles.tsv" --entries "output/entries.tsv" -o "catalog.json"
```

---

## Complete Workflow Example

### Scenario: Bulk update 50 Salesforce features

**Step 1: Export existing catalog**
```bash
cd src/CatalogCli
dotnet run -- export \
  -i "../CatalogEditor/CatalogEditor/CatalogEditor/data/catalogs/catalog-latest.json" \
  -o "./export/"
```

**Step 2: Edit in Excel**
- Open `./export/entries.tsv` in Excel
- Make changes (update descriptions, adjust hours, add tags)
- Save as tab-delimited text

**Step 3: Import back**
```bash
dotnet run -- import \
  --roles "./export/roles.tsv" \
  --entries "./export/entries.tsv" \
  -o "./updated-catalog.json"
```

**Step 4: Deploy to catalog directory**
```bash
# Create timestamped filename
$timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ")
Copy-Item "./updated-catalog.json" \
  "../CatalogEditor/CatalogEditor/CatalogEditor/data/catalogs/catalog-$timestamp.json"
```

**Step 5: Restart MCP server**
- Quit Claude Desktop completely
- Reopen - it will automatically use the latest catalog file

---

## Tech Stack Values

Use these standard values for the `TechStack` column:

| Value | Description | Example Features |
|-------|-------------|------------------|
| `salesforce` | Salesforce platform | Apex classes, LWC, Flows, Custom Objects |
| `blazor-azure` | Blazor + Azure | Blazor components, AKS, Azure Functions |
| `dotnet` | .NET/ASP.NET Core | SignalR, Entity Framework, Web API |
| `nodejs` | Node.js ecosystem | Express, React, Next.js |
| `react-aws` | React + AWS | React components, Lambda, S3 |
| `shared` | Cross-platform | Authentication, CRUD, Testing, DevOps |
| *(empty)* | Unspecified | Treated as shared/general |

**Custom values:** You can use any value, but these are recommended for consistency.

---

## Tag Best Practices

Tags enable multi-dimensional categorization. Use semicolons (`;`) to separate multiple tags.

**Examples:**

| Feature | Tags | Why |
|---------|------|-----|
| Salesforce Apex Class | `salesforce;apex;backend` | Platform + technology + layer |
| Blazor Data Grid | `frontend;data;ui-component;blazor` | Layer + purpose + type + platform |
| User Authentication | `authentication;security;backend;shared` | Function + concern + layer + platform |
| CI/CD Pipeline | `devops;ci;cd;automation` | Domain + specific types + characteristic |

**Tag categories:**
- **Platform:** `salesforce`, `blazor`, `azure`, `nodejs`, `react`
- **Layer:** `frontend`, `backend`, `database`, `api`
- **Function:** `authentication`, `authorization`, `crud`, `search`, `reporting`
- **Technology:** `apex`, `lwc`, `signalr`, `terraform`, `kubernetes`
- **Domain:** `devops`, `security`, `testing`, `data`

---

## Validation Rules

The importer validates:

### Roles (roles.tsv)
- ? `Id` must be unique and non-empty
- ? `Name` is required
- ? `CopilotMultiplier` must be a valid decimal number

### Entries (entries.tsv)
- ? Header must have exactly 6 fixed columns + role columns
- ? Column order must be: `Id, Name, Description, Category, TechStack, Tags, [roles...]`
- ? `Id` must be unique and non-empty
- ? `Name` is required
- ? All role column names must exist in `roles.tsv`
- ? Hour values must be valid decimal numbers or empty

### Validation Error Format
```
entries.tsv:15 - Invalid hours '16.5x' for role 'dev' in entry 'my-feature-id'
entries.tsv:23 - Duplicate entry Id 'user-auth'
roles.tsv:5 - Invalid CopilotMultiplier 'abc' for role 'dev'
```

---

## Troubleshooting

### Problem: "Expected 6 columns, found 4"
**Cause:** Using old TSV format without TechStack and Tags columns  
**Fix:** Export from latest catalog JSON to get proper format, or manually add TechStack and Tags columns after Description and Category

### Problem: Tags not importing correctly
**Cause:** Using commas instead of semicolons  
**Fix:** Replace commas with semicolons: `frontend,backend` ? `frontend;backend`

### Problem: "Role column 'developer' not found in roles.tsv"
**Cause:** Mismatch between entry role columns and roles file  
**Fix:** Ensure role IDs in entries.tsv header match exactly with roles.tsv Id column (case-sensitive)

### Problem: Excel corrupting data
**Cause:** Excel auto-formatting numbers or dates  
**Fix:** 
1. Format cells as Text before pasting data
2. Use "Import Data" feature instead of opening TSV directly
3. Save as "Text (Tab delimited) (*.txt)" not "CSV"

### Problem: Import creates empty Tags
**Cause:** Tags cell contains only whitespace or commas  
**Fix:** Either remove the content entirely (empty cell) or use valid semicolon-separated tags

---

## Tips for Large Catalogs

**For 100+ entries:**
- Use Excel's filter feature to focus on specific categories
- Sort by TechStack to edit platform-specific features together
- Use Excel formulas to bulk-update hours (e.g., increase all dev hours by 20%)
- Split large catalogs into multiple files by tech stack for team editing

**Version control:**
- Keep exported TSV files in version control alongside JSON
- Use Git diff to review changes before importing
- Tag releases: `git tag catalog-v1.2.0`

**Backup strategy:**
- Catalog JSON files are timestamped automatically
- Old versions remain in the catalog directory
- Export TSV regularly as backup format

---

## Command Reference

### Export
```bash
dotnet run -- export -i <input-json> -o <output-dir>
```

**Options:**
- `-i, --input` - Path to input catalog JSON file (required)
- `-o, --output` - Output directory for TSV files (required)

### Import
```bash
dotnet run -- import --roles <roles-tsv> --entries <entries-tsv> -o <output-json>
```

**Options:**
- `--roles` - Path to roles.tsv file (required)
- `--entries` - Path to entries.tsv file (required)
- `-o, --output` - Path for output catalog JSON file (required)

---

## Integration with MCP Server

After importing a new catalog:

1. **Copy to catalog directory:**
   ```bash
   $timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ")
   Copy-Item "imported-catalog.json" \
     "../CatalogEditor/data/catalogs/catalog-$timestamp.json"
   ```

2. **Restart Claude Desktop** - it automatically uses the latest file by timestamp

3. **Test filtering:**
   ```
   Claude: "Show me all Salesforce features"
   ? Calls get_catalog_features(techStack="salesforce")
   
   Claude: "List all frontend features"
   ? Calls get_catalog_features(tag="frontend")
   ```

---

## See Also

- **CatalogEditor (Blazor)** - Web UI for editing individual catalog entries
- **MCP Server** - Provides catalog features to Claude Desktop via Model Context Protocol
- **AI Instructions** - `src/estimator-mcp/data/instructions.md` - teaches Claude how to use tech stacks and tags

---

## Support

For issues or questions:
1. Check validation error messages for specific problems
2. Verify TSV file format matches specification above
3. Test with a small catalog (5-10 entries) first
4. Review existing catalog JSON files for examples

**Common support resources:**
- Example catalog: `src/CatalogEditor/CatalogEditor/CatalogEditor/data/catalogs/catalog-2025-12-14T20-10-24Z.json`
- Conversion script: User Downloads folder `convert-excel-to-tsv.ps1`
- Format validator: Built into import command
