namespace REA.Emergencia.Web.Models;

public sealed class PedidoDetailsViewModel
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string State { get; set; } = string.Empty;
    public string TipoPedidoName { get; set; } = string.Empty;
    public string TipoPedidoTableName { get; set; } = string.Empty;
    public int ExternalRequestID { get; set; }
    public bool IsSupportedType { get; set; }
    public IReadOnlyList<PedidoDetailFieldViewModel> Fields { get; set; } = Array.Empty<PedidoDetailFieldViewModel>();
}

public sealed class PedidoDetailFieldViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
