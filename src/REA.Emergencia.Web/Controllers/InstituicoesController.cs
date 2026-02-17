using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using REA.Emergencia.Data;
using REA.Emergencia.Domain;
using REA.Emergencia.Web.Models;

namespace REA.Emergencia.Web.Controllers;

[Authorize(Policy = "BackofficeAdminOnly")]
[Route("backoffice/instituicoes")]
public sealed class InstituicoesController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public InstituicoesController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? codigoEA,
        string? nome,
        int? distritoId,
        string? concelho,
        int? zinfId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var model = await BuildIndexViewModelAsync(
            codigoEA,
            nome,
            distritoId,
            concelho,
            zinfId,
            page,
            pageSize,
            cancellationToken);
        return View(model);
    }

    [HttpPost("import")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? excelFile, CancellationToken cancellationToken)
    {
        if (excelFile is null || excelFile.Length == 0)
        {
            var emptyFileModel = await BuildIndexViewModelAsync(cancellationToken: cancellationToken);
            emptyFileModel.ErrorMessage = "Selecione um ficheiro Excel para importar.";
            return View(nameof(Index), emptyFileModel);
        }

        var extension = Path.GetExtension(excelFile.FileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var invalidExtModel = await BuildIndexViewModelAsync(cancellationToken: cancellationToken);
            invalidExtModel.ErrorMessage = "Formato inválido. Use um ficheiro .xlsx.";
            return View(nameof(Index), invalidExtModel);
        }

        var result = new InstituicoesImportResultViewModel();
        var errors = new List<string>();

        var concelhosByName = await _dbContext.Concelhos
            .AsNoTracking()
            .Select(x => new { x.Id, x.Nome })
            .ToListAsync(cancellationToken);
        var distritosByName = await _dbContext.Distritos
            .AsNoTracking()
            .Select(x => new { x.Id, x.Nome })
            .ToListAsync(cancellationToken);
        var zinfs = await _dbContext.Zinfs
            .AsNoTracking()
            .Select(x => new ZinfLookup(x.Id, x.Nome))
            .ToListAsync(cancellationToken);
        var codigosPostais = await _dbContext.CodigosPostais
            .AsNoTracking()
            .Select(x => x.Numero)
            .ToListAsync(cancellationToken);
        var codigosPostaisSet = codigosPostais.ToHashSet();

        var existingByCodigoEa = await _dbContext.Instituicoes
            .ToDictionaryAsync(x => x.CodigoEA, StringComparer.OrdinalIgnoreCase, cancellationToken);

        await using var stream = excelFile.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null || worksheet.RangeUsed() is null)
        {
            var invalidWorkbookModel = await BuildIndexViewModelAsync(cancellationToken: cancellationToken);
            invalidWorkbookModel.ErrorMessage = "O ficheiro não contém dados.";
            return View(nameof(Index), invalidWorkbookModel);
        }

        var rows = worksheet.RangeUsed()!.RowsUsed().ToList();
        if (rows.Count < 2)
        {
            var noDataModel = await BuildIndexViewModelAsync(cancellationToken: cancellationToken);
            noDataModel.ErrorMessage = "O ficheiro não contém linhas de dados para importar.";
            return View(nameof(Index), noDataModel);
        }

        var headerRow = FindHeaderRow(rows);
        if (headerRow is null)
        {
            var headerErrorModel = await BuildIndexViewModelAsync(cancellationToken: cancellationToken);
            headerErrorModel.ErrorMessage = "Não foi possível identificar a linha de cabeçalhos no ficheiro.";
            return View(nameof(Index), headerErrorModel);
        }

        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var key = NormalizeHeader(cell.GetString());
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            // Keep first occurrence when duplicate header names exist.
            if (!headers.ContainsKey(key))
            {
                headers[key] = cell.Address.ColumnNumber;
            }
        }

        int GetCol(params string[] names)
        {
            foreach (var name in names)
            {
                var key = NormalizeHeader(name);
                if (headers.TryGetValue(key, out var col))
                {
                    return col;
                }
            }

            return -1;
        }

        var colCodigoEa = GetCol("Código EA", "Codigo EA");
        var colNome = GetCol("Nome");
        var colConcelho = GetCol("Concelho");
        var colDistrito = GetCol("Distrito");
        var colAreaInfluencia = GetCol("Área Influência", "Area Influencia", "Área de Influência");
        var colPessoaContacto = GetCol("Pessoa de Contacto", "Pessoa Contacto");
        var colTelefone = GetCol("Telefone");
        var colTelemovel = GetCol("Telemovel", "Telemóvel");
        var colEmail1 = GetCol("Email 1", "Email1");
        var colCodigoPostal = GetCol("Codigo Postal", "Código Postal", "CP Localidade");
        var colLocalidade = GetCol("Localidade");

        if (colCodigoEa <= 0 || colNome <= 0 || colConcelho <= 0 || colDistrito <= 0 || colAreaInfluencia <= 0 || colCodigoPostal <= 0)
        {
            var headerErrorModel = await BuildIndexViewModelAsync(cancellationToken: cancellationToken);
            headerErrorModel.ErrorMessage = "Cabeçalhos obrigatórios em falta. Verifique: Código EA, Nome, Concelho, Distrito, Área Influência, Codigo Postal.";
            return View(nameof(Index), headerErrorModel);
        }

        var headerRowNumber = headerRow.RowNumber();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.RowNumber() <= headerRowNumber)
            {
                continue;
            }

            var rowNumber = row.RowNumber();

            var codigoEa = ReadCell(row, colCodigoEa);
            var nome = ReadCell(row, colNome);
            var concelhoNome = ReadCell(row, colConcelho);
            var distritoNome = ReadCell(row, colDistrito);
            var areaInfluencia = ReadCell(row, colAreaInfluencia);
            var pessoaContacto = ReadCell(row, colPessoaContacto);
            var telefone = ReadCell(row, colTelefone);
            var telemovel = ReadCell(row, colTelemovel);
            var email1 = ReadCell(row, colEmail1);
            var codigoPostalRaw = ReadCell(row, colCodigoPostal);
            var localidade = ReadCell(row, colLocalidade);

            if (string.IsNullOrWhiteSpace(codigoEa) &&
                string.IsNullOrWhiteSpace(nome) &&
                string.IsNullOrWhiteSpace(concelhoNome))
            {
                continue;
            }

            result.ProcessedRows++;

            if (string.IsNullOrWhiteSpace(codigoEa) || string.IsNullOrWhiteSpace(nome))
            {
                errors.Add($"Linha {rowNumber}: Código EA e Nome são obrigatórios.");
                continue;
            }

            var concelho = concelhosByName.FirstOrDefault(x => string.Equals(x.Nome, concelhoNome, StringComparison.OrdinalIgnoreCase));
            if (concelho is null)
            {
                errors.Add($"Linha {rowNumber}: Concelho '{concelhoNome}' não encontrado.");
                continue;
            }

            var distrito = distritosByName.FirstOrDefault(x => string.Equals(x.Nome, distritoNome, StringComparison.OrdinalIgnoreCase));
            if (distrito is null)
            {
                errors.Add($"Linha {rowNumber}: Distrito '{distritoNome}' não encontrado.");
                continue;
            }

            var zinf = ResolveZinf(zinfs, areaInfluencia);
            if (zinf is null)
            {
                errors.Add($"Linha {rowNumber}: Área Influência '{areaInfluencia}' não encontrada.");
                continue;
            }

            var codigoPostalNumero = ParseCodigoPostal(codigoPostalRaw);
            if (!codigoPostalNumero.HasValue || !codigosPostaisSet.Contains(codigoPostalNumero.Value))
            {
                errors.Add($"Linha {rowNumber}: Código Postal '{codigoPostalRaw}' não encontrado.");
                continue;
            }

            if (!existingByCodigoEa.TryGetValue(codigoEa, out var instituicao))
            {
                instituicao = new Instituicao
                {
                    CodigoEA = codigoEa
                };
                _dbContext.Instituicoes.Add(instituicao);
                existingByCodigoEa[codigoEa] = instituicao;
                result.Inserted++;
            }
            else
            {
                result.Updated++;
            }

            instituicao.Nome = nome;
            instituicao.ConcelhoId = concelho.Id;
            instituicao.DistritoId = distrito.Id;
            instituicao.ZinfId = zinf.Id;
            instituicao.PessoaContacto = NullIfEmpty(pessoaContacto);
            instituicao.Telefone = NullIfEmpty(telefone);
            instituicao.Telemovel = NullIfEmpty(telemovel);
            instituicao.Email1 = NullIfEmpty(email1);
            instituicao.CodigoPostalNumero = codigoPostalNumero.Value;
            instituicao.Localidade = NullIfEmpty(localidade);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        result.Errors = errors.Take(200).ToList();

        var model = await BuildIndexViewModelAsync(cancellationToken: cancellationToken);
        model.ImportResult = result;
        model.StatusMessage = $"Importação concluída. Processadas: {result.ProcessedRows}, inseridas: {result.Inserted}, atualizadas: {result.Updated}.";
        return View(nameof(Index), model);
    }

    private async Task<InstituicoesIndexViewModel> BuildIndexViewModelAsync(
        string? codigoEA = null,
        string? nome = null,
        int? distritoId = null,
        string? concelho = null,
        int? zinfId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        var codigoEaFilter = (codigoEA ?? string.Empty).Trim();
        var nomeFilter = (nome ?? string.Empty).Trim();
        var concelhoFilter = (concelho ?? string.Empty).Trim();

        var distritoOptions = await _dbContext.Distritos
            .AsNoTracking()
            .OrderBy(x => x.Nome)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Nome
            })
            .ToListAsync(cancellationToken);

        var zinfOptions = await _dbContext.Zinfs
            .AsNoTracking()
            .OrderBy(x => x.Nome)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Nome
            })
            .ToListAsync(cancellationToken);

        var query = _dbContext.Instituicoes
            .AsNoTracking()
            .Include(x => x.Concelho)
            .Include(x => x.Distrito)
            .Include(x => x.Zinf)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(codigoEaFilter))
        {
            query = query.Where(x => EF.Functions.Like(x.CodigoEA, $"%{codigoEaFilter}%"));
        }

        if (!string.IsNullOrWhiteSpace(nomeFilter))
        {
            query = query.Where(x => EF.Functions.Like(x.Nome, $"%{nomeFilter}%"));
        }

        if (distritoId.HasValue)
        {
            query = query.Where(x => x.DistritoId == distritoId.Value);
        }

        if (!string.IsNullOrWhiteSpace(concelhoFilter))
        {
            query = query.Where(x => x.Concelho != null && EF.Functions.Like(x.Concelho.Nome, $"%{concelhoFilter}%"));
        }

        if (zinfId.HasValue)
        {
            query = query.Where(x => x.ZinfId == zinfId.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems <= 0 ? 1 : (int)Math.Ceiling((double)totalItems / pageSize);
        if (page > totalPages)
        {
            page = totalPages;
        }

        var instituicoes = await query
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

        return new InstituicoesIndexViewModel
        {
            Instituicoes = instituicoes,
            CodigoEA = codigoEaFilter,
            Nome = nomeFilter,
            DistritoId = distritoId,
            Concelho = concelhoFilter,
            ZinfId = zinfId,
            DistritoOptions = distritoOptions,
            ZinfOptions = zinfOptions,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };
    }

    private static string ReadCell(IXLRangeRow row, int colNumber)
    {
        if (colNumber <= 0)
        {
            return string.Empty;
        }

        return row.Cell(colNumber).GetString().Trim();
    }

    private static IXLRangeRow? FindHeaderRow(IReadOnlyList<IXLRangeRow> rows)
    {
        foreach (var row in rows.Take(30))
        {
            var normalizedHeaders = row.CellsUsed()
                .Select(c => NormalizeHeader(c.GetString()))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (normalizedHeaders.Contains("CODIGOEA") &&
                normalizedHeaders.Contains("NOME") &&
                normalizedHeaders.Contains("CONCELHO") &&
                normalizedHeaders.Contains("DISTRITO"))
            {
                return row;
            }
        }

        return null;
    }

    private static string NormalizeHeader(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (!char.IsLetterOrDigit(c))
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }

    private static int? ParseCodigoPostal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var parsed) ? parsed : null;
    }

    private static string? NullIfEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ZinfLookup? ResolveZinf(IReadOnlyList<ZinfLookup> zinfs, string areaInfluencia)
    {
        if (string.IsNullOrWhiteSpace(areaInfluencia))
        {
            return null;
        }

        var value = areaInfluencia.Trim();
        var exact = zinfs.FirstOrDefault(x => string.Equals(x.Nome, value, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var byPrefix = zinfs.FirstOrDefault(x => x.Nome.StartsWith(value + "-", StringComparison.OrdinalIgnoreCase));
        return byPrefix;
    }

    private sealed record ZinfLookup(int Id, string Nome);
}
