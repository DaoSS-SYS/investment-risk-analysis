using MathNet.Numerics.Distributions;
using RiskAnalysis.RiskEngine.Statistics;

namespace RiskAnalysis.RiskEngine.Var;

/// <summary>
/// Параметрический (дельта-нормальный) метод оценки стоимостной меры риска.
///
/// Метод исходит из предположения о нормальном распределении доходностей.
/// При этом предположении квантиль распределения доходности выражается через
/// среднее и среднеквадратическое отклонение:
///
///     q(1−α) = μ + z(1−α) · σ,
///
/// где z(1−α) — квантиль стандартного нормального распределения уровня 1 − α.
/// Стоимостная мера риска определяется как величина потери, то есть берётся
/// с обратным знаком:
///
///     VaR(α) = −(μ + z(1−α) · σ) · V,
///
/// где V — стоимость портфеля.
///
/// Ожидаемые потери при нормальном распределении выражаются в замкнутом виде:
///
///     ES(α) = (−μ + σ · φ(z(α)) / (1 − α)) · V,
///
/// где φ — плотность стандартного нормального распределения.
///
/// Пересчёт на горизонт в h торговых дней выполняется по правилу корня из
/// времени: среднее умножается на h, среднеквадратическое отклонение — на
/// корень из h. Правило вытекает из предположения о независимости доходностей
/// смежных периодов и предусмотрено требованиями Базельского комитета.
///
/// Ограничение метода. Проверка по критерию Жарка — Бера показывает, что
/// предположение о нормальности для доходностей акций не выполняется:
/// фактическое распределение обладает отрицательной асимметрией и
/// положительным эксцессом. Вследствие этого параметрическая оценка
/// систематически занижает риск, причём занижение возрастает с уровнем
/// доверия.
/// </summary>
public static class ParametricVarCalculator
{
    /// <summary>
    /// Рассчитывает стоимостную меру риска по выборке доходностей.
    /// </summary>
    /// <param name="returns">Ряд доходностей за один период.</param>
    /// <param name="confidenceLevel">Уровень доверия, например 0,99.</param>
    /// <param name="horizonDays">Горизонт оценки в торговых днях.</param>
    /// <param name="portfolioValue">Стоимость портфеля.</param>
    public static VarResult Calculate(
        IReadOnlyList<double> returns,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        double portfolioValue = 1.0)
    {
        ArgumentNullException.ThrowIfNull(returns);

        if (returns.Count < 2)
        {
            throw new ArgumentException(
                "Для оценки требуется не менее двух наблюдений доходности.",
                nameof(returns));
        }

        var statistics = DescriptiveStatisticsCalculator.Calculate(returns);

        return Calculate(
            statistics.Mean,
            statistics.StandardDeviation,
            returns.Count,
            confidenceLevel,
            horizonDays,
            portfolioValue);
    }

    /// <summary>
    /// Рассчитывает стоимостную меру риска по заданным параметрам
    /// распределения. Перегрузка применяется при оценке риска портфеля, когда
    /// среднее и среднеквадратическое отклонение получены из ковариационной
    /// матрицы, а не из ряда наблюдений.
    /// </summary>
    /// <param name="mean">Средняя доходность за один период.</param>
    /// <param name="standardDeviation">Среднеквадратическое отклонение за один период.</param>
    /// <param name="observationCount">Число наблюдений, по которым получены оценки.</param>
    /// <param name="confidenceLevel">Уровень доверия.</param>
    /// <param name="horizonDays">Горизонт оценки в торговых днях.</param>
    /// <param name="portfolioValue">Стоимость портфеля.</param>
    public static VarResult Calculate(
        double mean,
        double standardDeviation,
        int observationCount,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        double portfolioValue = 1.0)
    {
        VarConventions.ValidateConfidenceLevel(confidenceLevel);
        VarConventions.ValidateHorizon(horizonDays);
        VarConventions.ValidatePortfolioValue(portfolioValue);

        if (standardDeviation < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(standardDeviation),
                "Среднеквадратическое отклонение не может быть отрицательным.");
        }

        // Правило корня из времени.
        var horizonMean = mean * horizonDays;
        var horizonDeviation = standardDeviation * Math.Sqrt(horizonDays);

        // Квантиль стандартного нормального распределения уровня 1 − α.
        // Для уровня доверия 0,99 равен −2,3263.
        var lowerQuantile = Normal.InvCDF(0.0, 1.0, 1.0 - confidenceLevel);

        var valueAtRisk = -(horizonMean + lowerQuantile * horizonDeviation);

        // Ожидаемые потери при нормальном распределении.
        // Множитель φ(z(α)) / (1 − α) для уровня доверия 0,99 равен 2,6652,
        // тогда как квантиль равен 2,3263: ожидаемые потери всегда превышают
        // стоимостную меру риска.
        var upperQuantile = Normal.InvCDF(0.0, 1.0, confidenceLevel);
        var tailMultiplier = Normal.PDF(0.0, 1.0, upperQuantile) / (1.0 - confidenceLevel);

        var expectedShortfall = -horizonMean + horizonDeviation * tailMultiplier;

        return new VarResult(
            Method: VarMethod.Parametric,
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
            Description:
                $"Параметрический метод, уровень доверия {confidenceLevel:P0}, горизонт " +
                $"{horizonDays} торг. дн. Квантиль стандартного нормального распределения " +
                $"{lowerQuantile:F4}. С вероятностью {confidenceLevel:P0} потери за указанный " +
                $"период не превысят {valueAtRisk:P2} стоимости портфеля. Метод исходит из " +
                "предположения о нормальном распределении доходностей.");
    }
}
