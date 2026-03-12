using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;

namespace EstimatorMcp.Web.Services.Auth;

public class AzureEmailOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string SenderAddress { get; set; } = string.Empty;
}

public class AzureEmailService(IOptions<AzureEmailOptions> options, ILogger<AzureEmailService> logger) : IEmailService
{
    public async Task SendVerificationCodeAsync(string email, string code)
    {
        if (string.IsNullOrEmpty(options.Value.ConnectionString))
        {
            // ACS not configured — log the code so it can be retrieved from container logs
            logger.LogWarning(
                "ACS email not configured. Verification code for {Email}: {Code}",
                email, code);
            return;
        }

        var client = new EmailClient(options.Value.ConnectionString);

        var message = new EmailMessage(
            senderAddress: options.Value.SenderAddress,
            recipients: new EmailRecipients([new EmailAddress(email)]),
            content: new EmailContent("Your Estimator MCP verification code")
            {
                PlainText = $"Your verification code is: {code}\n\nThis code expires in 15 minutes.",
                Html = $"<p>Your verification code is: <strong>{code}</strong></p><p>This code expires in 15 minutes.</p>"
            });

        var operation = await client.SendAsync(WaitUntil.Completed, message);
        logger.LogInformation("Verification email sent to {Email}, status: {Status}", email, operation.Value.Status);
    }
}
