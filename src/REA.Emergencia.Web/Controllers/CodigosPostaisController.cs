using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using REA.Emergencia.Data;
using REA.Emergencia.Web.Models;

namespace REA.Emergencia.Web.Controllers;

[Authorize(Policy = "BackofficeAdminOnly")]
[Route("backoffice/codigos-postais")]
public sealed class CodigosPostaisController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public CodigosPostaisController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int? distritoId, string? concelho, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;
        var concelhoFilter = (concelho ?? string.Empty).Trim();

        var distritoOptions = await _dbContext.Distritos
            .AsNoTracking()
            .OrderBy(x => x.Nome)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Nome
            })
            .ToListAsync(cancellationToken);

        var query = _dbContext.CodigosPostais
            .AsNoTracking()
            .Include(x => x.Concelho)
            .ThenInclude(x => x.Distrito)
            .AsQueryable();

        if (distritoId.HasValue)
        {
            query = query.Where(x => x.Concelho.DistritoId == distritoId.Value);
        }

        if (!string.IsNullOrWhiteSpace(concelhoFilter))
        {
            query = query.Where(x => EF.Functions.Like(x.Concelho.Nome, $"%{concelhoFilter}%"));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems <= 0 ? 1 : (int)Math.Ceiling((double)totalItems / pageSize);
        if (page > totalPages)
        {
            page = totalPages;
        }

        var items = await query
            .OrderBy(x => x.Numero)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CodigoPostalListItemViewModel
            {
                Numero = x.Numero,
                Freguesia = x.Freguesia,
                Concelho = x.Concelho.Nome,
                Distrito = x.Concelho.Distrito.Nome
            })
            .ToListAsync(cancellationToken);

        var model = new CodigosPostaisIndexViewModel
        {
            DistritoId = distritoId,
            Concelho = concelhoFilter,
            DistritoOptions = distritoOptions,
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };

        return View(model);
    }
}
