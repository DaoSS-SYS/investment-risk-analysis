using RiskAnalysis.RiskEngine.Statistics;

namespace RiskAnalysis.RiskEngine.Var;

/// <summary>
/// Результат оценки риска портфеля.
/// </summary>
/// <param name="PortfolioValue">Стоимость портфеля.</param>
/// <param name="ConfidenceLevel">Уровень доверия.</param>
/// <param name="HorizonDays">Горизонт оценки в торговых днях.</param>
/// <param name="ObservationCount">Число наблюдений общего торгового календаря.</param>
/// <param name="PortfolioVolatilityAnnualized">Волатильность портфеля в годовом выражении.</param>
/// <param name="WeightedAverageVolatility">
/// Средневзвешенная волатильность инструментов — значение, которое имел бы
/// портфель при полной положительной корреляции составляющих.
/// </param>
/// <param name="DiversificationEffect">Относительное снижение риска за счёт диверсификации.</param>
/// <param name="Estimates">Оценки риска, полученные различными методами.</param>
/// <param name="Contributions">Разложение риска по инструментам.</param>
/// <param name="SumOfStandaloneVar">
/// Сумма обособленных мер риска позиций — величина, которую имел бы портфель
/// при полной положительной корреляции.
/// </param>
/// <param name="Conclusion">Вывод по результатам расчёта.</param>
public sealed record PortfolioRiskResult(
    double PortfolioValue,
    double ConfidenceLevel,
    int HorizonDays,
    int ObservationCount,
    double PortfolioVolatilityAnnualized,
    double WeightedAverageVolatility,
    double DiversificationEffect,
    IReadOnlyList<VarResult> Estimates,
    IReadOnlyList<RiskContribution> Contributions,
    double SumOfStandaloneVar,
    string Conclusion);

/// <summary>
/// Оценка риска портфеля в целом.
///
/// Риск портфеля не сводится к сумме рисков его составляющих: вследствие
/// неполной корреляции инструментов он оказывается меньше. Количественно
/// это выражается квадратичной формой
///
///     σ²(портфеля) = wᵀ · Σ · w,
///
/// в которой недиагональные элементы ковариационной матрицы уменьшают
/// результат тем сильнее, чем слабее связаны инструменты.
///
/// Построение ряда доходностей портфеля. Для применения метода исторического
/// моделирования требуется ряд доходностей портфеля. Он восстанавливается по
/// рядам доходностей инструментов при фиксированных долях:
///
///     r(p,t) = Σ w(i) · (exp(r(i,t)) − 1).
///
/// Переход к простым доходностям необходим, поскольку только они аддитивны
/// по составу портфеля. Постоянство долей соответствует предположению
/// о ежедневном приведении структуры портфеля к заданной: доли не меняются
/// вследствие различий в динамике инструментов.
/// </summary>
public static class PortfolioRiskCalculator
{
    /// <summary>
    /// Рассчитывает риск портфеля всеми реализованными методами
    /// и выполняет разложение риска по инструментам.
    /// </summary>
    /// <param name="weights">
    /// Вектор долей инструментов в портфеле. Сумма долей должна быть равна
    /// единице с точностью до погрешности округления.
    /// </param>
    /// <param name="returns">
    /// Матрица логарифмических доходностей: первый индекс — инструмент,
    /// второй — наблюдение. Ряды должны быть приведены к общему торговому
    /// календарю.
    /// </param>
    /// <param name="confidenceLevel">Уровень доверия.</param>
    /// <param name="horizonDays">Горизонт оценки в торговых днях.</param>
    /// <param name="portfolioValue">Стоимость портфеля.</param>
    /// <param name="scenarioCount">Число сценариев метода Монте-Карло.</param>
    public static PortfolioRiskResult Calculate(
        IReadOnlyList<double> weights,
        IReadOnlyList<double[]> returns,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        double portfolioValue = 1_000_000.0,
        int scenarioCount = 100_000)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(returns);

        VarConventions.ValidateConfidenceLevel(confidenceLevel);
        VarConventions.ValidateHorizon(horizonDays);
        VarConventions.ValidatePortfolioValue(portfolioValue);

        if (weights.Count != returns.Count)
        {
            throw new ArgumentException(
                "Число долей не соответствует числу рядов доходностей.", nameof(weights));
        }

        if (weights.Count == 0)
        {
            throw new ArgumentException("Портфель не содержит инструментов.", nameof(weights));
        }

        var weightSum = weights.Sum();

        if (Math.Abs(weightSum - 1.0) > 1e-6)
        {
            throw new ArgumentException(
                $"Сумма долей инструментов равна {weightSum:F6} вместо единицы.",
                nameof(weights));
        }

        var observationCount = returns[0].Length;

        for (var i = 1; i < returns.Count; i++)
        {
            if (returns[i].Length != observationCount)
            {
                throw new ArgumentException(
                    "Ряды доходностей имеют различную длину. Перед расчётом ряды должны " +
                    "быть приведены к общему торговому календарю.", nameof(returns));
            }
        }

        var means = CovarianceCalculator.Means(returns);
        var covariance = CovarianceCalculator.Covariance(returns);

