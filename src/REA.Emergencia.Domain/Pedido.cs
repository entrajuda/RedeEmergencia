using System;
using System.ComponentModel.DataAnnotations;

namespace REA.Emergencia.Domain;

public sealed class Pedido
{
    public int Id { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(100)]
    public string State { get; set; } = string.Empty;

    public int ExternalRequestID { get; set; }

    public int TipoPedidoId { get; set; }

    public TipoPedido TipoPedido { get; set; } = null!;
}
