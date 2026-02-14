using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using REA.Emergencia.Data;
using REA.Emergencia.Domain;
using REA.Emergencia.Web.Models;

namespace REA.Emergencia.Web.Areas.Encaminhamento.Controllers;

[Area("Encaminhamento")]
[Authorize(Roles = "Volunteer")]
[Route("encaminhamento/pedidos")]
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

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var pedido = await _dbContext.Pedidos
            .AsNoTracking()
            .Include(x => x.TipoPedido)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (pedido is null)
        {
            return NotFound();
        }

        var model = new PedidoDetailsViewModel
        {
            Id = pedido.Id,
            CreatedAtUtc = pedido.CreatedAtUtc,
            State = pedido.State,
            TipoPedidoName = pedido.TipoPedido.Name,
            TipoPedidoTableName = pedido.TipoPedido.TableName,
            ExternalRequestID = pedido.ExternalRequestID
        };

        if (string.Equals(pedido.TipoPedido.TableName, "PedidosBens", StringComparison.OrdinalIgnoreCase))
        {
            var pedidoBem = await _dbContext.PedidosBens
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == pedido.ExternalRequestID, cancellationToken);

            if (pedidoBem is null)
            {
                return NotFound();
            }

            model.IsSupportedType = true;
            model.Fields = BuildPedidoBemFields(pedidoBem);
        }

        return View(model);
    }

    private static IReadOnlyList<PedidoDetailFieldViewModel> BuildPedidoBemFields(PedidoBem pedidoBem)
    {
        return
        [
            new() { Label = "Nome", Value = pedidoBem.FullName },
            new() { Label = "Telemóvel", Value = pedidoBem.PhoneNumber },
            new() { Label = "Email", Value = pedidoBem.Email },
            new() { Label = "Morada", Value = pedidoBem.Address },
            new() { Label = "Código Postal", Value = pedidoBem.PostalCode },
            new() { Label = "Localidade", Value = pedidoBem.Localidade },
            new() { Label = "Freguesia", Value = pedidoBem.Freguesia },
            new() { Label = "Concelho", Value = pedidoBem.Concelho },
            new() { Label = "Nº Identificação", Value = pedidoBem.IdentificationNumber },
            new() { Label = "Idade", Value = pedidoBem.Age.ToString() },
            new() { Label = "Pessoas no agregado", Value = pedidoBem.HouseholdSize.ToString() },
            new() { Label = "Crianças <12", Value = pedidoBem.ChildrenUnder12.ToString() },
            new() { Label = "Jovens 13-17", Value = pedidoBem.Youth13To17.ToString() },
            new() { Label = "Adultos >=18", Value = pedidoBem.Adults18Plus.ToString() },
            new() { Label = "Pessoas >65", Value = pedidoBem.Seniors65Plus.ToString() },
            new() { Label = "Recebe apoio alimentar", Value = pedidoBem.ReceivesFoodSupport ? "Sim" : "Não" },
            new() { Label = "Instituição apoio alimentar", Value = pedidoBem.FoodSupportInstitutionName ?? "-" },
            new() { Label = "Consegue recolher perto de casa", Value = pedidoBem.CanPickUpNearby ? "Sim" : "Não" },
            new() { Label = "Tipos de produtos", Value = pedidoBem.NeededProductTypes },
            new() { Label = "Outros produtos (detalhe)", Value = pedidoBem.OtherNeededProductTypesDetails ?? "-" },
            new() { Label = "Sugestões", Value = pedidoBem.Suggestions ?? "-" },
            new() { Label = "Data pedido (UTC)", Value = pedidoBem.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss") }
        ];
    }
}
