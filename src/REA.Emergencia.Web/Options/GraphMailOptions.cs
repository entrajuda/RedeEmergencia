namespace REA.Emergencia.Web.Options;

public sealed class GraphMailOptions
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SenderUserId { get; set; } = string.Empty;
    public string Subject { get; set; } = "Confirmação do pedido";
}
