using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.Domain.Entities;
using RiskAnalysis.Domain.Enums;
using RiskAnalysis.Infrastructure.Persistence;

namespace RiskAnalysis.Infrastructure.Services;

/// <summary>
/// Подсистема загрузки котировок.
///
/// Идемпотентность загрузки обеспечивается сопоставлением полученных записей с
/// уже имеющимися по дате торгов: совпадающие записи обновляются, отсутствующие
/// добавляются. Уникальный индекс по паре «инструмент — дата торгов» в базе
/// данных исключает появление дубликатов при одновременном выполнении загрузок.
/// </summary>
public class QuoteImportService : IQuoteImportService
{
    /// <summary>
    /// Глубина истории, загружаемая при первом обращении к инструменту, лет.
    /// Десятилетний период охватывает несколько рыночных циклов, включая
    /// кризисные периоды, что необходимо для стресс-тестирования.
    /// </summary>
    private const int DefaultHistoryYears = 10;

    private readonly RiskAnalysisDbContext _db;
    private readonly IMarketDataClient _marketData;
    private readonly ILogger<QuoteImportService> _logger;

    public QuoteImportService(
        RiskAnalysisDbContext db,
        IMarketDataClient marketData,
        ILogger<QuoteImportService> logger)
    {
        _db = db;
        _marketData = marketData;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ImportResult> ImportQuotesAsync(
        int instrumentId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var instrument = await _db.Instruments
            .FirstOrDefaultAsync(i => i.Id == instrumentId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Инструмент с идентификатором {instrumentId} не найден в справочнике.");

        if (from > to)
        {
            throw new ArgumentException("Начало периода не может быть позже его окончания.", nameof(from));
        }

        var stopwatch = Stopwatch.StartNew();
        var startedAt = DateTimeOffset.UtcNow;

        var log = new ImportLog
        {
            Source = _marketData.SourceName,
            InstrumentId = instrument.Id,
            DateFrom = from,
            DateTo = to,
            StartedAt = startedAt
        };

        try
        {
            var received = await _marketData.GetHistoryAsync(
                instrument.Engine, instrument.Market, instrument.Board, instrument.Ticker,
                from, to, cancellationToken);

            // Существующие котировки за период выбираются одним запросом,
            // чтобы избежать обращения к базе данных по каждой записи.
            var existing = await _db.Quotes
                .Where(q => q.InstrumentId == instrument.Id
                            && q.TradeDate >= from
                            && q.TradeDate <= to)
                .ToDictionaryAsync(q => q.TradeDate, cancellationToken);

            var inserted = 0;
            var updated = 0;

            foreach (var quote in received)
            {
                if (existing.TryGetValue(quote.TradeDate, out var stored))
                {
                    // Биржа пересматривает итоги торгов, поэтому ранее
                    // загруженные значения подлежат обновлению.
                    if (stored.Open == quote.Open && stored.High == quote.High &&
                        stored.Low == quote.Low && stored.Close == quote.Close &&
                        stored.Volume == quote.Volume && stored.Turnover == quote.Turnover)
                    {
                        continue;
                    }

                    stored.Open = quote.Open;
                    stored.High = quote.High;
                    stored.Low = quote.Low;
                    stored.Close = quote.Close;
                    stored.Volume = quote.Volume;
                    stored.Turnover = quote.Turnover;
                    updated++;
                }
                else
                {
                    _db.Quotes.Add(new Quote
                    {
                        InstrumentId = instrument.Id,
                        TradeDate = quote.TradeDate,
                        Open = quote.Open,
                        High = quote.High,
                        Low = quote.Low,
                        Close = quote.Close,
                        Volume = quote.Volume,
                        Turnover = quote.Turnover
                    });
                    inserted++;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await UpdateHistoryBoundsAsync(instrument, cancellationToken);

            stopwatch.Stop();

            log.RowsReceived = received.Count;
            log.RowsInserted = inserted;
            log.RowsUpdated = updated;
            log.Status = received.Count > 0 ? ImportStatus.Success : ImportStatus.PartialSuccess;
            log.Message = received.Count > 0
                ? null
                : "Источник не вернул котировок за указанный период.";
            log.FinishedAt = DateTimeOffset.UtcNow;
            log.DurationMs = (int)stopwatch.ElapsedMilliseconds;

            _db.ImportLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Загрузка {Ticker} за {From}–{To}: получено {Received}, добавлено {Inserted}, обновлено {Updated}, {Duration} мс",
                instrument.Ticker, from, to, received.Count, inserted, updated, stopwatch.ElapsedMilliseconds);

            return new ImportResult(
                instrument.Id, instrument.Ticker, from, to,
                received.Count, inserted, updated,
                log.Status, log.Message, log.DurationMs);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();

            log.Status = ImportStatus.Failed;
            log.Message = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
            log.FinishedAt = DateTimeOffset.UtcNow;
            log.DurationMs = (int)stopwatch.ElapsedMilliseconds;

            // Журнал отказа сохраняется в отдельном контексте изменений,
            // чтобы неудачная загрузка не осталась незафиксированной.
            _db.ChangeTracker.Clear();
            _db.ImportLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex,
                "Загрузка котировок {Ticker} за период {From}–{To} завершилась ошибкой",
                instrument.Ticker, from, to);

            return new ImportResult(
                instrument.Id, instrument.Ticker, from, to,
                0, 0, 0, ImportStatus.Failed, log.Message, log.DurationMs);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ImportResult>> ImportAllActiveAsync(
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var instruments = await _db.Instruments
            .Where(i => i.IsActive)
            .OrderBy(i => i.Ticker)
            .Select(i => new { i.Id, i.Ticker, i.HistoryTo })
            .ToListAsync(cancellationToken);

        var results = new List<ImportResult>(instruments.Count);

        foreach (var instrument in instruments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Для инструмента с уже загруженной историей запрашивается только
            // недостающий период, начиная с последней имеющейся даты.
            // Последняя дата включается в запрос повторно, так как итоги
            // торгов последнего дня могут быть пересмотрены биржей.
            var from = instrument.HistoryTo ?? to.AddYears(-DefaultHistoryYears);

            if (from > to)
            {
                continue;
            }

            results.Add(await ImportQuotesAsync(instrument.Id, from, to, cancellationToken));
        }

        _logger.LogInformation(
            "Плановая загрузка завершена: обработано инструментов {Count}, добавлено котировок {Inserted}",
            results.Count, results.Sum(r => r.RowsInserted));

        return results;
    }

    /// <summary>
    /// Пересчитывает фактические границы имеющейся истории котировок
    /// инструмента. Эти значения используются для контроля полноты исходных
    /// данных и для определения периода довыгрузки.
    /// </summary>
    private async Task UpdateHistoryBoundsAsync(Instrument instrument, CancellationToken cancellationToken)
    {
        var bounds = await _db.Quotes
            .Where(q => q.InstrumentId == instrument.Id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Min = g.Min(q => q.TradeDate),
                Max = g.Max(q => q.TradeDate)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (bounds is null)
        {
            return;
        }

        instrument.HistoryFrom = bounds.Min;
        instrument.HistoryTo = bounds.Max;
        instrument.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
