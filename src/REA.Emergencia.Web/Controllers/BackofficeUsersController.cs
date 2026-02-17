using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using REA.Emergencia.Data;
using REA.Emergencia.Web.Helpers;
using REA.Emergencia.Web.Models;
using REA.Emergencia.Web.Services;

namespace REA.Emergencia.Web.Controllers;

[Authorize(Policy = "BackofficeAdminOnly")]
[Route("backoffice/users")]
public sealed class BackofficeUsersController : Controller
{
    private readonly IAzureAdRoleManagementService _roleManagementService;
    private readonly ApplicationDbContext _dbContext;

    public BackofficeUsersController(IAzureAdRoleManagementService roleManagementService, ApplicationDbContext dbContext)
    {
        _roleManagementService = roleManagementService;
        _dbContext = dbContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? status = null, CancellationToken cancellationToken = default)
    {
        var model = new BackofficeUsersIndexViewModel
        {
            StatusMessage = status
        };

        try
        {
            var users = await _roleManagementService.GetManagedUserAssignmentsAsync(cancellationToken);
            var userZinfAssignments = await _dbContext.UserZinfs
                .AsNoTracking()
                .Include(x => x.Zinf)
                .ToListAsync(cancellationToken);

            var zinfInfoByUpn = userZinfAssignments
                .GroupBy(x => UserPrincipalNameNormalizer.Normalize(x.UserPrincipalName), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .Select(x => x.Zinf?.Nome)
                        .OfType<string>()
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);

            var directoryUsers = await _roleManagementService.GetDirectoryUsersAsync(cancellationToken);
            model.Users = users
                .Select(x => new BackofficeUserRoleItemViewModel
                {
                    UserDisplayName = x.UserDisplayName,
                    UserPrincipalName = x.UserPrincipalName,
                    IsAdmin = x.IsAdmin,
                    IsVolunteer = x.IsVolunteer,
                    TotalZinfs = zinfInfoByUpn.TryGetValue(UserPrincipalNameNormalizer.Normalize(x.UserPrincipalName), out var names) ? names.Count : 0,
                    AssignedZinfNames = zinfInfoByUpn.TryGetValue(UserPrincipalNameNormalizer.Normalize(x.UserPrincipalName), out var assignedNames)
                        ? assignedNames
                        : Array.Empty<string>()
                })
                .ToList();
            model.DirectoryUserOptions = BuildDirectoryUserOptions(directoryUsers, model.AssignInput.UserPrincipalName);
        }
        catch (Exception ex)
        {
            model.ErrorMessage = ex.Message;
        }

        return View(model);
    }

    [HttpGet("add")]
    public async Task<IActionResult> Add([FromQuery] string? status = null, CancellationToken cancellationToken = default)
    {
        var model = await BuildAddViewAsync(new AssignAppRolesInputModel(), cancellationToken, [], statusMessage: status);
        return View(model);
    }

    [HttpGet("diagnostics")]
    public async Task<IActionResult> Diagnostics(CancellationToken cancellationToken)
    {
        var model = await _roleManagementService.RunDiagnosticsAsync(cancellationToken);
        return View(model);
    }

