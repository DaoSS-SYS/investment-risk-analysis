using System.Diagnostics;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.RiskEngine.Returns;
using RiskAnalysis.RiskEngine.Var;

namespace RiskAnalysis.Infrastructure.Services;

/// <summary>
/// Оценка риска инвестиционного портфеля.
///
/// Служба подготавливает исходные данные и передаёт их расчётному ядру:
/// определяет доли позиций по их текущей стоимости, получает ценовые ряды
/// инструментов, приведённые к общему торговому календарю, и рассчитывает
/// по ним доходности.
/// </summary>
public class PortfolioRiskService : IPortfolioRiskService
{
    private readonly IPortfolioService _portfolios;
    private readonly IPriceSeriesProvider _priceSeries;

    public PortfolioRiskService(IPortfolioService portfolios, IPriceSeriesProvider priceSeries)
    {
        _portfolios = portfolios;
        _priceSeries = priceSeries;
    }

    /// <inheritdoc />
    public async Task<PortfolioRiskReport> CalculateAsync(
        int portfolioId,
        RiskCalculationParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var portfolio = await _portfolios.GetAsync(portfolioId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Портфель с идентификатором {portfolioId} не найден.");

        if (portfolio.Positions.Count == 0)
        {
            throw new InvalidOperationException(
                $"Портфель «{portfolio.Name}» не содержит позиций.");
        }

        if (portfolio.CurrentValue <= 0m)
        {
            throw new InvalidOperationException(
                $"Текущая стоимость портфеля «{portfolio.Name}» равна нулю. " +
                "Проверьте наличие котировок по инструментам портфеля.");
        }

        var to = parameters.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = parameters.From ?? to.AddYears(-10);

        // Несколько позиций могут относиться к одному инструменту: они
        // объединяются, поскольку риск определяется совокупным вложением
        // в инструмент, а не способом его приобретения.
        var aggregated = portfolio.Positions
            .GroupBy(p => p.InstrumentId)
            .Select(g => new
            {
                InstrumentId = g.Key,
                Ticker = g.First().Ticker,
                Value = g.Sum(p => p.CurrentValue)
            })
            .Where(p => p.Value > 0m)
            .OrderBy(p => p.Ticker)
            .ToList();

        if (aggregated.Count == 0)
        {
            throw new InvalidOperationException(
                "Ни по одной позиции портфеля не удалось определить текущую стоимость.");
        }

        var totalValue = aggregated.Sum(p => p.Value);
        var weights = aggregated.Select(p => (double)(p.Value / totalValue)).ToArray();

        // Устранение погрешности округления: сумма долей должна быть
        // в точности равна единице.
        var weightSum = weights.Sum();

        for (var i = 0; i < weights.Length; i++)
        {
            weights[i] /= weightSum;
        }

        var ids = aggregated.Select(p => p.InstrumentId).ToList();
        var series = await _priceSeries.GetAlignedSeriesAsync(ids, from, to, cancellationToken);

        var insufficient = series.FirstOrDefault(s => s.Points.Count < 31);

        if (insufficient is not null)
        {
            throw new InvalidOperationException(
                $"По инструменту {insufficient.Ticker} после приведения рядов к общему " +
                "торговому календарю осталось недостаточно наблюдений. Выполните загрузку " +
                "истории котировок по всем инструментам портфеля.");
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

        var portfolioValue = parameters.PortfolioValueOverride ?? (double)totalValue;

        var result = PortfolioRiskCalculator.Calculate(
            weights, returns,
            parameters.ConfidenceLevel, parameters.HorizonDays,
            portfolioValue, parameters.ScenarioCount);

        var contributions = result.Contributions
            .Select(c => new PositionRiskContribution(
                InstrumentId: aggregated[c.Index].InstrumentId,
                Ticker: aggregated[c.Index].Ticker,
                Weight: c.Weight,
                MarginalVar: c.MarginalVar,
                ComponentVar: c.ComponentVar,
                ContributionShare: c.ContributionShare,
                StandaloneVar: c.StandaloneVar,
                DiversificationBenefit: c.DiversificationBenefit))
            .OrderByDescending(c => c.ContributionShare)
            .ToList();

        stopwatch.Stop();

        return new PortfolioRiskReport(
            PortfolioId: portfolio.Id,
            PortfolioName: portfolio.Name,
            From: series[0].Points[0].Date,
            To: series[0].Points[^1].Date,
            ObservationCount: result.ObservationCount,
            PortfolioValue: portfolioValue,
            ConfidenceLevel: parameters.ConfidenceLevel,
            HorizonDays: parameters.HorizonDays,
            PortfolioVolatilityAnnualized: result.PortfolioVolatilityAnnualized,
            WeightedAverageVolatility: result.WeightedAverageVolatility,
            DiversificationEffect: result.DiversificationEffect,
            Estimates: result.Estimates,
            Contributions: contributions,
            SumOfStandaloneVar: result.SumOfStandaloneVar,
            Conclusion: result.Conclusion,
            DurationMs: (int)stopwatch.ElapsedMilliseconds);
    }
}
