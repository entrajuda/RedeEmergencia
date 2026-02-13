using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REA.Emergencia.Data;
using REA.Emergencia.Domain;
using REA.Emergencia.Web.Models;
using System.Text.Json;

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

        var postalCodeNumber = NormalizePostalCodeToNumber(model.PostalCode);
        if (!postalCodeNumber.HasValue)
        {
            ModelState.AddModelError(nameof(model.PostalCode), "Introduza um código postal válido no formato 0000-000.");
            return View(model);
        }

        var codigoPostal = await _dbContext.CodigosPostais
            .AsNoTracking()
            .Include(x => x.Concelho)
            .FirstOrDefaultAsync(x => x.Numero == postalCodeNumber.Value, cancellationToken);

        if (codigoPostal is null)
        {
            ModelState.AddModelError(nameof(model.PostalCode), "O código postal não foi encontrado na base de dados.");
            return View(model);
        }

        model.Freguesia = codigoPostal.Freguesia;
        model.Concelho = codigoPostal.Concelho.Nome;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var tipoPedido = await _dbContext.TiposPedido
            .FirstOrDefaultAsync(x => x.TableName == "PedidosBens", cancellationToken);

        if (tipoPedido is null)
        {
            ModelState.AddModelError(string.Empty, "Configuracao em falta: TipoPedido para 'PedidosBens' nao encontrado.");
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
            NeededProductTypes = string.Join("; ", model.NeededProductTypes.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x))),
            OtherNeededProductTypesDetails = string.IsNullOrWhiteSpace(model.OtherNeededProductTypesDetails)
                ? null
                : model.OtherNeededProductTypesDetails.Trim(),
            Suggestions = string.IsNullOrWhiteSpace(model.Suggestions) ? null : model.Suggestions.Trim(),
        };

        _dbContext.PedidosBens.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var pedido = new Pedido
        {
            ExternalRequestID = entity.Id,
            TipoPedidoId = tipoPedido.Id,
            State = ExtractInitialState(tipoPedido.Workflow) ?? "NOVO",
        };

        _dbContext.Pedidos.Add(pedido);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return RedirectToAction(nameof(Success));
    }

    [HttpGet]
    public IActionResult Success()
    {
        return View();
    }

    [HttpGet("apoio_bens/codigo-postal")]
    public async Task<IActionResult> LookupCodigoPostal([FromQuery] string postalCode, CancellationToken cancellationToken)
    {
        var postalCodeNumber = NormalizePostalCodeToNumber(postalCode);
        if (!postalCodeNumber.HasValue)
        {
            return BadRequest(new { found = false, message = "Código postal inválido." });
        }

        var codigoPostal = await _dbContext.CodigosPostais
            .AsNoTracking()
            .Include(x => x.Concelho)
            .FirstOrDefaultAsync(x => x.Numero == postalCodeNumber.Value, cancellationToken);

        if (codigoPostal is null)
        {
            return NotFound(new { found = false, message = "Código postal não encontrado." });
        }

        return Ok(new
        {
            found = true,
            freguesia = codigoPostal.Freguesia,
            concelho = codigoPostal.Concelho.Nome
        });
    }

    private static string? ExtractInitialState(string workflowJson)
    {
        if (string.IsNullOrWhiteSpace(workflowJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(workflowJson);
            if (doc.RootElement.TryGetProperty("initialState", out var initialStateElement))
            {
                var initialState = initialStateElement.GetString();
                if (!string.IsNullOrWhiteSpace(initialState))
                {
                    return initialState.Trim();
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static int? NormalizePostalCodeToNumber(string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return null;
        }

        var digitsOnly = postalCode
            .Trim()
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        if (digitsOnly.Length != 7 || !int.TryParse(digitsOnly, out var number))
        {
            return null;
        }

        return number;
    }
}
