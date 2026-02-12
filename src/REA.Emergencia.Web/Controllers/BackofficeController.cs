using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace REA.Emergencia.Web.Controllers;

[Authorize(Policy = "BackofficeAdminOnly")]
[Route("backoffice")]
public sealed class BackofficeController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }
}
