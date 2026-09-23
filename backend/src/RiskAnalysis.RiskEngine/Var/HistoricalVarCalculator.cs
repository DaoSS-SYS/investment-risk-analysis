using RiskAnalysis.RiskEngine.Statistics;

namespace RiskAnalysis.RiskEngine.Var;

/// <summary>
/// Метод исторического моделирования стоимостной меры риска.
///
/// Метод не связан предположением о виде распределения доходностей: оценкой
/// квантиля распределения служит соответствующий выборочный квантиль
/// фактически наблюдавшихся доходностей.
///
///     VaR(α) = −q(1−α) · V,
///
/// где q(1−α) — выборочный квантиль уровня 1 − α ряда доходностей.
///
/// Ожидаемые потери рассчитываются как среднее значение доходностей,
/// не превышающих квантиль, взятое с обратным знаком. Тем самым учитывается
/// величина потерь в «хвосте» распределения, а не только его граница.
///
/// Достоинство метода состоит в том, что асимметрия и «тяжёлые хвосты»
/// фактического распределения учитываются непосредственно, без введения
/// дополнительных предположений.
///
/// Ограничения метода:
///
/// — оценка полностью определяется наблюдавшимся периодом: события, которых
///   в выборке не было, в оценке не отражаются;
/// — все наблюдения учитываются с равными весами, вследствие чего оценка
///   медленно реагирует на изменение рыночных условий;
/// — точность оценки в области высоких уровней доверия ограничена объёмом
///   выборки: при 2500 наблюдениях уровню доверия 0,99 соответствуют всего
///   25 наблюдений в «хвосте».
/// </summary>
public static class HistoricalVarCalculator
{
    /// <summary>
    /// Рассчитывает стоимостную меру риска методом исторического моделирования.
    /// </summary>
    /// <param name="returns">Ряд доходностей за один период.</param>
    /// <param name="confidenceLevel">Уровень доверия.</param>
    /// <param name="horizonDays">
    /// Горизонт оценки в торговых днях. Пересчёт выполняется по правилу корня
    /// из времени, что соответствует требованиям Базельского комитета.
    /// </param>
    /// <param name="portfolioValue">Стоимость портфеля.</param>
    public static VarResult Calculate(
        IReadOnlyList<double> returns,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        double portfolioValue = 1.0)
    {
        ArgumentNullException.ThrowIfNull(returns);

        VarConventions.ValidateConfidenceLevel(confidenceLevel);
        VarConventions.ValidateHorizon(horizonDays);
        VarConventions.ValidatePortfolioValue(portfolioValue);

        if (returns.Count < 30)
        {
            throw new ArgumentException(
                "Метод исторического моделирования требует достаточного объёма выборки: " +
                "не менее тридцати наблюдений доходности.",
                nameof(returns));
        }

        var tailProbability = 1.0 - confidenceLevel;

        // Выборочный квантиль уровня 1 − α. Для уровня доверия 0,99
        // это первый процентиль ряда доходностей.
        var quantile = DescriptiveStatisticsCalculator.Quantile(returns, tailProbability);

        // Ожидаемые потери: среднее по наблюдениям, не превышающим квантиль.
        double tailSum = 0.0;
        var tailCount = 0;

        for (var i = 0; i < returns.Count; i++)
        {
            if (returns[i] <= quantile)
            {
                tailSum += returns[i];
                tailCount++;
            }
        }

        // Если квантиль лежит строго ниже всех наблюдений «хвоста»,
        // ожидаемые потери принимаются равными стоимостной мере риска.
        var tailMean = tailCount > 0 ? tailSum / tailCount : quantile;

        var statistics = DescriptiveStatisticsCalculator.Calculate(returns);

        var scale = Math.Sqrt(horizonDays);

        var valueAtRisk = -quantile * scale;
        var expectedShortfall = -tailMean * scale;

        return new VarResult(
            Method: VarMethod.Historical,
            ConfidenceLevel: confidenceLevel,
            HorizonDays: horizonDays,
            ValueAtRiskRelative: valueAtRisk,
            ExpectedShortfallRelative: expectedShortfall,
            ValueAtRiskAbsolute: valueAtRisk * portfolioValue,
            ExpectedShortfallAbsolute: expectedShortfall * portfolioValue,
            PortfolioValue: portfolioValue,
            ObservationCount: returns.Count,
            Mean: statistics.Mean * horizonDays,
            StandardDeviation: statistics.StandardDeviation * scale,
            Description:
                $"Метод исторического моделирования, уровень доверия {confidenceLevel:P0}, " +
                $"горизонт {horizonDays} торг. дн. Оценка построена по выборке из " +
                $"{returns.Count} наблюдений, в «хвосте» распределения — {tailCount} " +
                $"наблюдений. Выборочный квантиль уровня {tailProbability:P0} составляет " +
                $"{quantile:P2}. Предположения о виде распределения не вводятся.");
    }
}
