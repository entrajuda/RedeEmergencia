using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REA.Emergencia.Data;
using REA.Emergencia.Domain;
using REA.Emergencia.Web.Helpers;
using REA.Emergencia.Web.Models;
using REA.Emergencia.Web.Services;
using System.Security.Claims;
using System.Text.Json;

namespace REA.Emergencia.Web.Controllers;

public sealed class PedidosBensController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IAzureAdRoleManagementService _azureAdRoleManagementService;
    private readonly IRequestNotificationEmailService _requestNotificationEmailService;
    private readonly ILogger<PedidosBensController> _logger;

    public PedidosBensController(
        ApplicationDbContext dbContext,
        IAppSettingsService appSettingsService,
        IAzureAdRoleManagementService azureAdRoleManagementService,
        IRequestNotificationEmailService requestNotificationEmailService,
        ILogger<PedidosBensController> logger)
    {
        _dbContext = dbContext;
        _appSettingsService = appSettingsService;
        _azureAdRoleManagementService = azureAdRoleManagementService;
        _requestNotificationEmailService = requestNotificationEmailService;
        _logger = logger;
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
            ZinfId = codigoPostal.Concelho.ZinfId
        };

        _dbContext.Pedidos.Add(pedido);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.PedidoEstadoLogs.Add(new PedidoEstadoLog
        {
            PedidoId = pedido.Id,
            FromState = "SEM_ESTADO",
            ToState = pedido.State,
            ChangedBy = ResolveChangedBy(model.Email)
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        await TrySendSubmissionEmailAsync(model.Email, pedido.PublicId, cancellationToken);
        await TrySendNovoPedidoEmailToZinfUsersAsync(pedido.Id, pedido.PublicId, pedido.ZinfId, cancellationToken);

        return RedirectToAction(nameof(Success), new { publicId = pedido.PublicId });
    }

    [HttpGet]
    public IActionResult Success(Guid? publicId)
    {
        ViewData["PublicId"] = publicId;
        return View();
    }

    [HttpGet("pedido/{publicId:guid}")]
    public async Task<IActionResult> EstadoPedido(Guid publicId, CancellationToken cancellationToken)
    {
        var pedido = await _dbContext.Pedidos
            .AsNoTracking()
            .Include(x => x.TipoPedido)
            .FirstOrDefaultAsync(x => x.PublicId == publicId, cancellationToken);

        if (pedido is null)
        {
            return NotFound();
        }

        var model = new PedidoStatusViewModel
        {
            PublicId = pedido.PublicId,
            State = pedido.State,
            CreatedAtUtc = pedido.CreatedAtUtc,
            TipoPedido = pedido.TipoPedido.Name
        };

        return View(model);
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

    private async Task TrySendSubmissionEmailAsync(string recipientEmail, Guid pedidoPublicId, CancellationToken cancellationToken)
    {
        var sendEmailToPedidoCreatorRaw = await _appSettingsService.GetValueAsync(AppSettingKeys.SendEmailToPedidoCreator, cancellationToken);
        var sendEmailToPedidoCreator = !string.IsNullOrWhiteSpace(sendEmailToPedidoCreatorRaw)
            ? string.Equals(sendEmailToPedidoCreatorRaw, "true", StringComparison.OrdinalIgnoreCase)
            : true;

        if (!sendEmailToPedidoCreator)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return;
        }

        var template = await _appSettingsService.GetValueAsync(AppSettingKeys.PedidoBensEmailTemplate, cancellationToken)
                       ?? await _appSettingsService.GetValueAsync("SubmissionEmailTemplate", cancellationToken);
        if (string.IsNullOrWhiteSpace(template))
        {
            return;
        }

        try
        {
            await _requestNotificationEmailService.SendRequestSubmittedEmailAsync(
                recipientEmail.Trim(),
                pedidoPublicId,
                template,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar email de confirmação para o pedido {PedidoPublicId}.", pedidoPublicId);
        }
    }

    private async Task TrySendNovoPedidoEmailToZinfUsersAsync(int pedidoId, Guid pedidoPublicId, int? zinfId, CancellationToken cancellationToken)
    {
        if (!zinfId.HasValue)
        {
            return;
        }

        var sendNovoPedidoEmailRaw = await _appSettingsService.GetValueAsync(AppSettingKeys.SendNovoPedidoEmailToZinfUsers, cancellationToken);
        var sendNovoPedidoEmail = !string.IsNullOrWhiteSpace(sendNovoPedidoEmailRaw)
            ? string.Equals(sendNovoPedidoEmailRaw, "true", StringComparison.OrdinalIgnoreCase)
            : true;

        if (!sendNovoPedidoEmail)
        {
            return;
        }

        var template = await _appSettingsService.GetValueAsync(AppSettingKeys.NovoPedidolTemplate, cancellationToken);
        if (string.IsNullOrWhiteSpace(template))
        {
            return;
        }

        var userPrincipalNames = await _dbContext.UserZinfs
            .AsNoTracking()
            .Where(x => x.ZinfId == zinfId.Value)
            .Select(x => x.UserPrincipalName)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (userPrincipalNames.Count == 0)
        {
            return;
        }

        var pedidoStatusUrl = Url.Action(
            action: nameof(EstadoPedido),
            controller: "PedidosBens",
            values: new { publicId = pedidoPublicId },
            protocol: Request.Scheme);

        var templateBody = template.Replace("{GuidPedido}", pedidoPublicId.ToString(), StringComparison.OrdinalIgnoreCase);
        var body = string.IsNullOrWhiteSpace(pedidoStatusUrl)
            ? templateBody
            : $"{templateBody}<br><br><a href=\"{pedidoStatusUrl}\">Consultar pedido</a>";

        foreach (var upn in userPrincipalNames)
        {
            string? recipientEmail;
            try
            {
                recipientEmail = await _azureAdRoleManagementService.ResolveUserEmailAsync(upn, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Erro ao resolver email no Active Directory para utilizador {UserPrincipalName}. PedidoId={PedidoId}, ZinfId={ZinfId}",
                    upn,
                    pedidoId,
                    zinfId.Value);
                continue;
            }

            if (string.IsNullOrWhiteSpace(recipientEmail) || !recipientEmail.Contains('@'))
            {
                _logger.LogError(
                    "A ignorar envio de novo pedido para utilizador sem email válido: {UserPrincipalName}. PedidoId={PedidoId}, ZinfId={ZinfId}",
                    upn,
                    pedidoId,
                    zinfId.Value);
                continue;
            }

            try
            {
                await _requestNotificationEmailService.SendEmailAsync(
                    recipientEmail,
                    $"Novo pedido recebido ({pedidoPublicId})",
                    body,
                    isHtml: true,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Falha ao enviar email de novo pedido para {RecipientEmail}. PedidoId={PedidoId}, ZinfId={ZinfId}",
                    recipientEmail,
                    pedidoId,
                    zinfId.Value);
            }
        }
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

    private string ResolveChangedBy(string? fallbackEmail)
    {
        return
            User.FindFirstValue("preferred_username") ??
            User.FindFirstValue("upn") ??
            User.FindFirstValue(ClaimTypes.Upn) ??
            User.FindFirstValue(ClaimTypes.Email) ??
            User.Identity?.Name ??
            fallbackEmail ??
            "Sistema";
    }
}
