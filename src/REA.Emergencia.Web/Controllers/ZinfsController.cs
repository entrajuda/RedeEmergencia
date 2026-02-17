using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using REA.Emergencia.Data;
using REA.Emergencia.Web.Helpers;
using REA.Emergencia.Web.Models;

namespace REA.Emergencia.Web.Controllers;

[Authorize(Policy = "BackofficeAdminOnly")]
[Route("backoffice/zinfs")]
public sealed class ZinfsController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public ZinfsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userAssignments = await _dbContext.UserZinfs
            .AsNoTracking()
            .Select(x => new
            {
                x.ZinfId,
                x.UserPrincipalName
            })
            .ToListAsync(cancellationToken);

        var usersByZinfId = userAssignments
            .GroupBy(x => x.ZinfId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .Select(x => UserPrincipalNameNormalizer.Normalize(x.UserPrincipalName))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList());

        var zinfRows = await _dbContext.Zinfs
            .AsNoTracking()
            .OrderBy(x => x.Nome)
            .ToListAsync(cancellationToken);

        var zinfs = zinfRows
            .Select(x => new ZinfListItemViewModel
            {
                Id = x.Id,
                Nome = x.Nome,
                AssignedUsers = usersByZinfId.TryGetValue(x.Id, out var users)
                    ? users
                    : Array.Empty<string>()
            })
            .ToList();

        var model = new ZinfsIndexViewModel
        {
            Zinfs = zinfs
        };

        return View(model);
    }

    [HttpGet("{id:int}/instituicoes")]
    public async Task<IActionResult> Instituicoes(
        int id,
        string? codigoEA,
        string? nome,
        string? distrito,
        string? concelho,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 25 : pageSize;
        var codigoEaFilter = (codigoEA ?? string.Empty).Trim();
        var nomeFilter = (nome ?? string.Empty).Trim();
        var distritoFilter = (distrito ?? string.Empty).Trim();
        var concelhoFilter = (concelho ?? string.Empty).Trim();

        var zinf = await _dbContext.Zinfs
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Nome })
            .FirstOrDefaultAsync(cancellationToken);

        if (zinf is null)
        {
            return NotFound();
        }

        var assignedUsers = await _dbContext.UserZinfs
            .AsNoTracking()
            .Where(x => x.ZinfId == id)
            .Select(x => x.UserPrincipalName)
            .ToListAsync(cancellationToken);

        var distritoOptions = await _dbContext.Instituicoes
            .AsNoTracking()
            .Where(x => x.ZinfId == id && x.Distrito != null)
            .Select(x => x.Distrito!.Nome)
            .Distinct()
            .OrderBy(x => x)
            .Select(x => new SelectListItem
            {
                Value = x,
                Text = x
            })
            .ToListAsync(cancellationToken);

        var instituicoesQuery = _dbContext.Instituicoes
            .AsNoTracking()
            .Where(x => x.ZinfId == id)
            .Include(x => x.Distrito)
            .Include(x => x.Concelho)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(codigoEaFilter))
        {
            instituicoesQuery = instituicoesQuery.Where(x => EF.Functions.Like(x.CodigoEA, $"%{codigoEaFilter}%"));
        }

        if (!string.IsNullOrWhiteSpace(nomeFilter))
        {
            instituicoesQuery = instituicoesQuery.Where(x => EF.Functions.Like(x.Nome, $"%{nomeFilter}%"));
        }

        if (!string.IsNullOrWhiteSpace(distritoFilter))
        {
            instituicoesQuery = instituicoesQuery.Where(x => x.Distrito != null && EF.Functions.Like(x.Distrito.Nome, $"%{distritoFilter}%"));
        }

        if (!string.IsNullOrWhiteSpace(concelhoFilter))
        {
            instituicoesQuery = instituicoesQuery.Where(x => x.Concelho != null && EF.Functions.Like(x.Concelho.Nome, $"%{concelhoFilter}%"));
        }

        var totalItems = await instituicoesQuery.CountAsync(cancellationToken);
        var totalPages = totalItems <= 0 ? 1 : (int)Math.Ceiling((double)totalItems / pageSize);
        if (page > totalPages)
        {
            page = totalPages;
        }

        var instituicoes = await instituicoesQuery
            .OrderBy(x => x.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InstituicaoListItemViewModel
            {
                CodigoEA = x.CodigoEA,
                Nome = x.Nome,
                Distrito = x.Distrito != null ? x.Distrito.Nome : null,
                Concelho = x.Concelho != null ? x.Concelho.Nome : null,
                Zinf = x.Zinf != null ? x.Zinf.Nome : null,
                PessoaContacto = x.PessoaContacto,
                Email1 = x.Email1
            })
            .ToListAsync(cancellationToken);

        var model = new ZinfInstituicoesViewModel
        {
            ZinfId = zinf.Id,
            ZinfNome = zinf.Nome,
            CodigoEA = codigoEaFilter,
            Nome = nomeFilter,
            Distrito = distritoFilter,
            Concelho = concelhoFilter,
            DistritoOptions = distritoOptions,
            AssignedUsers = assignedUsers
                .Select(UserPrincipalNameNormalizer.Normalize)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Instituicoes = instituicoes,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };

        return View(model);
    }
}
