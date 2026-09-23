using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.Domain.Entities;
using RiskAnalysis.Infrastructure.Persistence;
using RiskAnalysis.WebApi.Contracts;

namespace RiskAnalysis.WebApi.Controllers;

/// <summary>
/// Справочник финансовых инструментов и история их котировок.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class InstrumentsController : ControllerBase
{
    private readonly RiskAnalysisDbContext _db;
    private readonly IMarketDataClient _marketData;
    private readonly IQuoteImportService _importService;

    public InstrumentsController(
        RiskAnalysisDbContext db,
        IMarketDataClient marketData,
        IQuoteImportService importService)
    {
        _db = db;
        _marketData = marketData;
        _importService = importService;
    }

    /// <summary>Возвращает все инструменты справочника системы.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InstrumentDto>>> GetAll(CancellationToken cancellationToken)
    {
        var instruments = await _db.Instruments
            .AsNoTracking()
            .OrderBy(i => i.Ticker)
            .Select(i => new InstrumentDto(
                i.Id, i.Ticker, i.ShortName, i.FullName, i.SecurityType,
                i.Engine, i.Market, i.Board, i.Currency, i.Isin, i.Sector,
                i.IsActive, i.HistoryFrom, i.HistoryTo,
                i.Quotes.Count))
            .ToListAsync(cancellationToken);

        return Ok(instruments);
    }

    /// <summary>
    /// Ищет инструменты в справочнике Московской Биржи по биржевому коду
    /// или части наименования. Результат поиска не сохраняется в системе.
    /// </summary>
    /// <param name="query">Строка поиска, например SBER или Сбербанк.</param>
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<SecuritySearchResultDto>>> Search(
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "Не задана строка поиска." });
        }

        var found = await _marketData.SearchSecuritiesAsync(query, cancellationToken);

        var existingKeys = await _db.Instruments
            .AsNoTracking()
            .Select(i => i.Ticker + "|" + i.Board)
            .ToListAsync(cancellationToken);

        var existing = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = found
            .Select(s => new SecuritySearchResultDto(
                s.Ticker, s.ShortName, s.FullName, s.SecurityType,
                s.Engine, s.Market, s.Board, s.Isin,
                existing.Contains($"{s.Ticker}|{s.Board}")))
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// Добавляет инструмент в справочник системы. Реквизиты инструмента
    /// получаются из справочника Московской Биржи по биржевому коду.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<InstrumentDto>> Add(
        [FromBody] AddInstrumentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Ticker))
        {
            return BadRequest(new { error = "Не указан биржевой код инструмента." });
        }

        var ticker = request.Ticker.Trim().ToUpperInvariant();

        var candidates = await _marketData.SearchSecuritiesAsync(ticker, cancellationToken);

        SecurityInfo? security = candidates.FirstOrDefault(s =>
            s.Ticker.Equals(ticker, StringComparison.OrdinalIgnoreCase) &&
            (request.Board is null || s.Board.Equals(request.Board, StringComparison.OrdinalIgnoreCase)));

        if (security is null)
        {
            return NotFound(new
            {
                error = $"Инструмент {ticker} не найден в справочнике Московской Биржи " +
                        "либо снят с торгов."
            });
        }

        var duplicate = await _db.Instruments.AnyAsync(
            i => i.Ticker == security.Ticker && i.Board == security.Board,
            cancellationToken);

        if (duplicate)
        {
            return Conflict(new
            {
                error = $"Инструмент {security.Ticker} в режиме торгов {security.Board} " +
                        "уже присутствует в справочнике."
            });
        }

        var now = DateTimeOffset.UtcNow;

        var instrument = new Instrument
        {
            Ticker = security.Ticker,
            ShortName = security.ShortName,
            FullName = security.FullName,
            SecurityType = security.SecurityType,
            Engine = security.Engine,
            Market = security.Market,
            Board = security.Board,
            Currency = security.Currency ?? "RUB",
            Isin = security.Isin,
            Sector = request.Sector,
            LotSize = security.LotSize,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Instruments.Add(instrument);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = new InstrumentDto(
            instrument.Id, instrument.Ticker, instrument.ShortName, instrument.FullName,
            instrument.SecurityType, instrument.Engine, instrument.Market, instrument.Board,
            instrument.Currency, instrument.Isin, instrument.Sector, instrument.IsActive,
            instrument.HistoryFrom, instrument.HistoryTo, 0);

        return CreatedAtAction(nameof(GetAll), new { id = instrument.Id }, dto);
    }

    /// <summary>Удаляет инструмент вместе с его историей котировок.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var instrument = await _db.Instruments.FindAsync([id], cancellationToken);

        if (instrument is null)
        {
            return NotFound();
        }

        _db.Instruments.Remove(instrument);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Загружает историю котировок инструмента из внешнего источника
    /// за указанный период. Повторная загрузка того же периода не создаёт
    /// дубликатов.
    /// </summary>
    /// <param name="id">Идентификатор инструмента.</param>
    /// <param name="from">Начало периода. По умолчанию — десять лет назад.</param>
    /// <param name="to">Конец периода. По умолчанию — текущая дата.</param>
    [HttpPost("{id:int}/quotes/import")]
    public async Task<ActionResult<ImportResult>> ImportQuotes(
        int id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var dateTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dateFrom = from ?? dateTo.AddYears(-10);

        if (!await _db.Instruments.AnyAsync(i => i.Id == id, cancellationToken))
        {
            return NotFound(new { error = $"Инструмент с идентификатором {id} не найден." });
        }

        var result = await _importService.ImportQuotesAsync(id, dateFrom, dateTo, cancellationToken);

        return Ok(result);
    }

    /// <summary>Возвращает историю котировок инструмента из базы данных.</summary>
    /// <param name="id">Идентификатор инструмента.</param>
    /// <param name="from">Начало периода.</param>
    /// <param name="to">Конец периода.</param>
    [HttpGet("{id:int}/quotes")]
    public async Task<ActionResult<IReadOnlyList<QuoteDto>>> GetQuotes(
        int id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var query = _db.Quotes.AsNoTracking().Where(q => q.InstrumentId == id);

        if (from is not null)
        {
            query = query.Where(q => q.TradeDate >= from.Value);
        }

        if (to is not null)
        {
            query = query.Where(q => q.TradeDate <= to.Value);
        }

        var quotes = await query
            .OrderBy(q => q.TradeDate)
            .Select(q => new QuoteDto(q.TradeDate, q.Open, q.High, q.Low, q.Close, q.Volume, q.Turnover))
            .ToListAsync(cancellationToken);

        return Ok(quotes);
    }
}
