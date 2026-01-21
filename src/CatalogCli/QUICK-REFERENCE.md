# Catalog CLI - Quick Reference

## ? Fast Start

### Export existing catalog to Excel
```bash
cd src/CatalogCli
dotnet run -- export -i "../CatalogEditor/CatalogEditor/CatalogEditor/data/catalogs/catalog-latest.json" -o "./export/"
```
Opens `export/entries.tsv` in Excel ? Edit ? Save as Tab Delimited Text

### Import Excel back to JSON
```bash
dotnet run -- import --roles "./export/roles.tsv" --entries "./export/entries.tsv" -o "./new-catalog.json"
```

---

## ?? Column Order (MUST match exactly)

### entries.tsv format:
```
Id	Name	Description	Category	TechStack	Tags	architect	dev	devops	em	qa	security	ux
```

**Important:** 
- ? TechStack comes **before** Tags
- ? Use **semicolons** to separate tags: `salesforce;apex;backend`
- ? Use **tabs** between columns (not commas)

---

## ??? Tech Stack Values

| Use | For |
|-----|-----|
| `salesforce` | Apex, LWC, Flows |
| `blazor-azure` | Blazor + Azure |
| `dotnet` | ASP.NET, SignalR |
| `nodejs` | Node.js, Express |
| `shared` | Cross-platform |
| *(empty)* | Unspecified |

---

## ?? Tag Examples

```
salesforce;apex;backend
frontend;data;ui-component;blazor
authentication;security;backend;shared
devops;ci;cd;automation
```

**Use semicolons (`;`), NOT commas!**

---

## ? Validation Checklist

Before importing:
- [ ] Column order matches exactly (Id, Name, Description, Category, TechStack, Tags, [roles])
- [ ] All IDs are unique
- [ ] Tags use semicolons (`;`) not commas
- [ ] File saved as Tab Delimited Text (.tsv)
- [ ] No extra columns added
- [ ] Role names match between roles.tsv and entries.tsv

---

## ?? Common Errors

| Error | Cause | Fix |
|-------|-------|-----|
| "Expected 6 columns, found 4" | Missing TechStack/Tags columns | Add columns after Category |
| Tags not importing | Using commas instead of semicolons | Change `,` to `;` |
| "Role column 'X' not found" | Role name mismatch | Check roles.tsv and entries.tsv headers match |
| Empty tags | Cell has only spaces/commas | Clear cell or use valid tags |

---

## ?? Import Excel Workflow

1. **Convert Excel to TSV** (if needed):
   ```powershell
   # Run the conversion script
   .\convert-excel-to-tsv.ps1
   ```

2. **Import to JSON**:
   ```bash
   dotnet run -- import --roles "output/roles.tsv" --entries "output/entries.tsv" -o "catalog.json"
   ```

3. **Deploy to MCP**:
   ```powershell
   $ts = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ")
   Copy-Item "catalog.json" "../CatalogEditor/CatalogEditor/CatalogEditor/data/catalogs/catalog-$ts.json"
   ```

4. **Restart Claude Desktop**

---

## ?? Full Documentation

See `README.md` for complete details, examples, and troubleshooting.
