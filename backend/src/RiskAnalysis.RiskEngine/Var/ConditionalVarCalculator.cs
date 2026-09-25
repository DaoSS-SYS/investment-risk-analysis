using MathNet.Numerics.Distributions;
using RiskAnalysis.RiskEngine.Statistics;
using RiskAnalysis.RiskEngine.Volatility;

namespace RiskAnalysis.RiskEngine.Var;

/// <summary>
/// Оценка стоимостной меры риска на основе условной волатильности.
///
/// Бэктестирование методов, опирающихся на безусловную оценку волатильности,
/// показало группирование нарушений: гипотеза об их независимости отвергается
/// в подавляющем большинстве проверенных сочетаний. Причина состоит в том,
/// что выборочное среднеквадратическое отклонение одинаково для спокойных и
/// кризисных периодов и потому запаздывает при изменении рыночных условий.
///
/// Реализованные здесь методы устраняют эту причину, заменяя безусловную
/// оценку волатильности прогнозом, зависящим от недавних наблюдений.
///
/// Метод фильтрованного исторического моделирования заслуживает отдельного
/// пояснения. Он соединяет достоинства двух подходов:
///
/// 1. доходности делятся на соответствующие им условные волатильности, что
///    даёт ряд стандартизованных величин z(t) = r(t) / σ(t). Если модель
///    волатильности описывает данные верно, этот ряд не обладает
///    зависимостью во времени;
/// 2. квантиль оценивается по эмпирическому распределению стандартизованных
///    величин, то есть без предположения о его виде — «тяжёлые хвосты»
///    сохраняются;
/// 3. полученный квантиль умножается на прогноз волатильности на следующий
///    период.
///
/// Тем самым учитываются одновременно изменчивость волатильности и
/// отклонение распределения доходностей от нормального — два свойства,
/// каждое из которых по отдельности приводило к отклонению модели при
/// бэктестировании.
/// </summary>
public static class ConditionalVarCalculator
{
    /// <summary>
    /// Рассчитывает стоимостную меру риска по параметрическому методу
    /// с экспоненциально взвешенной оценкой волатильности.
    /// </summary>
    /// <param name="returns">Ряд логарифмических доходностей.</param>
    /// <param name="confidenceLevel">Уровень доверия.</param>
    /// <param name="horizonDays">Горизонт оценки в торговых днях.</param>
    /// <param name="portfolioValue">Стоимость портфеля.</param>
    /// <param name="lambda">Параметр сглаживания.</param>
    public static VarResult Ewma(
        IReadOnlyList<double> returns,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        double portfolioValue = 1.0,
        double lambda = EwmaCalculator.DefaultLambda)
    {
        ArgumentNullException.ThrowIfNull(returns);

        var volatility = EwmaCalculator.Calculate(returns, lambda);
        var statistics = DescriptiveStatisticsCalculator.Calculate(returns);

        return BuildParametric(
            VarMethod.EwmaParametric,
            statistics.Mean, volatility.Forecast, returns.Count,
            confidenceLevel, horizonDays, portfolioValue,
            $"Параметрический метод с экспоненциально взвешенной оценкой волатильности, " +
            $"λ = {lambda:F2}, уровень доверия {confidenceLevel:P0}, горизонт {horizonDays} " +
            $"торг. дн. Прогноз волатильности {volatility.Forecast:P2} против безусловной " +
            $"оценки {statistics.StandardDeviation:P2}.");
    }

    /// <summary>
    /// Рассчитывает стоимостную меру риска по параметрическому методу
    /// с оценкой волатильности по модели GARCH(1,1).
    /// </summary>
    public static VarResult Garch(
        IReadOnlyList<double> returns,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        double portfolioValue = 1.0)
    {
        ArgumentNullException.ThrowIfNull(returns);

        var volatility = GarchCalculator.Calculate(returns);
        var statistics = DescriptiveStatisticsCalculator.Calculate(returns);

        return BuildParametric(
            VarMethod.GarchParametric,
            statistics.Mean, volatility.Forecast, returns.Count,
            confidenceLevel, horizonDays, portfolioValue,
            $"Параметрический метод с оценкой волатильности по модели GARCH(1,1), " +
            $"α = {volatility.Parameters["alpha"]:F4}, β = {volatility.Parameters["beta"]:F4}, " +
            $"уровень доверия {confidenceLevel:P0}, горизонт {horizonDays} торг. дн. " +
            $"Прогноз волатильности {volatility.Forecast:P2} против безусловной оценки " +
            $"{statistics.StandardDeviation:P2}.");
    }

