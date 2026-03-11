namespace EstimatorMcp.Web.Services.Auth;

public interface IVerificationService
{
    /// <summary>Creates a 6-digit code for the given email, stores hashed, returns raw code.</summary>
    Task<string> CreateVerificationAsync(string email);

    /// <summary>Returns true and deletes the record if code is valid and not expired.</summary>
    Task<bool> ValidateCodeAsync(string email, string code);
}
