using System.ComponentModel.DataAnnotations;

namespace REA.Emergencia.Domain;

public sealed class Concelho
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    public int DistritoId { get; set; }

    public Distrito Distrito { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string ZINF { get; set; } = string.Empty;

    public ICollection<CodigoPostal> CodigosPostais { get; set; } = new List<CodigoPostal>();
}
