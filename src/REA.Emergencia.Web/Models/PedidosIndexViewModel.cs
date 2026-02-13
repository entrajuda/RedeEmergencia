using Microsoft.AspNetCore.Mvc.Rendering;

namespace REA.Emergencia.Web.Models;

public sealed class PedidosIndexViewModel
{
    public int? TipoPedidoId { get; set; }
    public IReadOnlyList<SelectListItem> TipoPedidoOptions { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<PedidoListItemViewModel> Pedidos { get; set; } = Array.Empty<PedidoListItemViewModel>();
}

public sealed class PedidoListItemViewModel
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string State { get; set; } = string.Empty;
    public int ExternalRequestID { get; set; }
    public int TipoPedidoId { get; set; }
    public string TipoPedidoName { get; set; } = string.Empty;
}
