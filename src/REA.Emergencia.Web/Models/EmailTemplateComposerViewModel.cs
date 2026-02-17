using System.ComponentModel.DataAnnotations;

namespace REA.Emergencia.Web.Models;

public sealed class EmailTemplateComposerViewModel
{
    public string SettingKey { get; set; } = string.Empty;
    public string SettingLabel { get; set; } = string.Empty;

    [Required(ErrorMessage = "Introduza o conteúdo do email.")]
    public string TemplateHtml { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Introduza um email válido para teste.")]
    public string? TestEmail { get; set; }

    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }
}
