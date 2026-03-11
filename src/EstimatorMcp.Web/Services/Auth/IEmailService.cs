namespace EstimatorMcp.Web.Services.Auth;

public interface IEmailService
{
    Task SendVerificationCodeAsync(string email, string code);
}
