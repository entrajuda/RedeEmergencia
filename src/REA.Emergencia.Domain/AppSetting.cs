using System.ComponentModel.DataAnnotations;

namespace REA.Emergencia.Domain;

public sealed class AppSetting
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;
}
