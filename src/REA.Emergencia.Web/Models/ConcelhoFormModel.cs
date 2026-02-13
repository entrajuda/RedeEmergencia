using System.ComponentModel.DataAnnotations;

namespace REA.Emergencia.Web.Models;

public sealed class ConcelhoFormModel
{
    [Required(ErrorMessage = "O concelho é obrigatório.")]
    [MaxLength(200, ErrorMessage = "O concelho não pode exceder 200 caracteres.")]
    public string Concelho { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Selecione um distrito.")]
    public int DistritoId { get; set; }

    [Required(ErrorMessage = "O campo ZINF é obrigatório.")]
    [MaxLength(100, ErrorMessage = "O campo ZINF não pode exceder 100 caracteres.")]
    public string ZINF { get; set; } = string.Empty;
}
