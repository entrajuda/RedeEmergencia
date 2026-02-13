using System.ComponentModel.DataAnnotations;

namespace REA.Emergencia.Web.Models;

public sealed class DistritoFormModel
{
    [Required(ErrorMessage = "O distrito é obrigatório.")]
    [MaxLength(200, ErrorMessage = "O distrito não pode exceder 200 caracteres.")]
    public string Distrito { get; set; } = string.Empty;
}
