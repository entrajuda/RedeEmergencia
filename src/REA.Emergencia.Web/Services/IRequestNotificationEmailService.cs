namespace REA.Emergencia.Web.Services;

public interface IRequestNotificationEmailService
{
    Task SendRequestSubmittedEmailAsync(string recipientEmail, Guid pedidoGuid, string templateBody, CancellationToken cancellationToken);
    Task SendEmailAsync(string recipientEmail, string subject, string body, bool isHtml, CancellationToken cancellationToken);
}
