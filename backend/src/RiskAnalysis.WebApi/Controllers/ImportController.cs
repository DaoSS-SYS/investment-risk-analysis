using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.Infrastructure.Persistence;

namespace RiskAnalysis.WebApi.Controllers;

/// <summary>
/// Управление загрузкой данных из внешних источников и журнал загрузок.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ImportController : ControllerBase
{
    private readonly RiskAnalysisDbContext _db;
    private readonly IQuoteImportService _importService;

    public ImportController(RiskAnalysisDbContext db, IQuoteImportService importService)
    {
        _db = db;
        _importService = importService;
    }

    /// <summary>
    /// Догружает котировки по всем активным инструментам справочника
    /// с даты последней имеющейся котировки по указанную дату.
    /// </summary>
    /// <param name="to">Конец периода. По умолчанию — текущая дата.</param>
    [HttpPost("quotes/all")]
    public async Task<ActionResult<IReadOnlyList<ImportResult>>> ImportAll(
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var dateTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var results = await _importService.ImportAllActiveAsync(dateTo, cancellationToken);

        return Ok(results);
    }

    /// <summary>Возвращает журнал загрузок данных.</summary>
    /// <param name="limit">Максимальное число записей.</param>
    [HttpGet("logs")]
    public async Task<ActionResult<object>> GetLogs(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var logs = await _db.ImportLogs
            .AsNoTracking()
            .OrderByDescending(l => l.StartedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(l => new
            {
                l.Id,
                l.Source,
                Ticker = l.Instrument != null ? l.Instrument.Ticker : null,
                l.DateFrom,
                l.DateTo,
                l.RowsReceived,
                l.RowsInserted,
                l.RowsUpdated,
                Status = l.Status.ToString(),
                l.Message,
                l.StartedAt,
                l.DurationMs
            })
            .ToListAsync(cancellationToken);

        return Ok(logs);
    }
}
