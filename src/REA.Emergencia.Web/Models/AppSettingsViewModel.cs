using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace REA.Emergencia.Web.Models;

public static class AppSettingKeys
{
    public const string PedidoBensEmailTemplate = "PedidoBensEmailTemplate";
    public const string NovoPedidolTemplate = "NovoPedidolTemplate";
    public const string SendEmailToPedidoCreator = "SendEmailToPedidoCreator";
    public const string SiteTheme = "SiteTheme";
    public const string EmailFrom = "EmailFrom";
}

public sealed class AppSettingsViewModel
{
    [Required(ErrorMessage = "Introduza o email de origem (From).")]
    [EmailAddress(ErrorMessage = "Introduza um email válido para o remetente.")]
    public string EmailFrom { get; set; } = string.Empty;

    [Required(ErrorMessage = "Introduza o conteúdo do email de submissão.")]
    public string PedidoBensEmailTemplate { get; set; } = string.Empty;

    [Required(ErrorMessage = "Introduza o conteúdo do template Novo Pedido.")]
    public string NovoPedidolTemplate { get; set; } = string.Empty;

    public bool SendEmailToPedidoCreator { get; set; } = true;

    [Required(ErrorMessage = "Selecione um tema do site.")]
    public string SiteTheme { get; set; } = "bootstrap-local";

    public IReadOnlyList<SelectListItem> SiteThemeOptions { get; set; } = Array.Empty<SelectListItem>();

    public string? StatusMessage { get; set; }
}
