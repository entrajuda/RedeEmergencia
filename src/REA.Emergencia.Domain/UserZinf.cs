using System.ComponentModel.DataAnnotations;

namespace REA.Emergencia.Domain;

public sealed class UserZinf
{
    [Required]
    [MaxLength(256)]
    public string UserPrincipalName { get; set; } = string.Empty;

    public int ZinfId { get; set; }

    public Zinf? Zinf { get; set; }
}
