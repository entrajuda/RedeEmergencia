using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace REA.Emergencia.Web.Areas.Encaminhamento.Controllers;

[Area("Encaminhamento")]
[Authorize(Roles = "Volunteer")]
[Route("encaminhamento")]
public sealed class EncaminhamentoController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }
}