    /// <summary>
    /// Рассчитывает стоимостную меру риска методом фильтрованного
    /// исторического моделирования.
    /// </summary>
    /// <param name="returns">Ряд логарифмических доходностей.</param>
    /// <param name="confidenceLevel">Уровень доверия.</param>
    /// <param name="horizonDays">Горизонт оценки в торговых днях.</param>
    /// <param name="portfolioValue">Стоимость портфеля.</param>
    /// <param name="lambda">Параметр сглаживания оценки волатильности.</param>
    public static VarResult FilteredHistorical(
        IReadOnlyList<double> returns,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        double portfolioValue = 1.0,
        double lambda = EwmaCalculator.DefaultLambda)
    {
        ArgumentNullException.ThrowIfNull(returns);

        VarConventions.ValidateConfidenceLevel(confidenceLevel);
        VarConventions.ValidateHorizon(horizonDays);
        VarConventions.ValidatePortfolioValue(portfolioValue);

        if (returns.Count < 100)
        {
            throw new ArgumentException(
                "Метод фильтрованного исторического моделирования требует не менее " +
                "ста наблюдений.", nameof(returns));
        }

        var volatility = EwmaCalculator.Calculate(returns, lambda);
        var statistics = DescriptiveStatisticsCalculator.Calculate(returns);

        // Стандартизованные доходности: каждое наблюдение делится
        // на условную волатильность, действовавшую в этот момент.
        // Начальный участок исключается: на нём оценка волатильности
        // определяется выбором начального значения.
        const int skip = 30;

        var standardized = new double[returns.Count - skip];

        for (var t = skip; t < returns.Count; t++)
        {
            var sigma = volatility.ConditionalVolatility[t];

            standardized[t - skip] = sigma > 0
                ? (returns[t] - statistics.Mean) / sigma
                : 0.0;
        }

        var tailProbability = 1.0 - confidenceLevel;

        var quantile = DescriptiveStatisticsCalculator.Quantile(standardized, tailProbability);

        double tailSum = 0.0;
        var tailCount = 0;

        for (var i = 0; i < standardized.Length; i++)
        {
            if (standardized[i] <= quantile)
            {
                tailSum += standardized[i];
                tailCount++;
            }
        }

        var tailMean = tailCount > 0 ? tailSum / tailCount : quantile;

        // Квантиль стандартизованного распределения масштабируется
        // прогнозом волатильности на следующий период.
        var horizonScale = Math.Sqrt(horizonDays);
        var horizonMean = statistics.Mean * horizonDays;
        var scaledVolatility = volatility.Forecast * horizonScale;

        var valueAtRisk = -(horizonMean + quantile * scaledVolatility);
        var expectedShortfall = -(horizonMean + tailMean * scaledVolatility);

        // Для сопоставления: квантиль нормального распределения того же уровня.
        var normalQuantile = Normal.InvCDF(0.0, 1.0, tailProbability);

        return new VarResult(
            Method: VarMethod.FilteredHistorical,
            ConfidenceLevel: confidenceLevel,
            HorizonDays: horizonDays,
            ValueAtRiskRelative: valueAtRisk,
            ExpectedShortfallRelative: expectedShortfall,
            ValueAtRiskAbsolute: valueAtRisk * portfolioValue,
            ExpectedShortfallAbsolute: expectedShortfall * portfolioValue,
            PortfolioValue: portfolioValue,
            ObservationCount: standardized.Length,
            Mean: horizonMean,
            StandardDeviation: scaledVolatility,
            Description:
                $"Фильтрованное историческое моделирование, λ = {lambda:F2}, уровень " +
                $"доверия {confidenceLevel:P0}, горизонт {horizonDays} торг. дн. " +
                $"Квантиль стандартизованных доходностей {quantile:F4} против квантиля " +
                $"нормального распределения {normalQuantile:F4}; прогноз волатильности " +
                $"{volatility.Forecast:P2} против безусловной оценки " +
                $"{statistics.StandardDeviation:P2}.");
    }

    /// <summary>
    /// Формирует результат параметрической оценки при заданном прогнозе
    /// волатильности.
    /// </summary>
    private static VarResult BuildParametric(
        VarMethod method,
        double mean,
        double volatilityForecast,
        int observationCount,
        double confidenceLevel,
        int horizonDays,
        double portfolioValue,
        string description)
    {
        VarConventions.ValidateConfidenceLevel(confidenceLevel);
        VarConventions.ValidateHorizon(horizonDays);
        VarConventions.ValidatePortfolioValue(portfolioValue);

        var horizonMean = mean * horizonDays;
        var horizonDeviation = volatilityForecast * Math.Sqrt(horizonDays);

        var lowerQuantile = Normal.InvCDF(0.0, 1.0, 1.0 - confidenceLevel);
        var upperQuantile = Normal.InvCDF(0.0, 1.0, confidenceLevel);

        var valueAtRisk = -(horizonMean + lowerQuantile * horizonDeviation);

        var tailMultiplier = Normal.PDF(0.0, 1.0, upperQuantile) / (1.0 - confidenceLevel);
        var expectedShortfall = -horizonMean + horizonDeviation * tailMultiplier;

        return new VarResult(
            Method: method,
            ConfidenceLevel: confidenceLevel,
            HorizonDays: horizonDays,
            ValueAtRiskRelative: valueAtRisk,
            ExpectedShortfallRelative: expectedShortfall,
            ValueAtRiskAbsolute: valueAtRisk * portfolioValue,
            ExpectedShortfallAbsolute: expectedShortfall * portfolioValue,
            PortfolioValue: portfolioValue,
            ObservationCount: observationCount,
            Mean: horizonMean,
            StandardDeviation: horizonDeviation,
            Description: description);
    }
}
