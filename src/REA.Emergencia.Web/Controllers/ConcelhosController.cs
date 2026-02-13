using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using REA.Emergencia.Data;
using REA.Emergencia.Domain;
using REA.Emergencia.Web.Models;

namespace REA.Emergencia.Web.Controllers;

[Authorize(Policy = "BackofficeAdminOnly")]
[Route("backoffice/concelhos")]
public sealed class ConcelhosController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public ConcelhosController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Concelhos
            .AsNoTracking()
            .Include(x => x.Distrito)
            .OrderBy(x => x.Nome)
            .ToListAsync(cancellationToken);

        return View(items);
    }

    [HttpGet("novo")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await LoadDistritosAsync(cancellationToken);
        return View(new ConcelhoFormModel());
    }

    [HttpPost("novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ConcelhoFormModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadDistritosAsync(cancellationToken);
            return View(model);
        }

        var entity = new Concelho
        {
            Nome = model.Concelho.Trim(),
            DistritoId = model.DistritoId,
            ZINF = model.ZINF.Trim()
        };

        _dbContext.Concelhos.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/editar")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Concelhos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var model = new ConcelhoFormModel
        {
            Concelho = entity.Nome,
            DistritoId = entity.DistritoId,
            ZINF = entity.ZINF
        };

        await LoadDistritosAsync(cancellationToken);
        return View(model);
    }

    [HttpPost("{id:int}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ConcelhoFormModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadDistritosAsync(cancellationToken);
            return View(model);
        }

        var entity = await _dbContext.Concelhos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Nome = model.Concelho.Trim();
        entity.DistritoId = model.DistritoId;
        entity.ZINF = model.ZINF.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/eliminar")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Concelhos
            .AsNoTracking()
            .Include(x => x.Distrito)
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
        var entity = await _dbContext.Concelhos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        _dbContext.Concelhos.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadDistritosAsync(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Distritos
            .AsNoTracking()
            .OrderBy(x => x.Nome)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Nome
            })
            .ToListAsync(cancellationToken);

        ViewBag.Distritos = items;
    }
}
