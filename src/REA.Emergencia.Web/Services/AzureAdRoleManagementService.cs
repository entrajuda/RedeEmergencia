using Azure.Identity;
using Microsoft.Kiota.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using REA.Emergencia.Web.Helpers;
using REA.Emergencia.Web.Options;

namespace REA.Emergencia.Web.Services;

public sealed class AzureAdRoleManagementService : IAzureAdRoleManagementService
{
    private const string AdminRoleValue = "Admin";
    private const string VolunteerRoleValue = "Volunteer";

    private readonly AzureAdRoleManagementOptions _options;
    private readonly ILogger<AzureAdRoleManagementService> _logger;
    private readonly GraphServiceClient _graphClient;

    public AzureAdRoleManagementService(IOptions<AzureAdRoleManagementOptions> options, ILogger<AzureAdRoleManagementService> logger)
    {
        _options = options.Value;
        _logger = logger;
        ValidateOptions(_options);

        var credential = new ClientSecretCredential(_options.TenantId, _options.ClientId, _options.ClientSecret);
        _graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
    }

    public async Task<IReadOnlyList<AzureAdUserRoleAssignment>> GetManagedUserAssignmentsAsync(CancellationToken cancellationToken)
    {
        var context = await LoadContextAsync(cancellationToken);
        var assignments = await GetServicePrincipalAssignmentsAsync(context.ServicePrincipal.Id!, cancellationToken);

        var roleLookup = new Dictionary<Guid, string>();
        if (context.AdminRoleId.HasValue)
        {
            roleLookup[context.AdminRoleId.Value] = AdminRoleValue;
        }
        if (context.VolunteerRoleId.HasValue)
        {
            roleLookup[context.VolunteerRoleId.Value] = VolunteerRoleValue;
        }

        var grouped = assignments
            .Where(x => x.PrincipalType?.Equals("User", StringComparison.OrdinalIgnoreCase) == true)
            .Where(x => x.AppRoleId.HasValue && roleLookup.ContainsKey(x.AppRoleId.Value))
            .GroupBy(x => x.PrincipalId)
            .ToList();

        var result = new List<AzureAdUserRoleAssignment>();
        foreach (var group in grouped)
        {
            if (!group.Key.HasValue)
            {
                continue;
            }

            var userId = group.Key.Value.ToString();
            var user = await _graphClient.Users[userId].GetAsync(cancellationToken: cancellationToken);
            if (user is null)
            {
                continue;
            }

            var assignedRoleIds = group
                .Where(x => x.AppRoleId.HasValue)
                .Select(x => x.AppRoleId!.Value)
                .ToHashSet();

            result.Add(new AzureAdUserRoleAssignment
            {
                UserDisplayName = user.DisplayName ?? user.UserPrincipalName ?? userId,
                UserPrincipalName = user.UserPrincipalName ?? string.Empty,
                IsAdmin = context.AdminRoleId.HasValue && assignedRoleIds.Contains(context.AdminRoleId.Value),
                IsVolunteer = context.VolunteerRoleId.HasValue && assignedRoleIds.Contains(context.VolunteerRoleId.Value)
            });
        }

        return result
            .OrderBy(x => x.UserDisplayName)
            .ThenBy(x => x.UserPrincipalName)
            .ToList();
    }

