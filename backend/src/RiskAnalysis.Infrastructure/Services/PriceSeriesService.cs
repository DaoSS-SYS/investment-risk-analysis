using Microsoft.EntityFrameworkCore;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.Domain.Enums;
using RiskAnalysis.Infrastructure.Persistence;

namespace RiskAnalysis.Infrastructure.Services;

/// <summary>
/// Поставщик подготовленных ценовых рядов.
///
/// Корректировка на корпоративные действия применяется при построении ряда,
/// а не при сохранении котировок. Исходные данные, полученные от биржи,
/// остаются неизменными, что обеспечивает прослеживаемость предобработки
/// и позволяет пересмотреть классификацию аномалии без повторной загрузки.
/// </summary>
public class PriceSeriesService : IPriceSeriesProvider
{
    private readonly RiskAnalysisDbContext _db;

    public PriceSeriesService(RiskAnalysisDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<PriceSeries> GetSeriesAsync(
        int instrumentId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var instrument = await _db.Instruments
            .AsNoTracking()
            .Where(i => i.Id == instrumentId)
            .Select(i => new { i.Id, i.Ticker })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"Инструмент с идентификатором {instrumentId} не найден в справочнике.");

        var quotes = await _db.Quotes
            .AsNoTracking()
            .Where(q => q.InstrumentId == instrumentId && q.TradeDate >= from && q.TradeDate <= to)
            .OrderBy(q => q.TradeDate)
            .Select(q => new { q.TradeDate, q.Close })
            .ToListAsync(cancellationToken);

        var adjustments = await GetAdjustmentsAsync(instrumentId, cancellationToken);

        var points = ApplyAdjustments(
            quotes.Select(q => (q.TradeDate, q.Close)).ToList(),
            adjustments);

        return new PriceSeries(instrument.Id, instrument.Ticker, points, adjustments.Count);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PriceSeries>> GetAlignedSeriesAsync(
        IReadOnlyCollection<int> instrumentIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (instrumentIds.Count == 0)
        {
            return [];
        }

        var series = new List<PriceSeries>(instrumentIds.Count);

        foreach (var id in instrumentIds)
        {
            series.Add(await GetSeriesAsync(id, from, to, cancellationToken));
        }

        // Общий торговый календарь: пересечение дат по всем инструментам.
        // Несовпадение календарей возникает при приостановке торгов отдельной
        // бумагой и при различиях в датах начала обращения. Сопоставление
        // доходностей разных торговых дней исказило бы ковариационную матрицу.
        HashSet<DateOnly>? commonDates = null;

        foreach (var item in series)
        {
            var dates = item.Points.Select(p => p.Date).ToHashSet();

            if (commonDates is null)
            {
                commonDates = dates;
            }
            else
            {
                commonDates.IntersectWith(dates);
            }
        }

        if (commonDates is null || commonDates.Count == 0)
        {
            return series
                .Select(s => s with { Points = [] })
                .ToList();
        }

        return series
            .Select(s => s with
            {
                Points = s.Points.Where(p => commonDates.Contains(p.Date)).ToList()
            })
            .ToList();
    }

    /// <summary>
    /// Возвращает применяемые корректировки инструмента, упорядоченные
    /// по убыванию даты.
    /// </summary>
    private async Task<List<(DateOnly Date, decimal Factor)>> GetAdjustmentsAsync(
        int instrumentId,
        CancellationToken cancellationToken)
    {
        var actions = await _db.CorporateActions
            .AsNoTracking()
            .Where(a => a.InstrumentId == instrumentId
                        && a.IsApplied
                        && a.ActionType != CorporateActionType.MarketEvent)
            .OrderByDescending(a => a.ActionDate)
            .Select(a => new { a.ActionDate, a.AdjustmentFactor })
            .ToListAsync(cancellationToken);

        return actions.Select(a => (a.ActionDate, a.AdjustmentFactor)).ToList();
    }

    /// <summary>
    /// Применяет корректировки к ценовому ряду.
    ///
    /// Корректировке подлежат котировки, предшествующие дате корпоративного
    /// действия. Ряд обходится от последней даты к первой с накоплением
    /// произведения множителей: при наличии нескольких корпоративных действий
    /// к ранним котировкам применяется произведение всех последующих
    /// множителей. Такой обход даёт линейную сложность относительно длины ряда.
    /// </summary>
    private static List<PricePoint> ApplyAdjustments(
        IReadOnlyList<(DateOnly Date, decimal Close)> quotes,
        IReadOnlyList<(DateOnly Date, decimal Factor)> adjustmentsDescending)
    {
        var points = new PricePoint[quotes.Count];

        var cumulativeFactor = 1m;
        var adjustmentIndex = 0;

        for (var i = quotes.Count - 1; i >= 0; i--)
        {
            var (date, close) = quotes[i];

            while (adjustmentIndex < adjustmentsDescending.Count &&
                   adjustmentsDescending[adjustmentIndex].Date > date)
            {
                cumulativeFactor *= adjustmentsDescending[adjustmentIndex].Factor;
                adjustmentIndex++;
            }

            points[i] = new PricePoint(date, close, close * cumulativeFactor);
        }

        return [.. points];
    }
}
