using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REA.Emergencia.Data;
using REA.Emergencia.Domain;
using REA.Emergencia.Web.Models;

namespace REA.Emergencia.Web.Controllers;

[Authorize(Policy = "BackofficeAdminOnly")]
[Route("backoffice/tipos-pedido")]
public sealed class TiposPedidoController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public TiposPedidoController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _dbContext.TiposPedido
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return View(items);
    }

    [HttpGet("novo")]
    public IActionResult Create()
    {
        return View(new TipoPedidoFormModel());
    }

    [HttpPost("novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TipoPedidoFormModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = new TipoPedido
        {
            Name = model.Name.Trim(),
            Workflow = model.Workflow.Trim(),
            TableName = model.TableName.Trim(),
        };

        _dbContext.TiposPedido.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/detalhe")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.TiposPedido
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        return View(entity);
    }

    [HttpGet("{id:int}/editar")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.TiposPedido
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        var model = new TipoPedidoFormModel
        {
            Name = entity.Name,
            Workflow = entity.Workflow,
            TableName = entity.TableName,
        };

        return View(model);
    }

    [HttpPost("{id:int}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TipoPedidoFormModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = await _dbContext.TiposPedido
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        entity.Name = model.Name.Trim();
        entity.Workflow = model.Workflow.Trim();
        entity.TableName = model.TableName.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/eliminar")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.TiposPedido
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        return View(entity);
    }

    [HttpPost("{id:int}/eliminar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.TiposPedido
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        _dbContext.TiposPedido.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }
}