    public async Task<IReadOnlyList<AzureAdDirectoryUser>> GetDirectoryUsersAsync(CancellationToken cancellationToken)
    {
        var users = new List<AzureAdDirectoryUser>();

        var response = await _graphClient.Users.GetAsync(
            request =>
            {
                request.QueryParameters.Select = ["id", "displayName", "userPrincipalName"];
                request.QueryParameters.Top = 999;
            },
            cancellationToken);

        while (response is not null)
        {
            if (response.Value is not null)
            {
                users.AddRange(response.Value
                    .Where(x => !string.IsNullOrWhiteSpace(x.UserPrincipalName))
                    .Select(x => new AzureAdDirectoryUser
                    {
                        DisplayName = x.DisplayName ?? x.UserPrincipalName ?? string.Empty,
                        UserPrincipalName = x.UserPrincipalName ?? string.Empty
                    }));
            }

            if (string.IsNullOrWhiteSpace(response.OdataNextLink))
            {
                break;
            }

            var nextRequest = new Microsoft.Graph.Users.UsersRequestBuilder(response.OdataNextLink, _graphClient.RequestAdapter);
            response = await nextRequest.GetAsync(cancellationToken: cancellationToken);
        }

        return users
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserPrincipalName)
            .ToList();
    }

    public async Task<string?> ResolveUserEmailAsync(string userPrincipalName, CancellationToken cancellationToken)
    {
        var candidates = UserPrincipalNameNormalizer.BuildCandidates(userPrincipalName);
        foreach (var candidate in candidates)
        {
            try
            {
                var user = await _graphClient.Users[candidate].GetAsync(
                    request =>
                    {
                        request.QueryParameters.Select = ["mail", "userPrincipalName", "otherMails"];
                    },
                    cancellationToken);

                if (user is null)
                {
                    continue;
                }

                var fromOtherMails = user.OtherMails?
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && x.Contains('@'));
                if (!string.IsNullOrWhiteSpace(fromOtherMails))
                {
                    return fromOtherMails.Trim();
                }

                if (!string.IsNullOrWhiteSpace(user.Mail) && user.Mail.Contains('@'))
                {
                    return user.Mail.Trim();
                }

                if (!string.IsNullOrWhiteSpace(user.UserPrincipalName) && user.UserPrincipalName.Contains('@'))
                {
                    return user.UserPrincipalName.Trim();
                }
            }
            catch
            {
                // Try next candidate.
            }
        }

        var normalized = UserPrincipalNameNormalizer.Normalize(userPrincipalName);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            if (normalized.Contains('@'))
            {
                return normalized;
            }

            // For guest-normalized values like user_domain.tld, recover user@domain.tld.
            var firstUnderscore = normalized.IndexOf('_');
            if (firstUnderscore > 0 && firstUnderscore < normalized.Length - 1)
            {
                var recovered = normalized[..firstUnderscore] + "@" + normalized[(firstUnderscore + 1)..];
                if (recovered.Contains('@'))
                {
                    return recovered;
                }
            }
        }

        return null;
    }

    public async Task AssignRolesAsync(string userPrincipalName, bool isAdmin, bool isVolunteer, CancellationToken cancellationToken)
    {
        var context = await LoadContextAsync(cancellationToken);
        var normalizedUpn = userPrincipalName.Trim();
        var user = await _graphClient.Users[normalizedUpn].GetAsync(cancellationToken: cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.Id))
        {
            throw new InvalidOperationException($"Utilizador '{normalizedUpn}' não encontrado no Azure Entra ID.");
        }

        var userObjectId = Guid.Parse(user.Id);
        var servicePrincipalId = Guid.Parse(context.ServicePrincipal.Id!);
        var roleAssignments = await _graphClient.Users[user.Id].AppRoleAssignments.GetAsync(cancellationToken: cancellationToken);
        var currentAssignments = roleAssignments?.Value?
            .Where(x => x.ResourceId == servicePrincipalId)
            .Where(x => x.AppRoleId.HasValue)
            .ToList() ?? new List<AppRoleAssignment>();

        var targetRoleIds = new HashSet<Guid>();
        if (isAdmin)
        {
            if (!context.AdminRoleId.HasValue)
            {
                throw new InvalidOperationException("O role 'Admin' não existe na aplicação.");
            }
            targetRoleIds.Add(context.AdminRoleId.Value);
        }
        if (isVolunteer)
        {
            if (!context.VolunteerRoleId.HasValue)
            {
                throw new InvalidOperationException("O role 'Volunteer' não existe na aplicação.");
            }
            targetRoleIds.Add(context.VolunteerRoleId.Value);
        }

        var managedRoleIds = new HashSet<Guid>();
        if (context.AdminRoleId.HasValue)
        {
            managedRoleIds.Add(context.AdminRoleId.Value);
        }
        if (context.VolunteerRoleId.HasValue)
        {
            managedRoleIds.Add(context.VolunteerRoleId.Value);
        }

        foreach (var assignment in currentAssignments.Where(x => x.AppRoleId.HasValue && managedRoleIds.Contains(x.AppRoleId.Value)))
        {
            if (targetRoleIds.Contains(assignment.AppRoleId!.Value))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(assignment.Id))
            {
                await _graphClient.Users[user.Id].AppRoleAssignments[assignment.Id].DeleteAsync(cancellationToken: cancellationToken);
            }
        }

        var currentRoleIds = currentAssignments
            .Where(x => x.AppRoleId.HasValue)
            .Select(x => x.AppRoleId!.Value)
            .ToHashSet();

        foreach (var roleId in targetRoleIds)
        {
            if (currentRoleIds.Contains(roleId))
            {
                continue;
            }

            var requestBody = new AppRoleAssignment
            {
                PrincipalId = userObjectId,
                ResourceId = servicePrincipalId,
                AppRoleId = roleId
            };

            await _graphClient.Users[user.Id].AppRoleAssignments.PostAsync(requestBody, cancellationToken: cancellationToken);
        }
    }

    public async Task RemoveManagedRolesAsync(string userPrincipalName, CancellationToken cancellationToken)
    {
        var context = await LoadContextAsync(cancellationToken);
        var normalizedUpn = userPrincipalName.Trim();
        var user = await _graphClient.Users[normalizedUpn].GetAsync(cancellationToken: cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.Id))
        {
            throw new InvalidOperationException($"Utilizador '{normalizedUpn}' não encontrado no Azure Entra ID.");
        }

        var servicePrincipalId = Guid.Parse(context.ServicePrincipal.Id!);
        var roleAssignments = await _graphClient.Users[user.Id].AppRoleAssignments.GetAsync(cancellationToken: cancellationToken);
        var currentAssignments = roleAssignments?.Value?
            .Where(x => x.ResourceId == servicePrincipalId)
            .Where(x => x.AppRoleId.HasValue)
            .ToList() ?? new List<AppRoleAssignment>();

        var managedRoleIds = new HashSet<Guid>();
        if (context.AdminRoleId.HasValue)
        {
            managedRoleIds.Add(context.AdminRoleId.Value);
        }
        if (context.VolunteerRoleId.HasValue)
        {
            managedRoleIds.Add(context.VolunteerRoleId.Value);
        }

        foreach (var assignment in currentAssignments.Where(x => x.AppRoleId.HasValue && managedRoleIds.Contains(x.AppRoleId.Value)))
        {
            if (!string.IsNullOrWhiteSpace(assignment.Id))
            {
                await _graphClient.Users[user.Id].AppRoleAssignments[assignment.Id].DeleteAsync(cancellationToken: cancellationToken);
            }
        }
    }

    public async Task<AzureAdRoleDiagnosticsResult> RunDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var targetAppId = string.IsNullOrWhiteSpace(_options.TargetAppId) ? _options.ClientId : _options.TargetAppId;
        var checks = new List<AzureAdRoleDiagnosticCheck>();

        ServicePrincipal? servicePrincipal = null;

        try
        {
            var sps = await _graphClient.ServicePrincipals.GetAsync(
                request =>
                {
                    request.QueryParameters.Filter = $"appId eq '{targetAppId}'";
                    request.QueryParameters.Top = 1;
                },
                cancellationToken);

            servicePrincipal = sps?.Value?.FirstOrDefault();
            if (servicePrincipal is null || string.IsNullOrWhiteSpace(servicePrincipal.Id))
            {
                checks.Add(new AzureAdRoleDiagnosticCheck
                {
                    Name = "Ler Service Principal de destino",
                    Success = false,
                    Message = $"Service Principal com appId '{targetAppId}' não encontrado."
                });
            }
            else
            {
                checks.Add(new AzureAdRoleDiagnosticCheck
                {
                    Name = "Ler Service Principal de destino",
                    Success = true,
                    Message = $"OK. ServicePrincipalId: {servicePrincipal.Id}"
                });
            }
        }
        catch (Exception ex)
        {
            checks.Add(BuildFailedCheck("Ler Service Principal de destino", ex));
        }

        if (servicePrincipal is not null && !string.IsNullOrWhiteSpace(servicePrincipal.Id))
        {
            try
            {
                var assignments = await _graphClient.ServicePrincipals[servicePrincipal.Id].AppRoleAssignedTo.GetAsync(
                    request => request.QueryParameters.Top = 1,
                    cancellationToken);

                checks.Add(new AzureAdRoleDiagnosticCheck
                {
                    Name = "Ler app role assignments (ServicePrincipal.AppRoleAssignedTo)",
                    Success = true,
                    Message = $"OK. Registos lidos: {assignments?.Value?.Count ?? 0}"
                });
            }
            catch (Exception ex)
            {
                checks.Add(BuildFailedCheck("Ler app role assignments (ServicePrincipal.AppRoleAssignedTo)", ex));
            }

            var hasAdmin = servicePrincipal.AppRoles?.Any(x => string.Equals(x.Value, AdminRoleValue, StringComparison.OrdinalIgnoreCase)) == true;
            var hasVolunteer = servicePrincipal.AppRoles?.Any(x => string.Equals(x.Value, VolunteerRoleValue, StringComparison.OrdinalIgnoreCase)) == true;
            checks.Add(new AzureAdRoleDiagnosticCheck
            {
                Name = "Verificar App Roles no Service Principal",
                Success = hasAdmin && hasVolunteer,
                Message = $"Admin: {(hasAdmin ? "encontrado" : "não encontrado")}, Volunteer: {(hasVolunteer ? "encontrado" : "não encontrado")}"
            });
        }

        try
        {
            var users = await _graphClient.Users.GetAsync(
                request => request.QueryParameters.Top = 1,
                cancellationToken);

            checks.Add(new AzureAdRoleDiagnosticCheck
            {
                Name = "Ler utilizadores (Users.Read)",
                Success = true,
                Message = $"OK. Registos lidos: {users?.Value?.Count ?? 0}"
            });
        }
        catch (Exception ex)
        {
            checks.Add(BuildFailedCheck("Ler utilizadores (Users.Read)", ex));
        }

        return new AzureAdRoleDiagnosticsResult
        {
            TenantId = _options.TenantId,
            ClientId = _options.ClientId,
            TargetAppId = targetAppId,
            Checks = checks
        };
    }

    private async Task<IReadOnlyList<AppRoleAssignment>> GetServicePrincipalAssignmentsAsync(string servicePrincipalId, CancellationToken cancellationToken)
    {
        var assignments = new List<AppRoleAssignment>();
        var response = await _graphClient.ServicePrincipals[servicePrincipalId].AppRoleAssignedTo.GetAsync(
            request =>
            {
                request.QueryParameters.Top = 999;
            },
            cancellationToken);

        while (response is not null)
        {
            if (response.Value is not null)
            {
                assignments.AddRange(response.Value);
            }

            if (string.IsNullOrWhiteSpace(response.OdataNextLink))
            {
                break;
            }

            var nextRequest = new Microsoft.Graph.ServicePrincipals.Item.AppRoleAssignedTo.AppRoleAssignedToRequestBuilder(response.OdataNextLink, _graphClient.RequestAdapter);
            response = await nextRequest.GetAsync(cancellationToken: cancellationToken);
        }

        return assignments;
    }

    private async Task<RoleManagementContext> LoadContextAsync(CancellationToken cancellationToken)
    {
        var targetAppId = string.IsNullOrWhiteSpace(_options.TargetAppId) ? _options.ClientId : _options.TargetAppId;
        var servicePrincipals = await _graphClient.ServicePrincipals.GetAsync(
            request =>
            {
                request.QueryParameters.Filter = $"appId eq '{targetAppId}'";
            },
            cancellationToken);

        var servicePrincipal = servicePrincipals?.Value?.FirstOrDefault();
        if (servicePrincipal is null || string.IsNullOrWhiteSpace(servicePrincipal.Id))
        {
            throw new InvalidOperationException($"Service Principal com appId '{targetAppId}' não encontrado.");
        }

        var adminRoleId = servicePrincipal.AppRoles?.FirstOrDefault(x => string.Equals(x.Value, AdminRoleValue, StringComparison.OrdinalIgnoreCase))?.Id;
        var volunteerRoleId = servicePrincipal.AppRoles?.FirstOrDefault(x => string.Equals(x.Value, VolunteerRoleValue, StringComparison.OrdinalIgnoreCase))?.Id;

        return new RoleManagementContext(servicePrincipal, adminRoleId, volunteerRoleId);
    }

    private static void ValidateOptions(AzureAdRoleManagementOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TenantId) ||
            string.IsNullOrWhiteSpace(options.ClientId) ||
            string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            throw new InvalidOperationException(
                "Configuração AzureAdRoleManagement incompleta. Defina TenantId, ClientId e ClientSecret.");
        }
    }

    private static AzureAdRoleDiagnosticCheck BuildFailedCheck(string name, Exception ex)
    {
        if (ex is ApiException apiEx)
        {
            return new AzureAdRoleDiagnosticCheck
            {
                Name = name,
                Success = false,
                Message = $"{apiEx.Message} (StatusCode: {(int?)apiEx.ResponseStatusCode ?? 0})"
            };
        }

        return new AzureAdRoleDiagnosticCheck
        {
            Name = name,
            Success = false,
            Message = ex.Message
        };
    }

    private sealed record RoleManagementContext(ServicePrincipal ServicePrincipal, Guid? AdminRoleId, Guid? VolunteerRoleId);
}
