using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.Domain.Entities;
using RiskAnalysis.Domain.Enums;
using RiskAnalysis.Infrastructure.Persistence;

namespace RiskAnalysis.Infrastructure.Services;

/// <summary>
/// Предобработка данных: выявление аномалий ценовых рядов и их классификация.
///
/// Московская Биржа публикует котировки в том виде, в каком сделки совершались
/// в соответствующий торговый день, без корректировки на корпоративные
/// действия. При дроблении акций цена снижается кратно коэффициенту дробления,
/// тогда как стоимость позиции инвестора не изменяется. В ценовом ряду это
/// выглядит как падение на десятки процентов.
///
/// Так, дробление обыкновенных акций ПАО «ГМК «Норильский никель» с
/// коэффициентом 1 к 100, проведённое 8 апреля 2024 года, даёт однодневную
/// логарифмическую доходность порядка минус 4,6. Без корректировки одно это
/// значение многократно завышает оценку волатильности инструмента и,
/// как следствие, все производные от неё показатели риска.
///
/// В то же время резкие изменения цены могут отражать действительное изменение
/// рыночной стоимости — например, снижение котировок 24 февраля 2022 года.
/// Такие наблюдения корректировке не подлежат: они образуют «тяжёлые хвосты»
/// распределения доходностей, учёт которых составляет предмет исследования,
/// и служат основой исторических сценариев стресс-тестирования.
///
/// Задача алгоритма состоит не в исключении выбросов, а в разделении
/// технических аномалий и действительных рыночных событий.
/// </summary>
public class CorporateActionService : ICorporateActionService
{
    /// <summary>
    /// Порог выявления аномалии по модулю логарифмической доходности.
    /// Значение 0,40 соответствует изменению цены примерно на минус 33 либо
    /// плюс 49 процентов за один торговый день. Порог выбран так, чтобы
    /// обнаруживать как дробления, так и наиболее значимые рыночные шоки,
    /// подлежащие проверке.
    /// </summary>
    private const double AnomalyThreshold = 0.40;

    /// <summary>
    /// Допустимое относительное отклонение наблюдаемого отношения цен от
    /// коэффициента дробления. В день проведения корпоративного действия цена
    /// изменяется не только вследствие дробления, но и под влиянием торгов,
    /// поэтому точного совпадения не наблюдается. Так, для дробления акций
    /// «Норильского никеля» наблюдаемое отношение составило 98,4 при
    /// коэффициенте 100, то есть отклонение 1,6 процента.
    /// </summary>
    private const decimal RatioTolerance = 0.05m;

    /// <summary>
    /// Коэффициенты, применяемые при дроблении и консолидации акций
    /// в практике российского фондового рынка.
    /// </summary>
    private static readonly decimal[] KnownRatios =
        [2m, 3m, 4m, 5m, 6m, 8m, 10m, 20m, 50m, 100m, 1000m];

    private readonly RiskAnalysisDbContext _db;
    private readonly ILogger<CorporateActionService> _logger;

