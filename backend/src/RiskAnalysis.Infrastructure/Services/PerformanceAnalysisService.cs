using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.Infrastructure.Persistence;
using RiskAnalysis.RiskEngine.Performance;
using RiskAnalysis.RiskEngine.Returns;
using RiskAnalysis.RiskEngine.Statistics;

namespace RiskAnalysis.Infrastructure.Services;

/// <summary>
/// Анализ эффективности вложений и корреляционный анализ.
/// </summary>
public class PerformanceAnalysisService : IPerformanceAnalysisService
{
    private readonly RiskAnalysisDbContext _db;
    private readonly IPriceSeriesProvider _priceSeries;
    private readonly IMacroImportService _macro;

    public PerformanceAnalysisService(
        RiskAnalysisDbContext db,
        IPriceSeriesProvider priceSeries,
        IMacroImportService macro)
    {
        _db = db;
        _priceSeries = priceSeries;
        _macro = macro;
    }

    /// <inheritdoc />
    public async Task<PerformanceAnalysisResult> AnalyzeAsync(
        int instrumentId,
        int? benchmarkInstrumentId,
        DateOnly from,
        DateOnly to,
        bool includeDrawdownSeries = false,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var riskFree = await _macro.GetRiskFreeRateAsync(from, to, cancellationToken);

        // При наличии эталонного портфеля ряды приводятся к общему торговому
        // календарю: оценка коэффициента бета по несовпадающим датам лишена
        // смысла.
        var ids = benchmarkInstrumentId is null || benchmarkInstrumentId == instrumentId
            ? new[] { instrumentId }
            : [instrumentId, benchmarkInstrumentId.Value];

        var series = await _priceSeries.GetAlignedSeriesAsync(ids, from, to, cancellationToken);

        var assetSeries = series[0];

        if (assetSeries.Points.Count < 31)
        {
            throw new InvalidOperationException(
                $"По инструменту {assetSeries.Ticker} за период с {from:dd.MM.yyyy} " +
                $"по {to:dd.MM.yyyy} недостаточно котировок для анализа эффективности.");
        }

        var assetPrices = ToObservations(assetSeries);
        var assetReturns = ReturnCalculator.Values(
            ReturnCalculator.Calculate(assetPrices, ReturnType.Logarithmic));

        var statistics = DescriptiveStatisticsCalculator.Calculate(assetReturns);
        var drawdown = DrawdownCalculator.Calculate(assetPrices, includeDrawdownSeries);

        var performance = PerformanceCalculator.Calculate(
            assetReturns, riskFree.AnnualRate, drawdown.MaxDrawdown);

        CapmResult? capm = null;
        string? benchmarkTicker = null;

        if (series.Count > 1)
        {
            var benchmarkSeries = series[1];
            benchmarkTicker = benchmarkSeries.Ticker;

            var benchmarkReturns = ReturnCalculator.Values(
                ReturnCalculator.Calculate(ToObservations(benchmarkSeries), ReturnType.Logarithmic));

            capm = CapmCalculator.Calculate(assetReturns, benchmarkReturns, riskFree.AnnualRate);
        }

        stopwatch.Stop();

        return new PerformanceAnalysisResult(
            InstrumentId: assetSeries.InstrumentId,
            Ticker: assetSeries.Ticker,
            BenchmarkTicker: benchmarkTicker,
            From: assetSeries.Points[0].Date,
            To: assetSeries.Points[^1].Date,
            ReturnCount: assetReturns.Length,
            RiskFreeRate: riskFree,
            Statistics: statistics,
            Performance: performance,
            Drawdown: drawdown,
            Capm: capm,
            DurationMs: (int)stopwatch.ElapsedMilliseconds);
    }

