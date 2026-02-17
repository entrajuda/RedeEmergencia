using System.ComponentModel.DataAnnotations;

namespace REA.Emergencia.Domain;

public sealed class Instituicao
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string CodigoEA { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Nome { get; set; } = string.Empty;

    public int? ConcelhoId { get; set; }
    public Concelho? Concelho { get; set; }

    public int? DistritoId { get; set; }
    public Distrito? Distrito { get; set; }

    public int? ZinfId { get; set; }
    public Zinf? Zinf { get; set; }

    [MaxLength(200)]
    public string? PessoaContacto { get; set; }

    [MaxLength(50)]
    public string? Telefone { get; set; }

    [MaxLength(50)]
    public string? Telemovel { get; set; }

    [MaxLength(200)]
    public string? Email1 { get; set; }

    public int? CodigoPostalNumero { get; set; }
    public CodigoPostal? CodigoPostal { get; set; }

    [MaxLength(200)]
    public string? Localidade { get; set; }
}
