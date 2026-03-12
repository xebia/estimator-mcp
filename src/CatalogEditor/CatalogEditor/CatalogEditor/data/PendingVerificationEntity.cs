namespace CatalogEditor.Data;

public class PendingVerificationEntity
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