    public CorporateActionService(RiskAnalysisDbContext db, ILogger<CorporateActionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AnomalyDetectionResult> DetectAsync(
        int instrumentId,
        CancellationToken cancellationToken = default)
    {
        var instrument = await _db.Instruments
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == instrumentId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Инструмент с идентификатором {instrumentId} не найден в справочнике.");

        var quotes = await _db.Quotes
            .AsNoTracking()
            .Where(q => q.InstrumentId == instrumentId)
            .OrderBy(q => q.TradeDate)
            .Select(q => new { q.TradeDate, q.Close })
            .ToListAsync(cancellationToken);

        // Даты, по которым запись уже существует, повторно не обрабатываются:
        // подтверждённая пользователем классификация имеет приоритет
        // над результатом автоматического выявления.
        var knownDates = await _db.CorporateActions
            .Where(a => a.InstrumentId == instrumentId)
            .Select(a => a.ActionDate)
            .ToListAsync(cancellationToken);

        var known = knownDates.ToHashSet();

        var anomalies = new List<DetectedAnomaly>();
        var newRecords = 0;
        var now = DateTimeOffset.UtcNow;

        for (var i = 1; i < quotes.Count; i++)
        {
            var previous = quotes[i - 1];
            var current = quotes[i];

            if (previous.Close <= 0m || current.Close <= 0m)
            {
                continue;
            }

            var observedRatio = current.Close / previous.Close;
            var logReturn = Math.Log((double)observedRatio);

            if (Math.Abs(logReturn) <= AnomalyThreshold)
            {
                continue;
            }

            var anomaly = Classify(current.TradeDate, previous.Close, current.Close,
                                   observedRatio, (decimal)logReturn);

            anomalies.Add(anomaly);

            if (known.Contains(current.TradeDate))
            {
                continue;
            }

            var isCorporateAction = anomaly.Classification != CorporateActionType.MarketEvent;

            _db.CorporateActions.Add(new CorporateAction
            {
                InstrumentId = instrumentId,
                ActionDate = anomaly.Date,
                ActionType = anomaly.Classification,
                Ratio = anomaly.Ratio,
                AdjustmentFactor = anomaly.AdjustmentFactor,
                ObservedRatio = anomaly.ObservedRatio,
                ObservedLogReturn = anomaly.ObservedLogReturn,
                Source = DetectionSource.Automatic,

                // Автоматическая классификация подлежит проверке пользователем.
                IsConfirmed = false,

                // Корректировка применяется сразу: в противном случае расчёты
                // по умолчанию выполнялись бы на заведомо искажённых данных.
                IsApplied = isCorporateAction,

                Comment = anomaly.Explanation,
                CreatedAt = now,
                UpdatedAt = now
            });

            newRecords++;
        }

        if (newRecords > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        var corporateActions = anomalies.Count(a => a.Classification != CorporateActionType.MarketEvent);

        _logger.LogInformation(
            "Предобработка {Ticker}: проверено котировок {Quotes}, аномалий {Anomalies}, " +
            "корпоративных действий {Actions}, рыночных событий {Events}, новых записей {New}",
            instrument.Ticker, quotes.Count, anomalies.Count,
            corporateActions, anomalies.Count - corporateActions, newRecords);

        return new AnomalyDetectionResult(
            instrumentId,
            instrument.Ticker,
            quotes.Count,
            anomalies.Count,
            corporateActions,
            anomalies.Count - corporateActions,
            newRecords,
            anomalies);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AnomalyDetectionResult>> DetectAllAsync(
        CancellationToken cancellationToken = default)
    {
        var ids = await _db.Instruments
            .OrderBy(i => i.Ticker)
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

        var results = new List<AnomalyDetectionResult>(ids.Count);

        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await DetectAsync(id, cancellationToken));
        }

        return results;
    }

    /// <summary>
    /// Классифицирует аномалию ценового ряда.
    ///
    /// Наблюдаемое отношение цен сопоставляется с коэффициентами дробления,
    /// применяемыми на практике. При совпадении в пределах допуска аномалия
    /// признаётся корпоративным действием и подлежит корректировке; в
    /// противном случае — действительным рыночным событием.
    /// </summary>
    private static DetectedAnomaly Classify(
        DateOnly date,
        decimal previousClose,
        decimal close,
        decimal observedRatio,
        decimal logReturn)
    {
        // Направление изменения определяет вид корпоративного действия:
        // снижение цены соответствует дроблению, рост — консолидации.
        var isPriceDrop = observedRatio < 1m;

        var candidate = isPriceDrop
            ? previousClose / close
            : close / previousClose;

        var matched = FindKnownRatio(candidate);

        if (matched is null)
        {
            return new DetectedAnomaly(
                date, previousClose, close, observedRatio, logReturn,
                CorporateActionType.MarketEvent,
                Ratio: observedRatio,
                AdjustmentFactor: 1m,
                Explanation:
                    $"Отношение цен {candidate:F4} не соответствует ни одному коэффициенту " +
                    "дробления в пределах допуска. Изменение цены признано действительным " +
                    "рыночным событием, корректировка не применяется.");
        }

        var deviation = Math.Abs(candidate - matched.Value) / matched.Value * 100m;

        return isPriceDrop
            ? new DetectedAnomaly(
                date, previousClose, close, observedRatio, logReturn,
                CorporateActionType.Split,
                Ratio: matched.Value,
                // Котировки до даты дробления делятся на коэффициент.
                AdjustmentFactor: 1m / matched.Value,
                Explanation:
                    $"Дробление акций с коэффициентом 1 к {matched.Value:0.##}. " +
                    $"Наблюдаемое отношение цен {candidate:F4}, отклонение {deviation:F2} процента. " +
                    $"Котировки до {date:dd.MM.yyyy} умножаются на {1m / matched.Value:G}.")
            : new DetectedAnomaly(
                date, previousClose, close, observedRatio, logReturn,
                CorporateActionType.ReverseSplit,
                Ratio: matched.Value,
                // Котировки до даты консолидации умножаются на коэффициент.
                AdjustmentFactor: matched.Value,
                Explanation:
                    $"Консолидация акций с коэффициентом {matched.Value:0.##} к 1. " +
                    $"Наблюдаемое отношение цен {candidate:F4}, отклонение {deviation:F2} процента. " +
                    $"Котировки до {date:dd.MM.yyyy} умножаются на {matched.Value:G}.");
    }

    /// <summary>
    /// Находит коэффициент дробления, ближайший к наблюдаемому отношению цен,
    /// при условии, что относительное отклонение не превышает допуска.
    /// </summary>
    private static decimal? FindKnownRatio(decimal candidate)
    {
        decimal? best = null;
        var bestDeviation = decimal.MaxValue;

        foreach (var ratio in KnownRatios)
        {
            var deviation = Math.Abs(candidate - ratio) / ratio;

            if (deviation <= RatioTolerance && deviation < bestDeviation)
            {
                best = ratio;
                bestDeviation = deviation;
            }
        }

        return best;
    }
}
