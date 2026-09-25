using System.Diagnostics;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.RiskEngine.Backtesting;
using RiskAnalysis.RiskEngine.Returns;
using RiskAnalysis.RiskEngine.StressTesting;
using RiskAnalysis.RiskEngine.Var;

namespace RiskAnalysis.Infrastructure.Services;

/// <summary>
/// Бэктестирование моделей оценки стоимостной меры риска.
/// </summary>
public class BacktestService : IBacktestService
{
    private readonly IPriceSeriesProvider _priceSeries;

    public BacktestService(IPriceSeriesProvider priceSeries) => _priceSeries = priceSeries;

    /// <inheritdoc />
    public async Task<BacktestReport> RunAsync(
        int instrumentId,
        DateOnly from,
        DateOnly to,
        double confidenceLevel = 0.99,
        int windowSize = 250,
        int scenarioCount = 10_000,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var series = await _priceSeries.GetSeriesAsync(instrumentId, from, to, cancellationToken);

        if (series.Points.Count < windowSize + 60)
        {
            throw new InvalidOperationException(
                $"По инструменту {series.Ticker} имеется {series.Points.Count} котировок. " +
                $"Для бэктестирования при глубине окна {windowSize} требуется не менее " +
                $"{windowSize + 60}. Загрузите историю за более продолжительный период " +
                "либо уменьшите глубину окна.");
        }

        var observations = new PriceObservation[series.Points.Count];

        for (var i = 0; i < series.Points.Count; i++)
        {
            observations[i] = new PriceObservation(
                series.Points[i].Date, (double)series.Points[i].AdjustedClose);
        }

        var returns = ReturnCalculator.Calculate(observations, ReturnType.Logarithmic);

        var methods = new[] { VarMethod.Parametric, VarMethod.Historical, VarMethod.MonteCarlo };
        var results = new List<BacktestResult>(methods.Length);

        foreach (var method in methods)
        {
            cancellationToken.ThrowIfCancellationRequested();

            results.Add(BacktestCalculator.Run(
                returns, method, confidenceLevel, windowSize, scenarioCount));
        }

        stopwatch.Stop();

        return new BacktestReport(
            InstrumentId: series.InstrumentId,
            Ticker: series.Ticker,
            From: returns[0].Date,
            To: returns[^1].Date,
            ConfidenceLevel: confidenceLevel,
            WindowSize: windowSize,
            TotalReturns: returns.Length,
            Results: results,
            Conclusion: BuildConclusion(results, confidenceLevel),
            DurationMs: (int)stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Формирует вывод по результатам сопоставления методов.
    /// </summary>
    private static string BuildConclusion(IReadOnlyList<BacktestResult> results, double confidenceLevel)
    {
        var passed = results
            .Where(r => !r.Kupiec.IsRejected && !r.Christoffersen.ConditionalCoverageRejected)
            .ToList();

        var expectedRate = 1.0 - confidenceLevel;

        // Наиболее пригодной признаётся модель, прошедшая оба критерия
        // и имеющая наименьшее отклонение фактической доли нарушений
        // от ожидаемой.
        var best = (passed.Count > 0 ? passed : results)
            .OrderBy(r => Math.Abs(r.ViolationRate - expectedRate))
            .First();

        var bestName = DescribeMethod(best.Method);

        if (passed.Count == 0)
        {
            return $"При уровне доверия {confidenceLevel:P0} ни одна из проверенных моделей " +
                   "не прошла критерии Купца и Кристоферсена одновременно. Наименьшее " +
                   $"отклонение частоты нарушений от ожидаемой показала модель: {bestName} " +
                   $"({best.ViolationRate:P2} при ожидаемых {expectedRate:P2}).";
        }

        var rejected = results.Count - passed.Count;

        var rejectedNote = rejected > 0
            ? $" Не прошли проверку моделей: {rejected} из {results.Count}."
            : " Все проверенные модели признаны пригодными.";

        return $"При уровне доверия {confidenceLevel:P0} наилучший результат показала модель: " +
               $"{bestName} — фактическая доля нарушений {best.ViolationRate:P2} при ожидаемой " +
               $"{expectedRate:P2}, {DescribeZone(best.Zone)} зона надзорной оценки." + rejectedNote;
    }

    private static string DescribeMethod(VarMethod method) => method switch
    {
        VarMethod.Parametric => "параметрический метод",
        VarMethod.Historical => "метод исторического моделирования",
        VarMethod.MonteCarlo => "метод Монте-Карло",
        _ => "неизвестный метод"
    };

    private static string DescribeZone(BaselZone zone) => zone switch
    {
        BaselZone.Green => "зелёная",
        BaselZone.Yellow => "жёлтая",
        BaselZone.Red => "красная",
        _ => "неопределённая"
    };
}

/// <summary>
/// Стресс-тестирование инвестиционного портфеля.
/// </summary>
public class StressTestService : IStressTestService
{
    private readonly IPortfolioService _portfolios;
    private readonly IPriceSeriesProvider _priceSeries;

    public StressTestService(IPortfolioService portfolios, IPriceSeriesProvider priceSeries)
    {
        _portfolios = portfolios;
        _priceSeries = priceSeries;
    }

    /// <inheritdoc />
    public async Task<StressTestReport> RunAsync(
        int portfolioId,
        DateOnly from,
        DateOnly to,
        double confidenceLevel = 0.99,
        int horizonDays = 10,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var portfolio = await _portfolios.GetAsync(portfolioId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Портфель с идентификатором {portfolioId} не найден.");

        if (portfolio.Positions.Count == 0 || portfolio.CurrentValue <= 0m)
        {
            throw new InvalidOperationException(
                $"Портфель «{portfolio.Name}» не содержит позиций с определённой стоимостью.");
        }

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

        var totalValue = aggregated.Sum(p => p.Value);
        var weights = aggregated.Select(p => (double)(p.Value / totalValue)).ToArray();

        var weightSum = weights.Sum();

        for (var i = 0; i < weights.Length; i++)
        {
            weights[i] /= weightSum;
        }

        var ids = aggregated.Select(p => p.InstrumentId).ToList();
        var series = await _priceSeries.GetAlignedSeriesAsync(ids, from, to, cancellationToken);

        if (series.Any(s => s.Points.Count < 60))
        {
            throw new InvalidOperationException(
                "После приведения рядов к общему торговому календарю осталось недостаточно " +
                "наблюдений для стресс-тестирования.");
        }

        var dates = series[0].Points.Skip(1).Select(point => point.Date).ToList();

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

        var portfolioValue = (double)totalValue;

        // Стоимостная мера риска, с которой сопоставляются сценарии.
        var portfolioRisk = PortfolioRiskCalculator.Calculate(
            weights, returns, confidenceLevel, horizonDays, portfolioValue, scenarioCount: 20_000);

        var valueAtRisk = portfolioRisk.Estimates[1].ValueAtRiskAbsolute;
        var expectedShortfall = portfolioRisk.Estimates[1].ExpectedShortfallAbsolute;

        var scenarios = new List<StressScenarioResult>();

        // Периоды наибольших потерь, определённые по фактическим данным.
        // Продолжительность выбрана сопоставимой с горизонтом оценки риска
        // и с продолжительностью рыночных потрясений.
        scenarios.AddRange(StressTestCalculator.FindWorstPeriods(
            weights, returns, dates, windowDays: horizonDays, count: 3,
            portfolioValue, valueAtRisk));

        scenarios.AddRange(StressTestCalculator.FindWorstPeriods(
            weights, returns, dates, windowDays: 60, count: 2,
            portfolioValue, valueAtRisk));

        // Гипотетические сценарии. Первые два задают равное снижение
        // стоимости всех инструментов, третий — кризисное поведение рынка,
        // при котором взаимосвязи усиливаются и падение затрагивает
        // инструменты неравномерно.
        var count = weights.Length;

        scenarios.Add(StressTestCalculator.ApplyHypothetical(
            "Снижение рынка на 20 %",
            weights, Enumerable.Repeat(-0.20, count).ToArray(),
            portfolioValue, valueAtRisk,
            "Гипотетический сценарий: равномерное снижение стоимости всех инструментов " +
            "портфеля на двадцать процентов."));

        scenarios.Add(StressTestCalculator.ApplyHypothetical(
            "Снижение рынка на 40 %",
            weights, Enumerable.Repeat(-0.40, count).ToArray(),
            portfolioValue, valueAtRisk,
            "Гипотетический сценарий: равномерное снижение стоимости всех инструментов " +
            "портфеля на сорок процентов. Сопоставимо с глубиной падения индекса " +
            "МосБиржи в кризисные периоды."));

        // Сценарий с неравномерным падением: наибольшая по доле позиция
        // теряет больше прочих. Такое поведение характерно для периодов,
        // когда продажи сосредоточены в наиболее ликвидных бумагах.
        var heaviest = Array.IndexOf(weights, weights.Max());

        var unevenShock = Enumerable.Repeat(-0.25, count).ToArray();
        unevenShock[heaviest] = -0.45;

        scenarios.Add(StressTestCalculator.ApplyHypothetical(
            "Неравномерное падение с распродажей ликвидных бумаг",
            weights, unevenShock, portfolioValue, valueAtRisk,
            "Гипотетический сценарий: снижение стоимости инструментов на двадцать пять " +
            "процентов при падении наибольшей по доле позиции на сорок пять процентов. " +
            "Воспроизводит сосредоточение продаж в наиболее ликвидных бумагах."));

        var views = scenarios
            .Select(scenario => new ScenarioView(
                Name: scenario.Name,
                Kind: scenario.Kind,
                From: scenario.From,
                To: scenario.To,
                TradingDays: scenario.TradingDays,
                PortfolioReturn: scenario.PortfolioReturn,
                LossAmount: scenario.LossAmount,
                ValueAfter: scenario.ValueAfter,
                LossToVarRatio: scenario.LossToVarRatio,
                Impacts: scenario.Impacts
                    .Select(impact => new ScenarioPositionImpact(
                        aggregated[impact.Index].InstrumentId,
                        aggregated[impact.Index].Ticker,
                        impact.Weight,
                        impact.InstrumentReturn,
                        impact.Contribution,
                        impact.LossAmount))
                    .OrderBy(impact => impact.LossAmount)
                    .ToList(),
                Description: scenario.Description))
            .OrderBy(scenario => scenario.LossAmount)
            .ToList();

        stopwatch.Stop();

        var worst = views[0];

        var conclusion =
            $"Наибольшие потери даёт сценарий «{worst.Name}»: {-worst.LossAmount:N0} рублей, " +
            $"или {-worst.PortfolioReturn:P1} стоимости портфеля. Это в " +
            $"{worst.LossToVarRatio:F1} раза превышает стоимостную меру риска на горизонте " +
            $"{horizonDays} торг. дн. при уровне доверия {confidenceLevel:P0} " +
            $"({valueAtRisk:N0} рублей). Стоимостная мера риска по построению не охватывает " +
            "события, находящиеся за пределами уровня доверия, и должна дополняться " +
            "стресс-тестированием.";

        return new StressTestReport(
            PortfolioId: portfolio.Id,
            PortfolioName: portfolio.Name,
            PortfolioValue: portfolioValue,
            From: series[0].Points[0].Date,
            To: series[0].Points[^1].Date,
            ConfidenceLevel: confidenceLevel,
            HorizonDays: horizonDays,
            ValueAtRisk: valueAtRisk,
            ExpectedShortfall: expectedShortfall,
            Scenarios: views,
            Conclusion: conclusion,
            DurationMs: (int)stopwatch.ElapsedMilliseconds);
    }
}
