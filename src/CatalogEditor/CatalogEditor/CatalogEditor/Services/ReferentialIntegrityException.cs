namespace CatalogEditor.Services;

public class ReferentialIntegrityException : Exception
{
    public string EntityType { get; }
    public string EntityId { get; }
    public IReadOnlyList<string> ReferencingEntities { get; }

    public ReferentialIntegrityException(string entityType, string entityId, IEnumerable<string> referencingEntities)
        : base($"Cannot delete {entityType} '{entityId}' because it is referenced by: {string.Join(", ", referencingEntities)}")
    {
        EntityType = entityType;
        EntityId = entityId;
        ReferencingEntities = referencingEntities.ToList();
    }
}

public class InvalidRoleReferenceException : Exception
{
    public IReadOnlyList<string> InvalidRoleIds { get; }

    public InvalidRoleReferenceException(IEnumerable<string> invalidRoleIds)
        : base($"Invalid role references: {string.Join(", ", invalidRoleIds)}")
    {
        InvalidRoleIds = invalidRoleIds.ToList();
    }
}
