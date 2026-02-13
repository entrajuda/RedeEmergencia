using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REA.Emergencia.Data;
using REA.Emergencia.Domain;
using REA.Emergencia.Web.Models;

namespace REA.Emergencia.Web.Controllers;

[Authorize(Policy = "BackofficeAdminOnly")]
[Route("backoffice/distritos")]
public sealed class DistritosController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public DistritosController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Distritos
            .AsNoTracking()
            .OrderBy(x => x.Nome)
            .ToListAsync(cancellationToken);

        return View(items);
    }

    [HttpGet("novo")]
    public IActionResult Create()
    {
        return View(new DistritoFormModel());
    }

    [HttpPost("novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DistritoFormModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = new Distrito
        {
            Nome = model.Distrito.Trim()
        };

        _dbContext.Distritos.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/editar")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Distritos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        return View(new DistritoFormModel { Distrito = entity.Nome });
    }

    [HttpPost("{id:int}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DistritoFormModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = await _dbContext.Distritos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Nome = model.Distrito.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/eliminar")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Distritos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        var hasConcelhos = await _dbContext.Concelhos
            .AsNoTracking()
            .AnyAsync(x => x.DistritoId == id, cancellationToken);

        ViewBag.CanDelete = !hasConcelhos;
        if (hasConcelhos)
        {
            ViewBag.DeleteBlockMessage = "Não é possível eliminar este distrito porque existem concelhos associados.";
        }

        return View(entity);
    }

    [HttpPost("{id:int}/eliminar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Distritos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var hasConcelhos = await _dbContext.Concelhos
            .AsNoTracking()
            .AnyAsync(x => x.DistritoId == id, cancellationToken);

        if (hasConcelhos)
        {
            ModelState.AddModelError(string.Empty, "Não é possível eliminar este distrito porque existem concelhos associados.");
            ViewBag.CanDelete = false;
            ViewBag.DeleteBlockMessage = "Não é possível eliminar este distrito porque existem concelhos associados.";
            return View("Delete", entity);
        }

        _dbContext.Distritos.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}
