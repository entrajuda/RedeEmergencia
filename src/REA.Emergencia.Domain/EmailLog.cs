using System.ComponentModel.DataAnnotations;

namespace REA.Emergencia.Domain;

public sealed class EmailLog
{
    public int Id { get; set; }

    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(1000)]
    public string Recipients { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;
}
