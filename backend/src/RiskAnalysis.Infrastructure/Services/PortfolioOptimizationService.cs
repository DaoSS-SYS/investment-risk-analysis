using System.Diagnostics;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.RiskEngine.Optimization;
using RiskAnalysis.RiskEngine.Returns;

namespace RiskAnalysis.Infrastructure.Services;

/// <summary>
/// Оптимизация структуры инвестиционного портфеля.
///
/// Служба подготавливает исходные данные и передаёт их расчётному ядру:
/// определяет текущие доли позиций, получает приведённые к общему торговому
/// календарю ценовые ряды и безрисковую ставку.
/// </summary>
public class PortfolioOptimizationService : IPortfolioOptimizationService
{
    private readonly IPortfolioService _portfolios;
    private readonly IPriceSeriesProvider _priceSeries;
    private readonly IMacroImportService _macro;

    public PortfolioOptimizationService(
        IPortfolioService portfolios,
        IPriceSeriesProvider priceSeries,
        IMacroImportService macro)
    {
        _portfolios = portfolios;
        _priceSeries = priceSeries;
        _macro = macro;
    }

    /// <inheritdoc />
    public async Task<OptimizationReport> OptimizeAsync(
        int portfolioId,
        DateOnly from,
        DateOnly to,
        double maximumWeight = 1.0,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var portfolio = await _portfolios.GetAsync(portfolioId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Портфель с идентификатором {portfolioId} не найден.");

        var aggregated = portfolio.Positions
            .GroupBy(p => p.InstrumentId)
            .Select(g => new
            {
                InstrumentId = g.Key,
                Ticker = g.First().Ticker,
                Value = g.Sum(p => p.CurrentValue),
            })
            .Where(p => p.Value > 0m)
            .OrderBy(p => p.Ticker)
            .ToList();

        if (aggregated.Count < 2)
        {
            throw new InvalidOperationException(
                $"Портфель «{portfolio.Name}» содержит менее двух инструментов " +
                "с определённой стоимостью. Оптимизация структуры невозможна.");
        }

        var totalValue = aggregated.Sum(p => p.Value);
        var currentWeights = aggregated.Select(p => (double)(p.Value / totalValue)).ToArray();

        var weightSum = currentWeights.Sum();

        for (var i = 0; i < currentWeights.Length; i++)
        {
            currentWeights[i] /= weightSum;
        }

        var ids = aggregated.Select(p => p.InstrumentId).ToList();
        var series = await _priceSeries.GetAlignedSeriesAsync(ids, from, to, cancellationToken);

        var insufficient = series.FirstOrDefault(s => s.Points.Count < 60);

        if (insufficient is not null)
        {
            throw new InvalidOperationException(
                $"По инструменту {insufficient.Ticker} после приведения рядов к общему " +
                "торговому календарю осталось недостаточно наблюдений для оптимизации.");
        }

        var returns = series
            .Select(s =>
            {
                var observations = new PriceObservation[s.Points.Count];

                for (var i = 0; i < s.Points.Count; i++)
                {
                    observations[i] = new PriceObservation(
                        s.Points[i].Date, (double)s.Points[i].AdjustedClose);
                }

                return ReturnCalculator.Values(
                    ReturnCalculator.Calculate(observations, ReturnType.Logarithmic));
            })
            .ToList();

        var riskFree = await _macro.GetRiskFreeRateAsync(from, to, cancellationToken);

        var result = MarkowitzOptimizer.Optimize(
            returns, riskFree.AnnualRate, currentWeights, maximumWeight);

        NamedPortfolio Name(string name, PortfolioPoint point) => new(
            Name: name,
            ExpectedReturn: point.ExpectedReturn,
            Volatility: point.Volatility,
            SharpeRatio: point.SharpeRatio,
            Weights: point.Weights
                .Select((weight, index) => new InstrumentWeight(
                    InstrumentId: aggregated[index].InstrumentId,
                    Ticker: aggregated[index].Ticker,
                    Weight: weight,
                    CurrentWeight: currentWeights[index],
                    Change: weight - currentWeights[index]))
                .OrderByDescending(item => item.Weight)
                .ToList());

        stopwatch.Stop();

        return new OptimizationReport(
            PortfolioId: portfolio.Id,
            PortfolioName: portfolio.Name,
            From: series[0].Points[0].Date,
            To: series[0].Points[^1].Date,
            ObservationCount: result.ObservationCount,
            PortfolioValue: (double)totalValue,
            RiskFreeRateAnnual: riskFree.AnnualRate,
            MaximumWeight: maximumWeight,
            Current: result.CurrentPortfolio is null
                ? null
                : Name("Текущая структура", result.CurrentPortfolio),
            MinimumVariance: Name("Портфель наименьшей дисперсии", result.MinimumVariance),
            MaximumSharpe: Name("Касательный портфель", result.MaximumSharpe),
            EfficientFrontier: result.EfficientFrontier
                .Select(point => new FrontierPoint(
                    point.Volatility, point.ExpectedReturn, point.SharpeRatio))
                .ToList(),
            RandomPortfolios: result.RandomPortfolios
                .Select(point => new FrontierPoint(
                    point.Volatility, point.ExpectedReturn, point.SharpeRatio))
                .ToList(),
            Conclusion: result.Conclusion,
            DurationMs: (int)stopwatch.ElapsedMilliseconds);
    }
}