        var portfolioDeviation = CovarianceCalculator.PortfolioStandardDeviation(weights, covariance);

        // Простые доходности инструментов по наблюдениям: требуются как для
        // восстановления ряда доходностей портфеля, так и для разложения
        // ожидаемых потерь.
        var simpleReturns = new double[returns.Count][];

        for (var i = 0; i < returns.Count; i++)
        {
            simpleReturns[i] = new double[observationCount];

            for (var t = 0; t < observationCount; t++)
            {
                simpleReturns[i][t] = Math.Exp(returns[i][t]) - 1.0;
            }
        }

        var portfolioReturns = new double[observationCount];

        for (var t = 0; t < observationCount; t++)
        {
            double value = 0.0;

            for (var i = 0; i < returns.Count; i++)
            {
                value += weights[i] * simpleReturns[i][t];
            }

            portfolioReturns[t] = value;
        }

        // Средняя доходность портфеля для параметрической оценки берётся
        // как взвешенная сумма средних логарифмических доходностей
        // инструментов — согласованно с ковариационной матрицей, по которой
        // рассчитано среднеквадратическое отклонение, и с разложением риска
        // по инструментам. Использование среднего по ряду простых доходностей
        // портфеля нарушило бы тождество Эйлера: сумма компонентных мер
        // перестала бы в точности совпадать с мерой риска портфеля.
        double portfolioMean = 0.0;

        for (var i = 0; i < weights.Count; i++)
        {
            portfolioMean += weights[i] * means[i];
        }

        var estimates = new List<VarResult>
        {
            ParametricVarCalculator.Calculate(
                portfolioMean, portfolioDeviation, observationCount,
                confidenceLevel, horizonDays, portfolioValue),

            HistoricalVarCalculator.Calculate(
                portfolioReturns, confidenceLevel, horizonDays, portfolioValue),

            MonteCarloVarCalculator.CalculatePortfolio(
                weights, returns, confidenceLevel, horizonDays, portfolioValue,
                new MonteCarloOptions(scenarioCount, SimulationDistribution.Normal)),

            MonteCarloVarCalculator.CalculatePortfolio(
                weights, returns, confidenceLevel, horizonDays, portfolioValue,
                new MonteCarloOptions(scenarioCount, SimulationDistribution.HistoricalBootstrap))
        };

        var contributions = ComponentVarCalculator.Decompose(
            weights, means, covariance, confidenceLevel, horizonDays, portfolioValue);

        var annualizationFactor = Math.Sqrt(Returns.ReturnCalculator.TradingDaysPerYear);

        var weightedAverageVolatility = 0.0;

        for (var i = 0; i < weights.Count; i++)
        {
            weightedAverageVolatility += weights[i] * Math.Sqrt(covariance[i, i]);
        }

        var portfolioVolatilityAnnualized = portfolioDeviation * annualizationFactor;
        var weightedAverageAnnualized = weightedAverageVolatility * annualizationFactor;

        var diversificationEffect = weightedAverageAnnualized > 0
            ? 1.0 - portfolioVolatilityAnnualized / weightedAverageAnnualized
            : 0.0;

        var sumOfStandalone = contributions.Sum(c => c.StandaloneVar);
        var portfolioVar = estimates[0].ValueAtRiskAbsolute;

        // Проверка тождества Эйлера: сумма компонентных мер риска обязана
        // совпадать с параметрической мерой риска портфеля. Нарушение
        // означало бы рассогласование исходных данных расчёта.
        var componentSum = contributions.Sum(c => c.ComponentVar);

        if (Math.Abs(componentSum - portfolioVar) > Math.Abs(portfolioVar) * 1e-9 + 1e-6)
        {
            throw new InvalidOperationException(
                $"Нарушено тождество Эйлера: сумма компонентных мер риска {componentSum:F6} " +
                $"не совпадает с мерой риска портфеля {portfolioVar:F6}.");
        }

        var largest = contributions.OrderByDescending(c => c.ContributionShare).First();

        var conclusion =
            $"Стоимостная мера риска портфеля при уровне доверия {confidenceLevel:P0} " +
            $"и горизонте {horizonDays} торг. дн. составляет {portfolioVar:N0} " +
            $"против {sumOfStandalone:N0} при полной положительной корреляции позиций: " +
            $"диверсификация снижает риск на {1.0 - portfolioVar / sumOfStandalone:P1}. " +
            $"Наибольший вклад в риск портфеля вносит позиция с долей " +
            $"{largest.Weight:P1}, на которую приходится {largest.ContributionShare:P1} " +
            "общего риска.";

        return new PortfolioRiskResult(
            PortfolioValue: portfolioValue,
            ConfidenceLevel: confidenceLevel,
            HorizonDays: horizonDays,
            ObservationCount: observationCount,
            PortfolioVolatilityAnnualized: portfolioVolatilityAnnualized,
            WeightedAverageVolatility: weightedAverageAnnualized,
            DiversificationEffect: diversificationEffect,
            Estimates: estimates,
            Contributions: contributions,
            SumOfStandaloneVar: sumOfStandalone,
            Conclusion: conclusion);
    }
}
