using System.Diagnostics;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.RiskEngine.Returns;
using RiskAnalysis.RiskEngine.Statistics;
using RiskAnalysis.RiskEngine.Var;

namespace RiskAnalysis.Infrastructure.Services;

/// <summary>
/// Оценка стоимостной меры риска инструмента всеми реализованными методами
/// с сопоставлением полученных результатов.
/// </summary>
public class VarAnalysisService : IVarAnalysisService
{
    private readonly IPriceSeriesProvider _priceSeries;

    public VarAnalysisService(IPriceSeriesProvider priceSeries) => _priceSeries = priceSeries;

    /// <inheritdoc />
    public async Task<VarComparisonResult> CompareMethodsAsync(
        int instrumentId,
        DateOnly from,
        DateOnly to,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        double portfolioValue = 1_000_000.0,
        int scenarioCount = 100_000,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();

        var series = await _priceSeries.GetSeriesAsync(instrumentId, from, to, cancellationToken);

        if (series.Points.Count < 31)
        {
            throw new InvalidOperationException(
                $"По инструменту {series.Ticker} за период с {from:dd.MM.yyyy} по {to:dd.MM.yyyy} " +
                "недостаточно котировок для оценки риска. Выполните загрузку истории.");
        }

        var observations = new PriceObservation[series.Points.Count];

        for (var i = 0; i < series.Points.Count; i++)
        {
            observations[i] = new PriceObservation(
                series.Points[i].Date, (double)series.Points[i].AdjustedClose);
        }

        var returns = ReturnCalculator.Values(
            ReturnCalculator.Calculate(observations, ReturnType.Logarithmic, ReturnFrequency.Daily));

        var statistics = DescriptiveStatisticsCalculator.Calculate(returns);

        var normality = NormalityTests.JarqueBera(
            statistics.Count, statistics.Skewness, statistics.ExcessKurtosis);

        var estimates = new List<VarEstimate>
        {
            Measure("Параметрический (дельта-нормальный)",
                () => ParametricVarCalculator.Calculate(
                    returns, confidenceLevel, horizonDays, portfolioValue)),

            Measure("Историческое моделирование",
                () => HistoricalVarCalculator.Calculate(
                    returns, confidenceLevel, horizonDays, portfolioValue)),

            Measure("Монте-Карло, нормальное распределение",
                () => MonteCarloVarCalculator.Calculate(
                    returns, confidenceLevel, horizonDays, portfolioValue,
                    new MonteCarloOptions(scenarioCount, SimulationDistribution.Normal))),

            Measure("Монте-Карло, распределение Стьюдента",
                () => MonteCarloVarCalculator.Calculate(
                    returns, confidenceLevel, horizonDays, portfolioValue,
                    new MonteCarloOptions(
                        scenarioCount, SimulationDistribution.StudentT,
                        StudentTDegreesOfFreedom: EstimateDegreesOfFreedom(statistics.ExcessKurtosis)))),

            Measure("Монте-Карло, историческая бутстрэп-выборка",
                () => MonteCarloVarCalculator.Calculate(
                    returns, confidenceLevel, horizonDays, portfolioValue,
                    new MonteCarloOptions(scenarioCount, SimulationDistribution.HistoricalBootstrap)))
        };

        totalStopwatch.Stop();

        return new VarComparisonResult(
            InstrumentId: series.InstrumentId,
            Ticker: series.Ticker,
            From: series.Points[0].Date,
            To: series.Points[^1].Date,
            ReturnCount: returns.Length,
            ConfidenceLevel: confidenceLevel,
            HorizonDays: horizonDays,
            PortfolioValue: portfolioValue,
            Statistics: statistics,
            Normality: normality,
            Estimates: estimates,
            Conclusion: BuildConclusion(estimates, normality, confidenceLevel),
            DurationMs: (int)totalStopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Выполняет расчёт с замером длительности. Замеры накапливаются
    /// для последующей оценки производительности системы.
    /// </summary>
    private static VarEstimate Measure(string name, Func<VarResult> calculate)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = calculate();
        stopwatch.Stop();

        return new VarEstimate(name, result, (int)stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Оценивает число степеней свободы распределения Стьюдента по
    /// наблюдаемому коэффициенту эксцесса.
    ///
    /// Для распределения Стьюдента с ν степенями свободы коэффициент эксцесса
    /// равен 6 / (ν − 4) при ν больше четырёх, откуда ν = 4 + 6 / g₂.
    /// Полученное значение ограничивается снизу, поскольку при ν не более двух
    /// дисперсия распределения не определена.
    /// </summary>
    private static double EstimateDegreesOfFreedom(double excessKurtosis)
    {
        if (excessKurtosis <= 0.0)
        {
            return 30.0;
        }

        var degreesOfFreedom = 4.0 + 6.0 / excessKurtosis;

        return Math.Clamp(degreesOfFreedom, 2.5, 30.0);
    }

    /// <summary>
    /// Формирует вывод по результатам сопоставления методов.
    /// </summary>
    private static string BuildConclusion(
        IReadOnlyList<VarEstimate> estimates,
        NormalityTestResult normality,
        double confidenceLevel)
    {
        var parametric = estimates[0].Result;
        var historical = estimates[1].Result;

        var difference = historical.ValueAtRiskRelative - parametric.ValueAtRiskRelative;
        var relative = parametric.ValueAtRiskRelative > 0
            ? difference / parametric.ValueAtRiskRelative
            : 0.0;

        var comparison = difference > 0
            ? $"Оценка методом исторического моделирования превышает параметрическую " +
              $"на {relative:P1}. Расхождение объясняется отличием фактического " +
              "распределения доходностей от нормального: параметрический метод " +
              "не учитывает «тяжёлые хвосты» и занижает оценку риска."
            : $"Параметрическая оценка превышает полученную методом исторического " +
              $"моделирования на {-relative:P1}. Такое соотношение возникает, когда " +
              "отдельные крупные убытки существенно увеличивают среднеквадратическое " +
              "отклонение выборки, но их доля не достигает уровня " +
              $"{1.0 - confidenceLevel:P0}, вследствие чего выборочный квантиль " +
              "остаётся ближе к основной части распределения.";

        var normalityNote = normality.IsNormalityRejected
            ? "Гипотеза о нормальности распределения доходностей отвергнута, поэтому " +
              "предпочтение следует отдать методам, не связанным предположением " +
              "о виде распределения."
            : "Гипотеза о нормальности распределения доходностей не отвергнута, " +
              "применение параметрического метода допустимо.";

        return $"{comparison} {normalityNote}";
    }
}
