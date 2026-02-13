using System;
using System.ComponentModel.DataAnnotations;

namespace REA.Emergencia.Domain;

public sealed class TipoPedido
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    public string Workflow { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string TableName { get; set; } = string.Empty;

    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
