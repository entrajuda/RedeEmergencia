using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace REA.Emergencia.Web.Models;

public sealed class BackofficeUsersIndexViewModel
{
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<string> OperationLogs { get; set; } = Array.Empty<string>();
    public IReadOnlyList<BackofficeUserRoleItemViewModel> Users { get; set; } = Array.Empty<BackofficeUserRoleItemViewModel>();
    public IReadOnlyList<SelectListItem> DirectoryUserOptions { get; set; } = Array.Empty<SelectListItem>();
    public AssignAppRolesInputModel AssignInput { get; set; } = new();
}

public sealed class BackofficeUsersAddViewModel
{
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<string> OperationLogs { get; set; } = Array.Empty<string>();
    public IReadOnlyList<SelectListItem> DirectoryUserOptions { get; set; } = Array.Empty<SelectListItem>();
    public AssignAppRolesInputModel AssignInput { get; set; } = new();
}

public sealed class BackofficeUserRoleItemViewModel
{
    public string UserDisplayName { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsVolunteer { get; set; }
    public int TotalZinfs { get; set; }
    public IReadOnlyList<string> AssignedZinfNames { get; set; } = Array.Empty<string>();
}

public sealed class ManageUserZinfsViewModel
{
    public string UserPrincipalName { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<ManageUserZinfItemViewModel> Zinfs { get; set; } = Array.Empty<ManageUserZinfItemViewModel>();
}

public sealed class ManageUserZinfItemViewModel
{
    public int ZinfId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Selected { get; set; }
}

public sealed class AssignAppRolesInputModel
{
    [Required(ErrorMessage = "Introduza o email ou UPN do utilizador.")]
    public string UserPrincipalName { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }
    public bool IsVolunteer { get; set; }
}
