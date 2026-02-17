using Microsoft.AspNetCore.Mvc.Rendering;

namespace REA.Emergencia.Web.Models;

public sealed class ZinfsIndexViewModel
{
    public IReadOnlyList<ZinfListItemViewModel> Zinfs { get; set; } = Array.Empty<ZinfListItemViewModel>();
}

public sealed class ZinfListItemViewModel
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public IReadOnlyList<string> AssignedUsers { get; set; } = Array.Empty<string>();
}

public sealed class ZinfInstituicoesViewModel
{
    public int ZinfId { get; set; }
    public string ZinfNome { get; set; } = string.Empty;
    public IReadOnlyList<string> AssignedUsers { get; set; } = Array.Empty<string>();
    public string CodigoEA { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Distrito { get; set; } = string.Empty;
    public string Concelho { get; set; } = string.Empty;
    public IReadOnlyList<SelectListItem> DistritoOptions { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<InstituicaoListItemViewModel> Instituicoes { get; set; } = Array.Empty<InstituicaoListItemViewModel>();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalItems { get; set; }

    public int TotalPages => TotalItems <= 0 ? 1 : (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    public int StartItem => TotalItems == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int EndItem => Math.Min(Page * PageSize, TotalItems);
}
