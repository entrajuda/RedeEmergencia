using Microsoft.AspNetCore.Mvc;
using REA.Emergencia.Data;
using REA.Emergencia.Domain;
using REA.Emergencia.Web.Models;

namespace REA.Emergencia.Web.Controllers;

public sealed class PedidosBensController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public PedidosBensController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    [Route("apoio_bens")]
    public IActionResult ApoioBens()
    {
        return View(new PedidoBemInputModel());
    }

    [HttpPost("apoio_bens")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApoioBens(PedidoBemInputModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = new PedidoBem
        {
            FullName = model.FullName.Trim(),
            PhoneNumber = model.PhoneNumber.Trim(),
            Email = model.Email.Trim(),
            Address = model.Address.Trim(),
            PostalCode = model.PostalCode.Trim(),
            Localidade = model.Localidade.Trim(),
            Freguesia = model.Freguesia.Trim(),
            Concelho = model.Concelho.Trim(),
            IdentificationNumber = model.IdentificationNumber.Trim(),
            Age = model.Age,
            HouseholdSize = model.HouseholdSize,
            ChildrenUnder12 = model.ChildrenUnder12,
            Youth13To17 = model.Youth13To17,
            Adults18Plus = model.Adults18Plus,
            Seniors65Plus = model.Seniors65Plus,
            ReceivesFoodSupport = model.ReceivesFoodSupport ?? false,
            FoodSupportInstitutionName = string.IsNullOrWhiteSpace(model.FoodSupportInstitutionName)
                ? null
                : model.FoodSupportInstitutionName.Trim(),
            CanPickUpNearby = model.CanPickUpNearby ?? false,
            Suggestions = string.IsNullOrWhiteSpace(model.Suggestions) ? null : model.Suggestions.Trim(),
        };

        _dbContext.PedidosBens.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Success));
    }

    [HttpGet]
    public IActionResult Success()
    {
        return View();
    }
}