    [HttpPost("assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign([Bind(Prefix = "AssignInput")] AssignAppRolesInputModel input, CancellationToken cancellationToken)
    {
        var logs = new List<string>
        {
            $"Pedido recebido para utilizador '{input.UserPrincipalName}'.",
            $"Perfis solicitados: Admin={(input.IsAdmin ? "Sim" : "Não")}, Volunteer={(input.IsVolunteer ? "Sim" : "Não")}."
        };

        if (!input.IsAdmin && !input.IsVolunteer)
        {
            ModelState.AddModelError(string.Empty, "Selecione pelo menos um perfil (Admin ou Volunteer).");
            logs.Add("Validação falhou: nenhum perfil selecionado.");
        }

        if (!ModelState.IsValid)
        {
            var model = await BuildAddViewAsync(input, cancellationToken, logs, errorMessage: "Pedido inválido. Corrija os erros e tente novamente.");
            return View(nameof(Add), model);
        }

        try
        {
            logs.Add("A iniciar chamada ao Microsoft Graph para atribuição de perfis...");
            await _roleManagementService.AssignRolesAsync(
                input.UserPrincipalName,
                input.IsAdmin,
                input.IsVolunteer,
                cancellationToken);
            logs.Add("Chamada ao Microsoft Graph concluída sem erro.");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            logs.Add($"Erro ao atribuir perfis: {ex.Message}");
            var model = await BuildAddViewAsync(input, cancellationToken, logs, errorMessage: "Falha ao atribuir perfis.");
            return View(nameof(Add), model);
        }

        return RedirectToAction(nameof(Index), new { status = $"Perfis atualizados com sucesso para {input.UserPrincipalName}." });
    }

    [HttpPost("remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveUserRoles([FromForm] string userPrincipalName, CancellationToken cancellationToken)
    {
        var logs = new List<string>
        {
            $"Pedido recebido para remover perfis do utilizador '{userPrincipalName}'."
        };

        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            logs.Add("Validação falhou: utilizador não indicado.");
            return await BuildUsersViewAsync(new AssignAppRolesInputModel(), cancellationToken, logs, errorMessage: "Utilizador inválido.");
        }

        try
        {
            logs.Add("A iniciar chamada ao Microsoft Graph para remoção de perfis...");
            await _roleManagementService.RemoveManagedRolesAsync(userPrincipalName, cancellationToken);
            logs.Add("Perfis removidos com sucesso.");

            var upnCandidates = UserPrincipalNameNormalizer.BuildCandidates(userPrincipalName);
            if (upnCandidates.Count > 0)
            {
                logs.Add("A remover associações ZINF locais do utilizador...");
                var userZinfs = await _dbContext.UserZinfs
                    .Where(x => upnCandidates.Contains(x.UserPrincipalName))
                    .ToListAsync(cancellationToken);

                if (userZinfs.Count > 0)
                {
                    _dbContext.UserZinfs.RemoveRange(userZinfs);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    logs.Add($"Associações ZINF removidas: {userZinfs.Count}.");
                }
                else
                {
                    logs.Add("Sem associações ZINF para remover.");
                }
            }
        }
        catch (Exception ex)
        {
            logs.Add($"Erro ao remover perfis: {ex.Message}");
            return await BuildUsersViewAsync(new AssignAppRolesInputModel(), cancellationToken, logs, errorMessage: "Falha ao remover perfis do utilizador.");
        }

        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var viewResult = await BuildUsersViewAsync(
                new AssignAppRolesInputModel(),
                cancellationToken,
                logs,
                statusMessage: $"Perfis removidos com sucesso para {userPrincipalName}.");

            if (viewResult is ViewResult vr && vr.Model is BackofficeUsersIndexViewModel vm)
            {
                var stillVisible = vm.Users.Any(x =>
                    string.Equals(x.UserPrincipalName, userPrincipalName, StringComparison.OrdinalIgnoreCase));

                if (!stillVisible)
                {
                    logs.Add($"Utilizador removido da lista com sucesso (tentativa {attempt}/{maxAttempts}).");
                    vm.OperationLogs = logs;
                    return viewResult;
                }

                logs.Add($"Utilizador ainda visível na lista (tentativa {attempt}/{maxAttempts}).");
                if (attempt == maxAttempts)
                {
                    logs.Add("Aviso: o utilizador ainda aparece na lista devido a atraso de propagação. Atualize novamente em alguns segundos.");
                    vm.ErrorMessage ??= "Remoção executada, mas a lista ainda não refletiu a alteração.";
                    vm.OperationLogs = logs;
                    return viewResult;
                }
            }

            await Task.Delay(800, cancellationToken);
        }

        return await BuildUsersViewAsync(
            new AssignAppRolesInputModel(),
            cancellationToken,
            logs,
            statusMessage: $"Perfis removidos com sucesso para {userPrincipalName}.");
    }

    [HttpGet("{userPrincipalName}/zinfs")]
    public async Task<IActionResult> ManageZinfs([FromRoute] string userPrincipalName, [FromQuery] string? status = null, CancellationToken cancellationToken = default)
    {
        var model = await BuildManageZinfsViewModelAsync(userPrincipalName, cancellationToken);
        model.StatusMessage = status;
        return View(model);
    }

    [HttpPost("{userPrincipalName}/zinfs")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageZinfs(
        [FromRoute] string userPrincipalName,
        [FromForm] int[] selectedZinfIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedUpn = UserPrincipalNameNormalizer.Normalize(userPrincipalName);
        if (string.IsNullOrWhiteSpace(normalizedUpn))
        {
            return RedirectToAction(nameof(Index), new { status = "Utilizador inválido." });
        }

        var validZinfIds = await _dbContext.Zinfs
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var selectedSet = selectedZinfIds
            .Distinct()
            .Where(validZinfIds.Contains)
            .ToHashSet();

        var currentAssociations = await _dbContext.UserZinfs
            .Where(x => x.UserPrincipalName == normalizedUpn)
            .ToListAsync(cancellationToken);

        var toRemove = currentAssociations
            .Where(x => !selectedSet.Contains(x.ZinfId))
            .ToList();

        if (toRemove.Count > 0)
        {
            _dbContext.UserZinfs.RemoveRange(toRemove);
        }

        var existingZinfIds = currentAssociations
            .Select(x => x.ZinfId)
            .ToHashSet();

        var toAdd = selectedSet
            .Where(x => !existingZinfIds.Contains(x))
            .Select(zinfId => new Domain.UserZinf
            {
                UserPrincipalName = normalizedUpn,
                ZinfId = zinfId
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            await _dbContext.UserZinfs.AddRangeAsync(toAdd, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(
            nameof(ManageZinfs),
            new
            {
                userPrincipalName = normalizedUpn,
                status = "Zonas de influência atualizadas com sucesso."
            });
    }

    private async Task<IActionResult> BuildUsersViewAsync(
        AssignAppRolesInputModel input,
        CancellationToken cancellationToken,
        List<string> logs,
        string? statusMessage = null,
        string? errorMessage = null)
    {
        var model = new BackofficeUsersIndexViewModel
        {
            AssignInput = input,
            StatusMessage = statusMessage,
            ErrorMessage = errorMessage,
            OperationLogs = logs
        };

        try
        {
            logs.Add("A obter lista de utilizadores com perfis atribuídos...");
            var users = await _roleManagementService.GetManagedUserAssignmentsAsync(cancellationToken);
            logs.Add($"Utilizadores com perfis carregados: {users.Count}.");

            logs.Add("A obter lista de utilizadores do diretório...");
            var directoryUsers = await _roleManagementService.GetDirectoryUsersAsync(cancellationToken);
            logs.Add($"Utilizadores disponíveis no diretório: {directoryUsers.Count}.");

            var userZinfAssignments = await _dbContext.UserZinfs
                .AsNoTracking()
                .Include(x => x.Zinf)
                .ToListAsync(cancellationToken);

            var zinfInfoByUpn = userZinfAssignments
                .GroupBy(x => UserPrincipalNameNormalizer.Normalize(x.UserPrincipalName), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .Select(x => x.Zinf?.Nome)
                        .OfType<string>()
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);

            model.Users = users
                .Select(x => new BackofficeUserRoleItemViewModel
                {
                    UserDisplayName = x.UserDisplayName,
                    UserPrincipalName = x.UserPrincipalName,
                    IsAdmin = x.IsAdmin,
                    IsVolunteer = x.IsVolunteer,
                    TotalZinfs = zinfInfoByUpn.TryGetValue(UserPrincipalNameNormalizer.Normalize(x.UserPrincipalName), out var names) ? names.Count : 0,
                    AssignedZinfNames = zinfInfoByUpn.TryGetValue(UserPrincipalNameNormalizer.Normalize(x.UserPrincipalName), out var assignedNames)
                        ? assignedNames
                        : Array.Empty<string>()
                })
                .ToList();
            model.DirectoryUserOptions = BuildDirectoryUserOptions(directoryUsers, model.AssignInput.UserPrincipalName);
        }
        catch (Exception ex)
        {
            model.ErrorMessage = string.IsNullOrWhiteSpace(model.ErrorMessage) ? ex.Message : model.ErrorMessage;
            logs.Add($"Erro a carregar dados para a página: {ex.Message}");
        }

        return View(nameof(Index), model);
    }

    private static IReadOnlyList<SelectListItem> BuildDirectoryUserOptions(IReadOnlyList<AzureAdDirectoryUser> users, string? selectedUserPrincipalName)
    {
        var options = users
            .Select(x => new SelectListItem
            {
                Value = x.UserPrincipalName,
                Text = $"{x.DisplayName} ({x.UserPrincipalName})",
                Selected = string.Equals(x.UserPrincipalName, selectedUserPrincipalName, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();

        options.Insert(0, new SelectListItem
        {
            Value = string.Empty,
            Text = "Selecione um utilizador"
        });

        return options;
    }

    private async Task<BackofficeUsersAddViewModel> BuildAddViewAsync(
        AssignAppRolesInputModel input,
        CancellationToken cancellationToken,
        List<string> logs,
        string? statusMessage = null,
        string? errorMessage = null)
    {
        var model = new BackofficeUsersAddViewModel
        {
            AssignInput = input,
            StatusMessage = statusMessage,
            ErrorMessage = errorMessage,
            OperationLogs = logs
        };

        try
        {
            logs.Add("A obter lista de utilizadores do diretório...");
            var directoryUsers = await _roleManagementService.GetDirectoryUsersAsync(cancellationToken);
            logs.Add($"Utilizadores disponíveis no diretório: {directoryUsers.Count}.");
            model.DirectoryUserOptions = BuildDirectoryUserOptions(directoryUsers, model.AssignInput.UserPrincipalName);
        }
        catch (Exception ex)
        {
            model.ErrorMessage = string.IsNullOrWhiteSpace(model.ErrorMessage) ? ex.Message : model.ErrorMessage;
            logs.Add($"Erro a carregar utilizadores do diretório: {ex.Message}");
        }

        return model;
    }

    private async Task<ManageUserZinfsViewModel> BuildManageZinfsViewModelAsync(string userPrincipalName, CancellationToken cancellationToken)
    {
        var normalizedUpn = UserPrincipalNameNormalizer.Normalize(userPrincipalName);
        var selectedZinfIds = await _dbContext.UserZinfs
            .AsNoTracking()
            .Where(x => x.UserPrincipalName == normalizedUpn)
            .Select(x => x.ZinfId)
            .ToListAsync(cancellationToken);

        var selectedSet = selectedZinfIds.ToHashSet();

        var zinfItems = await _dbContext.Zinfs
            .AsNoTracking()
            .OrderBy(x => x.Nome)
            .Select(x => new ManageUserZinfItemViewModel
            {
                ZinfId = x.Id,
                Nome = x.Nome,
                Selected = selectedSet.Contains(x.Id)
            })
            .ToListAsync(cancellationToken);

        return new ManageUserZinfsViewModel
        {
            UserPrincipalName = normalizedUpn,
            UserDisplayName = normalizedUpn,
            Zinfs = zinfItems
        };
    }
}
