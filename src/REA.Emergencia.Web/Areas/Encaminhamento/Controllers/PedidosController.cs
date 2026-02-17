using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using REA.Emergencia.Data;
using REA.Emergencia.Domain;
using REA.Emergencia.Web.Helpers;
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
        var isAdmin = User.IsInRole("Admin");
        var userZinfIds = isAdmin ? [] : await GetCurrentUserZinfIdsAsync(cancellationToken);
        var accessibleZinfs = isAdmin
            ? await _dbContext.Zinfs
                .AsNoTracking()
                .OrderBy(x => x.Nome)
                .Select(x => x.Nome)
                .ToListAsync(cancellationToken)
            : await _dbContext.Zinfs
                .AsNoTracking()
                .Where(x => userZinfIds.Contains(x.Id))
                .OrderBy(x => x.Nome)
                .Select(x => x.Nome)
                .ToListAsync(cancellationToken);

        var tipoPedidoOptions = await _dbContext.TiposPedido
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name,
            })
            .ToListAsync(cancellationToken);

        var pedidosRawQuery =
            from p in _dbContext.Pedidos.AsNoTracking()
            join tp in _dbContext.TiposPedido.AsNoTracking() on p.TipoPedidoId equals tp.Id
            join z in _dbContext.Zinfs.AsNoTracking() on p.ZinfId equals z.Id into zJoin
            from z in zJoin.DefaultIfEmpty()
            where tp.TableName == "PedidosBens"
            select new
            {
                p.Id,
                p.CreatedAtUtc,
                p.State,
                p.ExternalRequestID,
                p.TipoPedidoId,
                TipoPedidoName = tp.Name,
                p.ZinfId,
                ZinfName = z != null ? z.Nome : "-"
            };

        if (tipoPedidoId.HasValue)
        {
            pedidosRawQuery = pedidosRawQuery.Where(x => x.TipoPedidoId == tipoPedidoId.Value);
        }

        var pedidosRaw = await pedidosRawQuery
            .Where(x => isAdmin || (x.ZinfId.HasValue && userZinfIds.Contains(x.ZinfId.Value)))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var pedidos = pedidosRaw
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .Select(x => new PedidoListItemViewModel
            {
                Id = x.Id,
                CreatedAtUtc = x.CreatedAtUtc,
                State = x.State,
                ExternalRequestID = x.ExternalRequestID,
                TipoPedidoId = x.TipoPedidoId,
                TipoPedidoName = x.TipoPedidoName,
                ZinfName = x.ZinfName
            })
            .ToList();

        var model = new PedidosIndexViewModel
        {
            TipoPedidoId = tipoPedidoId,
            TipoPedidoOptions = tipoPedidoOptions,
            Pedidos = pedidos,
            IsAdmin = isAdmin,
            AccessibleZinfs = accessibleZinfs
        };

        return View(model);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole("Admin");
        var userZinfIds = isAdmin ? [] : await GetCurrentUserZinfIdsAsync(cancellationToken);

        var pedido = await _dbContext.Pedidos
            .AsNoTracking()
            .Include(x => x.TipoPedido)
            .Include(x => x.Zinf)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (pedido is null)
        {
            return NotFound();
        }

        if (!string.Equals(pedido.TipoPedido.TableName, "PedidosBens", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var pedidoZinfId = pedido.ZinfId;
        if (!isAdmin && (!pedidoZinfId.HasValue || !userZinfIds.Contains(pedidoZinfId.Value)))
        {
            TempData["ErrorMessage"] = "Não tem acesso a este pedido (ZINF não autorizada).";
            return RedirectToAction(nameof(Index));
        }

        var instituicoesMesmoZinf = pedidoZinfId.HasValue
            ? await _dbContext.Instituicoes
                .AsNoTracking()
                .Where(x => x.ZinfId == pedidoZinfId.Value)
                .OrderBy(x => x.Nome)
                .Select(x => new PedidoInstituicaoListItemViewModel
                {
                    CodigoEA = x.CodigoEA,
                    Nome = x.Nome,
                    PessoaContacto = x.PessoaContacto,
                    Email1 = x.Email1
                })
                .ToListAsync(cancellationToken)
            : [];

        var estadoLogs = await _dbContext.PedidoEstadoLogs
            .AsNoTracking()
            .Where(x => x.PedidoId == pedido.Id)
            .OrderByDescending(x => x.ChangedAtUtc)
            .Select(x => new PedidoEstadoLogItemViewModel
            {
                ChangedAtUtc = x.ChangedAtUtc,
                FromState = x.FromState,
                ToState = x.ToState,
                ChangedBy = x.ChangedBy
            })
            .ToListAsync(cancellationToken);

        var model = new PedidoDetailsViewModel
        {
            Id = pedido.Id,
            CreatedAtUtc = pedido.CreatedAtUtc,
            State = pedido.State,
            TipoPedidoName = pedido.TipoPedido.Name,
            TipoPedidoTableName = pedido.TipoPedido.TableName,
            ZinfName = pedido.Zinf != null ? pedido.Zinf.Nome : "-",
            ExternalRequestID = pedido.ExternalRequestID,
            EstadoLogs = estadoLogs,
            InstituicoesMesmoZinf = instituicoesMesmoZinf
        };

        var pedidoBem = await _dbContext.PedidosBens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == pedido.ExternalRequestID, cancellationToken);

        if (pedidoBem is null)
        {
            return NotFound();
        }

        model.IsSupportedType = true;
        model.Fields = BuildPedidoBemFields(pedidoBem);

        return View(model);
    }

    private async Task<HashSet<int>> GetCurrentUserZinfIdsAsync(CancellationToken cancellationToken)
    {
        var userPrincipalName =
            User.FindFirstValue("preferred_username") ??
            User.FindFirstValue("upn") ??
            User.FindFirstValue(ClaimTypes.Upn) ??
            User.FindFirstValue(ClaimTypes.Email) ??
            User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            return [];
        }

        var candidates = UserPrincipalNameNormalizer.BuildCandidates(userPrincipalName);
        if (candidates.Count == 0)
        {
            return [];
        }

        var zinfIds = await _dbContext.UserZinfs
            .AsNoTracking()
            .Where(x => candidates.Contains(x.UserPrincipalName))
            .Select(x => x.ZinfId)
            .ToListAsync(cancellationToken);

        return zinfIds.ToHashSet();
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
            new() { Label = "Sugestões", Value = pedidoBem.Suggestions ?? "-" }
        ];
    }
}
