using System.Text.Json;
using EstimatorMcp.Models;
using EstimatorMcp.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace EstimatorMcp.Web.Services;

public class DbCatalogDataProvider(AppDbContext context) : ICatalogDataProvider
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // ── Mappers ────────────────────────────────────────────────────────────────

    private static TechStack MapTechStack(TechStackEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Description = e.Description,
        Roles = e.Roles.Select(MapRole).ToList()
    };

    private static Role MapRole(RoleEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Description = e.Description,
        CopilotMultiplier = e.CopilotMultiplier,
        TechStackId = e.TechStackId
    };

    private static CatalogEntry MapEntry(CatalogEntryEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Description = e.Description,
        Category = e.Category,
        TechStack = e.TechStackId,
        Tags = e.TagsJson != null ? JsonSerializer.Deserialize<List<string>>(e.TagsJson) : null,
        MediumEstimates = e.Estimates.Select(est => new MediumEstimate
        {
            RoleId = est.RoleId,
            Hours = est.Hours
        }).ToList()
    };

    // ── Full catalog ───────────────────────────────────────────────────────────

    public async Task<CatalogData> LoadCatalogAsync()
    {
        var techStacks = await context.TechStacks
            .Include(ts => ts.Roles)
            .AsNoTracking()
            .ToListAsync();

        var globalRoles = await context.Roles
            .Where(r => r.TechStackId == null)
            .AsNoTracking()
            .ToListAsync();

        var entries = await context.CatalogEntries
            .Include(e => e.Estimates)
            .AsNoTracking()
            .ToListAsync();

        var catalog = new CatalogData
        {
            TechStacks = techStacks.Select(MapTechStack).ToList(),
            GlobalRoles = globalRoles.Select(MapRole).ToList(),
            Catalog = entries.Select(MapEntry).ToList()
        };
        catalog.PopulateRoleTechStackIds();
        return catalog;
    }

    public async Task SaveCatalogAsync(CatalogData catalog)
    {
        var snapshot = JsonSerializer.Serialize(catalog, _jsonOptions);

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // Clear existing data (estimates first due to FK constraints)
            await context.EntryEstimates.ExecuteDeleteAsync();
            await context.CatalogEntries.ExecuteDeleteAsync();
            await context.Roles.ExecuteDeleteAsync();
            await context.TechStacks.ExecuteDeleteAsync();

            // Insert tech stacks
            foreach (var ts in catalog.TechStacks)
            {
                context.TechStacks.Add(new TechStackEntity { Id = ts.Id, Name = ts.Name, Description = ts.Description });
                foreach (var role in ts.Roles)
                    context.Roles.Add(new RoleEntity { Id = role.Id, TechStackId = ts.Id, Name = role.Name, Description = role.Description, CopilotMultiplier = role.CopilotMultiplier });
            }

            // Insert global roles
            foreach (var role in catalog.GlobalRoles)
                context.Roles.Add(new RoleEntity { Id = role.Id, TechStackId = null, Name = role.Name, Description = role.Description, CopilotMultiplier = role.CopilotMultiplier });

            // Insert entries + estimates
            foreach (var entry in catalog.Catalog)
            {
                context.CatalogEntries.Add(new CatalogEntryEntity
                {
                    Id = entry.Id, Name = entry.Name, Description = entry.Description,
                    Category = entry.Category, TechStackId = entry.TechStack,
                    TagsJson = entry.Tags != null ? JsonSerializer.Serialize(entry.Tags) : null,
                    Estimates = entry.MediumEstimates.Select(m => new EntryEstimateEntity
                    {
                        EntryId = entry.Id, RoleId = m.RoleId, Hours = m.Hours
                    }).ToList()
                });
            }

            context.CatalogVersions.Add(new CatalogVersionEntity { CreatedAt = DateTime.UtcNow, SnapshotJson = snapshot });

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ── TechStacks ─────────────────────────────────────────────────────────────

    public async Task<List<TechStack>> GetTechStacksAsync()
    {
        var entities = await context.TechStacks.Include(ts => ts.Roles).AsNoTracking().ToListAsync();
        return entities.Select(MapTechStack).ToList();
    }

    public async Task<TechStack?> GetTechStackAsync(string id)
    {
        var entity = await context.TechStacks.Include(ts => ts.Roles).AsNoTracking().FirstOrDefaultAsync(ts => ts.Id == id);
        return entity == null ? null : MapTechStack(entity);
    }

    public async Task SaveTechStackAsync(TechStack techStack)
    {
        var existing = await context.TechStacks.FirstOrDefaultAsync(ts => ts.Id == techStack.Id);
        if (existing != null)
        {
            existing.Name = techStack.Name;
            existing.Description = techStack.Description;
        }
        else
        {
            context.TechStacks.Add(new TechStackEntity { Id = techStack.Id, Name = techStack.Name, Description = techStack.Description });
        }
        await context.SaveChangesAsync();
    }

    public async Task DeleteTechStackAsync(string id)
    {
        var referencingEntries = await context.CatalogEntries
            .Where(e => e.TechStackId == id)
            .Select(e => e.Name)
            .ToListAsync();

        if (referencingEntries.Count > 0)
            throw new ReferentialIntegrityException("TechStack", id, referencingEntries);

        var entity = await context.TechStacks.Include(ts => ts.Roles).FirstOrDefaultAsync(ts => ts.Id == id);
        if (entity == null) return;

        context.Roles.RemoveRange(entity.Roles);
        context.TechStacks.Remove(entity);
        await context.SaveChangesAsync();
    }

    // ── Roles ──────────────────────────────────────────────────────────────────

    public async Task<List<Role>> GetRolesAsync()
    {
        return (await context.Roles.AsNoTracking().ToListAsync()).Select(MapRole).ToList();
    }

    public async Task<Role?> GetRoleAsync(string id)
    {
        var entity = await context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        return entity == null ? null : MapRole(entity);
    }

    public async Task<List<Role>> GetGlobalRolesAsync()
    {
        return (await context.Roles.Where(r => r.TechStackId == null).AsNoTracking().ToListAsync()).Select(MapRole).ToList();
    }

    public async Task<List<Role>> GetRolesForTechStackAsync(string techStackId)
    {
        return (await context.Roles.Where(r => r.TechStackId == techStackId).AsNoTracking().ToListAsync()).Select(MapRole).ToList();
    }

    public async Task<List<Role>> GetAvailableRolesForEntryAsync(string? techStackId)
    {
        var query = context.Roles.Where(r => r.TechStackId == null);
        if (!string.IsNullOrEmpty(techStackId))
            query = query.Union(context.Roles.Where(r => r.TechStackId == techStackId));
        return (await query.AsNoTracking().ToListAsync()).Select(MapRole).ToList();
    }

    public async Task SaveRoleAsync(Role role)
    {
        var existing = await context.Roles.FirstOrDefaultAsync(r => r.Id == role.Id);
        if (existing != null)
        {
            existing.Name = role.Name;
            existing.Description = role.Description;
            existing.CopilotMultiplier = role.CopilotMultiplier;
            existing.TechStackId = string.IsNullOrEmpty(role.TechStackId) ? null : role.TechStackId;
        }
        else
        {
            context.Roles.Add(new RoleEntity
            {
                Id = role.Id, Name = role.Name, Description = role.Description,
                CopilotMultiplier = role.CopilotMultiplier,
                TechStackId = string.IsNullOrEmpty(role.TechStackId) ? null : role.TechStackId
            });
        }
        await context.SaveChangesAsync();
    }

    public async Task DeleteRoleAsync(string id)
    {
        var referencingEntries = await context.EntryEstimates
            .Where(e => e.RoleId == id)
            .Select(e => e.Entry.Name)
            .Distinct()
            .ToListAsync();

        if (referencingEntries.Count > 0)
            throw new ReferentialIntegrityException("Role", id, referencingEntries);

        var entity = await context.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (entity != null)
        {
            context.Roles.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    // ── Catalog Entries ────────────────────────────────────────────────────────

    public async Task<List<CatalogEntry>> GetCatalogEntriesAsync()
    {
        var entities = await context.CatalogEntries.Include(e => e.Estimates).AsNoTracking().ToListAsync();
        return entities.Select(MapEntry).ToList();
    }

    public async Task<CatalogEntry?> GetCatalogEntryAsync(string id)
    {
        var entity = await context.CatalogEntries.Include(e => e.Estimates).AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        return entity == null ? null : MapEntry(entity);
    }

    public async Task SaveCatalogEntryAsync(CatalogEntry entry)
    {
        var roleIds = await context.Roles.Select(r => r.Id).ToListAsync();
        var invalidRoleIds = entry.MediumEstimates.Select(m => m.RoleId).Where(id => !roleIds.Contains(id)).Distinct().ToList();
        if (invalidRoleIds.Count > 0)
            throw new InvalidRoleReferenceException(invalidRoleIds);

        var existing = await context.CatalogEntries.Include(e => e.Estimates).FirstOrDefaultAsync(e => e.Id == entry.Id);
        if (existing != null)
        {
            existing.Name = entry.Name;
            existing.Description = entry.Description;
            existing.Category = entry.Category;
            existing.TechStackId = entry.TechStack;
            existing.TagsJson = entry.Tags != null ? JsonSerializer.Serialize(entry.Tags) : null;
            context.EntryEstimates.RemoveRange(existing.Estimates);
            existing.Estimates = entry.MediumEstimates.Select(m => new EntryEstimateEntity { EntryId = entry.Id, RoleId = m.RoleId, Hours = m.Hours }).ToList();
        }
        else
        {
            context.CatalogEntries.Add(new CatalogEntryEntity
            {
                Id = entry.Id, Name = entry.Name, Description = entry.Description,
                Category = entry.Category, TechStackId = entry.TechStack,
                TagsJson = entry.Tags != null ? JsonSerializer.Serialize(entry.Tags) : null,
                Estimates = entry.MediumEstimates.Select(m => new EntryEstimateEntity { EntryId = entry.Id, RoleId = m.RoleId, Hours = m.Hours }).ToList()
            });
        }
        await context.SaveChangesAsync();
    }

    public async Task DeleteCatalogEntryAsync(string id)
    {
        var entity = await context.CatalogEntries.FirstOrDefaultAsync(e => e.Id == id);
        if (entity != null)
        {
            context.CatalogEntries.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    // ── Referential Integrity ──────────────────────────────────────────────────

    public async Task<bool> IsRoleReferencedAsync(string roleId) =>
        await context.EntryEstimates.AnyAsync(e => e.RoleId == roleId);

    public async Task<List<CatalogEntry>> GetEntriesReferencingRoleAsync(string roleId)
    {
        var entities = await context.CatalogEntries
            .Include(e => e.Estimates)
            .Where(e => e.Estimates.Any(est => est.RoleId == roleId))
            .AsNoTracking()
            .ToListAsync();
        return entities.Select(MapEntry).ToList();
    }

    public async Task<List<string>> ValidateRoleReferencesAsync(CatalogEntry entry)
    {
        var roleIds = await context.Roles.Select(r => r.Id).ToListAsync();
        return entry.MediumEstimates.Select(m => m.RoleId).Where(id => !roleIds.Contains(id)).Distinct().ToList();
    }
}
