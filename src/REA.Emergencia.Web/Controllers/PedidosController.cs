using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using REA.Emergencia.Data;
using REA.Emergencia.Web.Models;

namespace REA.Emergencia.Web.Controllers;

[Authorize(Policy = "BackofficeAdminOnly")]
[Route("backoffice/pedidos")]
public sealed class PedidosController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public PedidosController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int? tipoPedidoId, CancellationToken cancellationToken)
    {
        var tipoPedidoOptions = await _dbContext.TiposPedido
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name,
            })
            .ToListAsync(cancellationToken);

        var query = _dbContext.Pedidos
            .AsNoTracking()
            .Include(x => x.TipoPedido)
            .AsQueryable();

        if (tipoPedidoId.HasValue)
        {
            query = query.Where(x => x.TipoPedidoId == tipoPedidoId.Value);
        }

        var pedidos = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new PedidoListItemViewModel
            {
                Id = x.Id,
                CreatedAtUtc = x.CreatedAtUtc,
                State = x.State,
                ExternalRequestID = x.ExternalRequestID,
                TipoPedidoId = x.TipoPedidoId,
                TipoPedidoName = x.TipoPedido.Name,
            })
            .ToListAsync(cancellationToken);

        var model = new PedidosIndexViewModel
        {
            TipoPedidoId = tipoPedidoId,
            TipoPedidoOptions = tipoPedidoOptions,
            Pedidos = pedidos,
        };

        return View(model);
    }
}
