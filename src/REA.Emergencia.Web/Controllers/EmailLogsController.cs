using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REA.Emergencia.Data;
using REA.Emergencia.Web.Models;

namespace REA.Emergencia.Web.Controllers;

[Authorize(Policy = "BackofficeAdminOnly")]
[Route("backoffice/email-logs")]
public sealed class EmailLogsController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public EmailLogsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var logs = await _dbContext.EmailLogs
            .AsNoTracking()
            .OrderByDescending(x => x.SentAtUtc)
            .Take(1000)
            .Select(x => new EmailLogItemViewModel
            {
                SentAtUtc = x.SentAtUtc,
                Recipients = x.Recipients,
                Subject = x.Subject
            })
            .ToListAsync(cancellationToken);

        var model = new EmailLogsIndexViewModel
        {
            Logs = logs
        };

        return View(model);
    }

    [HttpPost("clear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        await _dbContext.EmailLogs.ExecuteDeleteAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}
