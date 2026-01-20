# Gap Analysis: Implementation vs Design Specs

**Date:** 2026-01-20
**Analyzed by:** Claude Code

---

## Completion Checklist

| Status | Issue | Completed | Notes |
|--------|-------|-----------|-------|
| ✅ | Fix Fibonacci scaling multipliers | 2026-01-20 | Commit 57d5b98 |
| ✅ | Add referential integrity validation | 2026-01-20 | Prevents deletion of roles in use; validates role references on catalog entry save |
| ⬜ | Add search/filter to Catalog Editor | - | Low priority |
| ⬜ | Add version management UI | - | Low priority |
| ⬜ | Add caching to MCP server | - | Medium priority |
| ⬜ | Standardize environment variable names | - | Low priority |

---

## ~~Critical Issue: Wrong Fibonacci Scaling Multipliers~~ ✅ RESOLVED

> **Status:** Fixed in commit 57d5b98 on 2026-01-20

~~The T-shirt size multipliers in `CalculateEstimateTool.cs` do not match the specification.~~

### Spec (spec/data-structure.md):
| Size | Fibonacci | Multiplier |
|------|-----------|------------|
| XS | 1 | 0.2x (1/5) |
| S | 2 | 0.4x (2/5) |
| M | 5 | 1.0x (baseline) |
| L | 8 | 1.6x (8/5) |
| XL | 13 | 2.6x (13/5) |

### Implementation (CalculateEstimateTool.cs:137-144):
| Size | Multiplier |
|------|------------|
| XS | 0.5x |
| S | 0.75x |
| M | 1.0x |
| L | 1.5x |
| XL | 2.0x |

**Impact:** Estimates will be substantially different from spec. XS estimates are 2.5x too high, XL estimates are 23% too low.

---

## Missing Features

### MCP Server (`src/estimator-mcp/`)

1. **No validation that RoleIds in estimates reference existing roles** - Could return invalid data if catalog has orphaned role references
2. **No caching** - Catalog reloaded from disk on every tool call (spec mentions caching at runtime)
3. **Environment variable mismatch** - Spec says `CATALOG_DIR`, implementation uses `ESTIMATOR_CATALOG_PATH`

### Catalog Editor (`src/CatalogEditor/`)

1. **No search/filter functionality** on catalog or roles lists
2. **No version management UI** - Files are versioned on disk but no UI to view/compare/rollback previous versions
3. ~~**No referential integrity checks** - Can delete roles that are still referenced by catalog entries~~ ✅ **RESOLVED** - Added `ReferentialIntegrityException` and validation in `JsonCatalogDataProvider.DeleteRoleAsync()` and `SaveCatalogEntryAsync()`
4. **No category dropdown/management** - Free text only, no standardization
5. **No bulk import/export** (CSV, Excel formats)
6. **No API endpoints** - Only web UI access to catalog data

---

## Minor Discrepancies

| Area | Spec | Implementation | Impact |
|------|------|----------------|--------|
| Tool naming | `get_catalog_features` | `GetCatalogFeatures` | None - MCP framework handles conversion |
| Default Copilot multipliers | Dev: 0.70, DevOps: 0.75 | Values come from catalog data | Working as designed |
| JSON features array | Mentioned in overview.md | Derived from catalog via LINQ | Acceptable per data-structure.md |

---

## What's Working Correctly

- All 3 MCP tools implemented and functional (`GetInstructions`, `GetCatalogFeatures`, `CalculateEstimate`)
- Data models match spec (`CatalogData`, `Role`, `CatalogEntry`, `MediumEstimate`)
- Timestamp-based file versioning (`catalog-{ISO8601}.json`)
- Copilot multiplier application (correctly applied after size scaling)
- Full CRUD operations in Catalog Editor
- Thread-safe file operations with semaphore locking
- File-only logging (no stdio interference with MCP protocol)
- Proper async/await patterns throughout

---

## Recommendation Priority

| Priority | Issue | File(s) | Status |
|----------|-------|---------|--------|
| **High** | Fix Fibonacci scaling multipliers | `src/estimator-mcp/Tools/CalculateEstimateTool.cs` | ✅ Done |
| Medium | Add referential integrity validation | `src/CatalogEditor/.../JsonCatalogDataProvider.cs` | ✅ Done |
| Low | Add search/filter to Catalog Editor | `src/CatalogEditor/.../Pages/Catalog.razor` | ⬜ Open |
| Low | Add version management UI | New components needed | ⬜ Open |

---

## References

- Spec: `spec/data-structure.md` - Fibonacci scaling definition
- Spec: `spec/overview.md` - MCP tool specifications
- Spec: `.github/instructions/copilot-instructions.md` - Architecture guidance
- Implementation: `src/estimator-mcp/Tools/CalculateEstimateTool.cs:137-144`
