using System.ComponentModel.DataAnnotations;

namespace REA.Emergencia.Domain;

public sealed class PedidoEstadoLog
{
    public int Id { get; set; }

    public int PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(100)]
    public string FromState { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ToState { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string ChangedBy { get; set; } = string.Empty;
}
