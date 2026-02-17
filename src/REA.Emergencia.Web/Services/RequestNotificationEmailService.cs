using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using REA.Emergencia.Web.Options;

namespace REA.Emergencia.Web.Services;

public sealed class RequestNotificationEmailService : IRequestNotificationEmailService
{
    private readonly GraphMailOptions _options;
    private readonly IAppSettingsService _appSettingsService;
    private readonly GraphServiceClient _graphClient;

    public RequestNotificationEmailService(IOptions<GraphMailOptions> options, IAppSettingsService appSettingsService)
    {
        _options = options.Value;
        _appSettingsService = appSettingsService;
        ValidateOptions();

        var credential = new ClientSecretCredential(_options.TenantId, _options.ClientId, _options.ClientSecret);
        _graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
    }

    public async Task SendRequestSubmittedEmailAsync(string recipientEmail, Guid pedidoGuid, string templateBody, CancellationToken cancellationToken)
    {
        var body = templateBody.Replace("{GuidPedido}", pedidoGuid.ToString(), StringComparison.OrdinalIgnoreCase);
        await SendEmailAsync(recipientEmail, _options.Subject, body, isHtml: true, cancellationToken);
    }

    public async Task SendEmailAsync(string recipientEmail, string subject, string body, bool isHtml, CancellationToken cancellationToken)
    {
        var senderUser = await ResolveSenderUserAsync(cancellationToken);

        var requestBody = new SendMailPostRequestBody
        {
            Message = new Message
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = isHtml ? BodyType.Html : BodyType.Text,
                    Content = body
                },
                ToRecipients =
                [
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = recipientEmail
                        }
                    }
                ]
            },
            SaveToSentItems = true
        };

        await _graphClient.Users[senderUser].SendMail.PostAsync(requestBody, cancellationToken: cancellationToken);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.TenantId) ||
            string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new InvalidOperationException("Configuração GraphMail incompleta. Verifique a secção GraphMail no appsettings.");
        }
    }

    private async Task<string> ResolveSenderUserAsync(CancellationToken cancellationToken)
    {
        var configuredSender = await _appSettingsService.GetValueAsync(Models.AppSettingKeys.EmailFrom, cancellationToken);
        var senderUser = string.IsNullOrWhiteSpace(configuredSender)
            ? _options.SenderUserId
            : configuredSender.Trim();

        if (string.IsNullOrWhiteSpace(senderUser))
        {
            throw new InvalidOperationException("Email From não configurado. Defina-o em Backoffice > Configurações.");
        }

        return senderUser;
    }
}
