namespace REA.Emergencia.Web.Options;

public sealed class AzureAdRoleManagementOptions
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string? TargetAppId { get; set; }
}
