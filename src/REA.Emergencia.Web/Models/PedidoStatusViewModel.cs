namespace REA.Emergencia.Web.Models;

public sealed class PedidoStatusViewModel
{
    public Guid PublicId { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string TipoPedido { get; set; } = string.Empty;
}
