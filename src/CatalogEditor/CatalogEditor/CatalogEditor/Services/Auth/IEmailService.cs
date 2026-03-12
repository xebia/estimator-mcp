namespace CatalogEditor.Services.Auth;

public interface IEmailService
{
    Task SendVerificationCodeAsync(string email, string code);
}
