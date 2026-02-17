namespace REA.Emergencia.Web.Models;

public sealed class PedidoDetailsViewModel
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string State { get; set; } = string.Empty;
    public string TipoPedidoName { get; set; } = string.Empty;
    public string TipoPedidoTableName { get; set; } = string.Empty;
    public string ZinfName { get; set; } = "-";
    public int ExternalRequestID { get; set; }
    public bool IsSupportedType { get; set; }
    public IReadOnlyList<PedidoDetailFieldViewModel> Fields { get; set; } = Array.Empty<PedidoDetailFieldViewModel>();
    public IReadOnlyList<PedidoEstadoLogItemViewModel> EstadoLogs { get; set; } = Array.Empty<PedidoEstadoLogItemViewModel>();
    public IReadOnlyList<PedidoInstituicaoListItemViewModel> InstituicoesMesmoZinf { get; set; } = Array.Empty<PedidoInstituicaoListItemViewModel>();
}

public sealed class PedidoDetailFieldViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class PedidoInstituicaoListItemViewModel
{
    public string CodigoEA { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? PessoaContacto { get; set; }
    public string? Email1 { get; set; }
}

public sealed class PedidoEstadoLogItemViewModel
{
    public DateTime ChangedAtUtc { get; set; }
    public string FromState { get; set; } = string.Empty;
    public string ToState { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
}
