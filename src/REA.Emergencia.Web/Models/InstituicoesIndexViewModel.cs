using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace REA.Emergencia.Web.Models;

public sealed class InstituicoesIndexViewModel
{
    public IReadOnlyList<InstituicaoListItemViewModel> Instituicoes { get; set; } = Array.Empty<InstituicaoListItemViewModel>();
    public string CodigoEA { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public int? DistritoId { get; set; }
    public string Concelho { get; set; } = string.Empty;
    public int? ZinfId { get; set; }
    public IReadOnlyList<SelectListItem> DistritoOptions { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> ZinfOptions { get; set; } = Array.Empty<SelectListItem>();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalItems { get; set; }
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public InstituicoesImportResultViewModel? ImportResult { get; set; }

    public int TotalPages => TotalItems <= 0 ? 1 : (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    public int StartItem => TotalItems == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int EndItem => Math.Min(Page * PageSize, TotalItems);
}

public sealed class InstituicaoListItemViewModel
{
    public string CodigoEA { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Distrito { get; set; }
    public string? Concelho { get; set; }
    public string? Zinf { get; set; }
    public string? PessoaContacto { get; set; }
    public string? Email1 { get; set; }
}

public sealed class InstituicoesImportResultViewModel
{
    public int ProcessedRows { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
}
