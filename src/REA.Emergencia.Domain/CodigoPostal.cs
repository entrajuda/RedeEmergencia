using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace REA.Emergencia.Domain;

public sealed class CodigoPostal
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Range(1000000, 9999999, ErrorMessage = "O código postal deve estar entre 1000000 e 9999999.")]
    public int Numero { get; set; }

    [Required]
    [MaxLength(200)]
    public string Freguesia { get; set; } = string.Empty;

    public int ConcelhoId { get; set; }

    public Concelho Concelho { get; set; } = null!;
}
