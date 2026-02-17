using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using REA.Emergencia.Web.Models;
using REA.Emergencia.Web.Services;

namespace REA.Emergencia.Web.Controllers;

[Authorize(Policy = "BackofficeAdminOnly")]
[Route("backoffice/settings")]
public sealed class BackofficeSettingsController : Controller
{
    private const string TestEmailSubject = "Email de testes Rede Emergência";

    private readonly IAppSettingsService _appSettingsService;
    private readonly IRequestNotificationEmailService _requestNotificationEmailService;

    public BackofficeSettingsController(
        IAppSettingsService appSettingsService,
        IRequestNotificationEmailService requestNotificationEmailService)
    {
        _appSettingsService = appSettingsService;
        _requestNotificationEmailService = requestNotificationEmailService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var template = await _appSettingsService.GetValueAsync(AppSettingKeys.PedidoBensEmailTemplate, cancellationToken)
                       ?? await _appSettingsService.GetValueAsync("SubmissionEmailTemplate", cancellationToken)
                       ?? string.Empty;
        var novoPedidoTemplate = await _appSettingsService.GetValueAsync(AppSettingKeys.NovoPedidolTemplate, cancellationToken) ?? string.Empty;
        var sendEmailToPedidoCreatorRaw = await _appSettingsService.GetValueAsync(AppSettingKeys.SendEmailToPedidoCreator, cancellationToken);
        var selectedTheme = await _appSettingsService.GetValueAsync(AppSettingKeys.SiteTheme, cancellationToken) ?? "bootstrap-local";
        var emailFrom = await _appSettingsService.GetValueAsync(AppSettingKeys.EmailFrom, cancellationToken) ?? string.Empty;
        var sendEmailToPedidoCreator = !string.IsNullOrWhiteSpace(sendEmailToPedidoCreatorRaw)
            ? string.Equals(sendEmailToPedidoCreatorRaw, "true", StringComparison.OrdinalIgnoreCase)
            : true;

        var model = new AppSettingsViewModel
        {
            EmailFrom = emailFrom,
            PedidoBensEmailTemplate = template,
            NovoPedidolTemplate = novoPedidoTemplate,
            SendEmailToPedidoCreator = sendEmailToPedidoCreator,
            SiteTheme = selectedTheme,
            SiteThemeOptions = AppThemeCatalog.GetThemeOptions(selectedTheme)
        };

        return View(model);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AppSettingsViewModel model, CancellationToken cancellationToken)
    {
        model.SiteThemeOptions = AppThemeCatalog.GetThemeOptions(model.SiteTheme);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _appSettingsService.SetValueAsync(AppSettingKeys.PedidoBensEmailTemplate, model.PedidoBensEmailTemplate, cancellationToken);
        await _appSettingsService.SetValueAsync(AppSettingKeys.NovoPedidolTemplate, model.NovoPedidolTemplate, cancellationToken);
        await _appSettingsService.SetValueAsync(AppSettingKeys.SendEmailToPedidoCreator, model.SendEmailToPedidoCreator ? "true" : "false", cancellationToken);
        await _appSettingsService.SetValueAsync(AppSettingKeys.SiteTheme, model.SiteTheme, cancellationToken);
        await _appSettingsService.SetValueAsync(AppSettingKeys.EmailFrom, model.EmailFrom, cancellationToken);

        model.StatusMessage = "Configuração guardada com sucesso.";
        model.SiteThemeOptions = AppThemeCatalog.GetThemeOptions(model.SiteTheme);
        return View(model);
    }

    [HttpGet("email-template")]
    public async Task<IActionResult> EmailTemplate([FromQuery] string settingKey, CancellationToken cancellationToken)
    {
        if (!TryResolveTemplateSetting(settingKey, out var resolvedKey, out var resolvedLabel))
        {
            return BadRequest("Setting de template inválida.");
        }

        var template = await _appSettingsService.GetValueAsync(resolvedKey, cancellationToken) ?? string.Empty;
        if (string.Equals(resolvedKey, AppSettingKeys.PedidoBensEmailTemplate, StringComparison.OrdinalIgnoreCase))
        {
            template = string.IsNullOrWhiteSpace(template)
                ? await _appSettingsService.GetValueAsync("SubmissionEmailTemplate", cancellationToken) ?? string.Empty
                : template;
        }

        var model = new EmailTemplateComposerViewModel
        {
            SettingKey = resolvedKey,
            SettingLabel = resolvedLabel,
            TemplateHtml = template
        };

        return View(model);
    }

    [HttpPost("email-template/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEmailTemplate(EmailTemplateComposerViewModel model, CancellationToken cancellationToken)
    {
        if (!TryResolveTemplateSetting(model.SettingKey, out var resolvedKey, out var resolvedLabel))
        {
            model.ErrorMessage = "Setting de template inválida.";
            return View("EmailTemplate", model);
        }

        model.SettingKey = resolvedKey;
        model.SettingLabel = resolvedLabel;

        if (!ModelState.IsValid)
        {
            return View("EmailTemplate", model);
        }

        await _appSettingsService.SetValueAsync(resolvedKey, model.TemplateHtml, cancellationToken);

        model.StatusMessage = "Template guardado com sucesso.";
        return View("EmailTemplate", model);
    }

    [HttpPost("email-template/send-test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTestEmail(EmailTemplateComposerViewModel model, CancellationToken cancellationToken)
    {
        if (!TryResolveTemplateSetting(model.SettingKey, out var resolvedKey, out var resolvedLabel))
        {
            model.ErrorMessage = "Setting de template inválida.";
            return View("EmailTemplate", model);
        }

        model.SettingKey = resolvedKey;
        model.SettingLabel = resolvedLabel;

        if (string.IsNullOrWhiteSpace(model.TestEmail))
        {
            ModelState.AddModelError(nameof(model.TestEmail), "Introduza um email para teste.");
        }

        if (!ModelState.IsValid)
        {
            return View("EmailTemplate", model);
        }

        try
        {
            await _requestNotificationEmailService.SendEmailAsync(
                model.TestEmail!.Trim(),
                TestEmailSubject,
                model.TemplateHtml,
                isHtml: true,
                cancellationToken);

            model.StatusMessage = "Email de teste enviado com sucesso.";
        }
        catch (Exception ex)
        {
            model.ErrorMessage = $"Falha no envio do email de teste: {ex.Message}";
        }

        return View("EmailTemplate", model);
    }

    private static bool TryResolveTemplateSetting(string? settingKey, out string resolvedKey, out string resolvedLabel)
    {
        if (string.Equals(settingKey, AppSettingKeys.PedidoBensEmailTemplate, StringComparison.OrdinalIgnoreCase))
        {
            resolvedKey = AppSettingKeys.PedidoBensEmailTemplate;
            resolvedLabel = "Email enviado após submissão de pedido";
            return true;
        }

        if (string.Equals(settingKey, AppSettingKeys.NovoPedidolTemplate, StringComparison.OrdinalIgnoreCase))
        {
            resolvedKey = AppSettingKeys.NovoPedidolTemplate;
            resolvedLabel = "Email de novo pedido para utilizadores da ZINF";
            return true;
        }

        resolvedKey = string.Empty;
        resolvedLabel = string.Empty;
        return false;
    }
}