    /// <inheritdoc />
    public async Task<CorrelationAnalysisResult> AnalyzeCorrelationAsync(
        IReadOnlyCollection<int> instrumentIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (instrumentIds.Count < 2)
        {
            throw new InvalidOperationException(
                "Корреляционный анализ требует не менее двух инструментов.");
        }

        var series = await _priceSeries.GetAlignedSeriesAsync(
            instrumentIds, from, to, cancellationToken);

        if (series.Any(s => s.Points.Count < 31))
        {
            throw new InvalidOperationException(
                "После приведения рядов к общему торговому календарю по отдельным " +
                "инструментам осталось недостаточно наблюдений. Проверьте полноту " +
                "загруженной истории котировок.");
        }

        var returns = series
            .Select(s => ReturnCalculator.Values(
                ReturnCalculator.Calculate(ToObservations(s), ReturnType.Logarithmic)))
            .ToList();

        var covariance = CovarianceCalculator.Covariance(returns);
        var correlation = CovarianceCalculator.CorrelationFromCovariance(covariance);

        var count = series.Count;
        var annualizationFactor = Math.Sqrt(ReturnCalculator.TradingDaysPerYear);

        var volatilities = new double[count];

        for (var i = 0; i < count; i++)
        {
            volatilities[i] = Math.Sqrt(covariance[i, i]) * annualizationFactor;
        }

        // Равновзвешенный портфель: доли инструментов одинаковы.
        var weights = Enumerable.Repeat(1.0 / count, count).ToArray();

        var portfolioVolatility =
            CovarianceCalculator.PortfolioStandardDeviation(weights, covariance) * annualizationFactor;

        // Средневзвешенная волатильность составляющих равна волатильности
        // портфеля при полной положительной корреляции: она служит эталоном,
        // относительно которого измеряется эффект диверсификации.
        var weightedAverage = volatilities.Average();

        var diversificationEffect = weightedAverage > 0
            ? 1.0 - portfolioVolatility / weightedAverage
            : 0.0;

        double correlationSum = 0.0;
        var pairCount = 0;

        for (var i = 0; i < count; i++)
        {
            for (var j = i + 1; j < count; j++)
            {
                correlationSum += correlation[i, j];
                pairCount++;
            }
        }

        var averageCorrelation = pairCount > 0 ? correlationSum / pairCount : 0.0;

        var matrix = new double[count][];

        for (var i = 0; i < count; i++)
        {
            matrix[i] = new double[count];

            for (var j = 0; j < count; j++)
            {
                matrix[i][j] = correlation[i, j];
            }
        }

        var conclusion =
            $"Средняя парная корреляция составляет {averageCorrelation:F3}. " +
            $"Волатильность равновзвешенного портфеля {portfolioVolatility:P2} годовых " +
            $"против средневзвешенной волатильности составляющих {weightedAverage:P2}: " +
            $"диверсификация снижает риск на {diversificationEffect:P1}. " +
            (averageCorrelation > 0.7
                ? "Высокая корреляция инструментов ограничивает возможности снижения " +
                  "риска за счёт диверсификации в пределах рассматриваемой группы."
                : "Корреляция инструментов оставляет возможности дальнейшего снижения " +
                  "риска за счёт изменения структуры портфеля.");

        return new CorrelationAnalysisResult(
            Tickers: series.Select(s => s.Ticker).ToList(),
            From: series[0].Points[0].Date,
            To: series[0].Points[^1].Date,
            ObservationCount: returns[0].Length,
            Correlation: matrix,
            AnnualizedVolatility: volatilities,
            EqualWeightedPortfolioVolatility: portfolioVolatility,
            WeightedAverageVolatility: weightedAverage,
            DiversificationEffect: diversificationEffect,
            AverageCorrelation: averageCorrelation,
            Conclusion: conclusion);
    }

    /// <summary>
    /// Преобразует ценовой ряд к типу расчётного ядра.
    /// </summary>
    private static PriceObservation[] ToObservations(PriceSeries series)
    {
        var result = new PriceObservation[series.Points.Count];

        for (var i = 0; i < series.Points.Count; i++)
        {
            result[i] = new PriceObservation(
                series.Points[i].Date, (double)series.Points[i].AdjustedClose);
        }

        return result;
    }
}
