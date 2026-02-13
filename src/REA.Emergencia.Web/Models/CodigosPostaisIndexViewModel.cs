using Microsoft.AspNetCore.Mvc.Rendering;

namespace REA.Emergencia.Web.Models;

public sealed class CodigosPostaisIndexViewModel
{
    public int? DistritoId { get; set; }
    public string Concelho { get; set; } = string.Empty;
    public IReadOnlyList<SelectListItem> DistritoOptions { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<CodigoPostalListItemViewModel> Items { get; set; } = Array.Empty<CodigoPostalListItemViewModel>();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalItems { get; set; }

    public int TotalPages => TotalItems <= 0 ? 1 : (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    public int StartItem => TotalItems == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int EndItem => Math.Min(Page * PageSize, TotalItems);
}

public sealed class CodigoPostalListItemViewModel
{
    public int Numero { get; set; }
    public string Freguesia { get; set; } = string.Empty;
    public string Concelho { get; set; } = string.Empty;
    public string Distrito { get; set; } = string.Empty;
}
