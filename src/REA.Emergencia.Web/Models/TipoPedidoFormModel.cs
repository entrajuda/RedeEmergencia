using System.ComponentModel.DataAnnotations;

namespace REA.Emergencia.Web.Models;

public sealed class TipoPedidoFormModel
{
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [MaxLength(200, ErrorMessage = "O nome não pode exceder 200 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "O workflow é obrigatório.")]
    public string Workflow { get; set; } = string.Empty;

    [Required(ErrorMessage = "O nome da tabela é obrigatório.")]
    [MaxLength(200, ErrorMessage = "O nome da tabela não pode exceder 200 caracteres.")]
    public string TableName { get; set; } = string.Empty;
}
