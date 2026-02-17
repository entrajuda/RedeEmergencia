namespace REA.Emergencia.Web.Services;

public interface IAzureAdRoleManagementService
{
    Task<IReadOnlyList<AzureAdUserRoleAssignment>> GetManagedUserAssignmentsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AzureAdDirectoryUser>> GetDirectoryUsersAsync(CancellationToken cancellationToken);
    Task<string?> ResolveUserEmailAsync(string userPrincipalName, CancellationToken cancellationToken);
    Task AssignRolesAsync(string userPrincipalName, bool isAdmin, bool isVolunteer, CancellationToken cancellationToken);
    Task RemoveManagedRolesAsync(string userPrincipalName, CancellationToken cancellationToken);
    Task<AzureAdRoleDiagnosticsResult> RunDiagnosticsAsync(CancellationToken cancellationToken);
}

public sealed class AzureAdUserRoleAssignment
{
    public string UserDisplayName { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsVolunteer { get; set; }
}

public sealed class AzureAdDirectoryUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;
}

public sealed class AzureAdRoleDiagnosticsResult
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string TargetAppId { get; set; } = string.Empty;
    public IReadOnlyList<AzureAdRoleDiagnosticCheck> Checks { get; set; } = Array.Empty<AzureAdRoleDiagnosticCheck>();
}

public sealed class AzureAdRoleDiagnosticCheck
{
    public string Name { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
