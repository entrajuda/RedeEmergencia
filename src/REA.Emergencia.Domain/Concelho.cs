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

    public int? ZinfId { get; set; }

    public Zinf? Zinf { get; set; }

    public ICollection<CodigoPostal> CodigosPostais { get; set; } = new List<CodigoPostal>();
}
