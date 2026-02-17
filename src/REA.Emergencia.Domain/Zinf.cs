using System.ComponentModel.DataAnnotations;

namespace REA.Emergencia.Domain;

public sealed class Zinf
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    public ICollection<Concelho> Concelhos { get; set; } = new List<Concelho>();
    public ICollection<UserZinf> UserZinfs { get; set; } = new List<UserZinf>();
}
