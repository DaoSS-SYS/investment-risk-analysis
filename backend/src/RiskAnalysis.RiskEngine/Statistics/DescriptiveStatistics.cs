namespace RiskAnalysis.RiskEngine.Statistics;

/// <summary>
/// Описательные статистики ряда доходностей.
/// </summary>
/// <param name="Count">Число наблюдений.</param>
/// <param name="Mean">Среднее арифметическое доходности за период.</param>
/// <param name="Variance">Выборочная дисперсия (несмещённая оценка, делитель n − 1).</param>
/// <param name="StandardDeviation">Выборочное среднеквадратическое отклонение.</param>
/// <param name="Skewness">
/// Коэффициент асимметрии, оценка по моментам (g1). Отрицательное значение
/// означает преобладание крупных отрицательных доходностей над положительными.
/// </param>
/// <param name="ExcessKurtosis">
/// Коэффициент эксцесса, оценка по моментам (g2), за вычетом трёх. Для
/// нормального распределения равен нулю; положительное значение означает
/// «тяжёлые хвосты» — более высокую, чем у нормального распределения,
/// вероятность крупных отклонений.
/// </param>
/// <param name="SkewnessAdjusted">
/// Коэффициент асимметрии, скорректированный на объём выборки (G1).
/// Соответствует функции СКОС табличного процессора Microsoft Excel.
/// </param>
/// <param name="ExcessKurtosisAdjusted">
/// Коэффициент эксцесса, скорректированный на объём выборки (G2).
/// Соответствует функции ЭКСЦЕСС табличного процессора Microsoft Excel.
/// </param>
/// <param name="Minimum">Минимальное наблюдение.</param>
/// <param name="Maximum">Максимальное наблюдение.</param>
/// <param name="Median">Медиана.</param>
/// <param name="AnnualizedMean">Средняя доходность в годовом выражении.</param>
/// <param name="AnnualizedVolatility">Волатильность в годовом выражении.</param>
public sealed record DescriptiveStatisticsResult(
    int Count,
    double Mean,
    double Variance,
    double StandardDeviation,
    double Skewness,
    double ExcessKurtosis,
    double SkewnessAdjusted,
    double ExcessKurtosisAdjusted,
    double Minimum,
    double Maximum,
    double Median,
    double AnnualizedMean,
    double AnnualizedVolatility);

/// <summary>
/// Расчёт описательных статистик ряда доходностей.
///
/// О двух оценках асимметрии и эксцесса. Коэффициенты рассчитываются двумя
/// способами, различающимися поправкой на объём выборки:
///
/// — оценки по моментам g1 и g2 используют выборочные центральные моменты с
///   делителем n. Именно эти оценки входят в статистику критерия Жарка — Бера,
///   поэтому их расчёт обязателен;
///
/// — скорректированные оценки G1 и G2 содержат поправку на смещение и
///   совпадают с результатом функций СКОС и ЭКСЦЕСС табличного процессора
///   Microsoft Excel.
///
/// Обе величины возвращаются одновременно: расхождение между ними при малом
/// объёме выборки существенно, и при проверке расчётов в табличном процессоре
/// сопоставлять следует именно скорректированные оценки.
/// </summary>
public static class DescriptiveStatisticsCalculator
{
    /// <summary>
    /// Рассчитывает описательные статистики ряда доходностей.
    /// </summary>
    /// <param name="returns">Ряд доходностей.</param>
    /// <param name="periodsPerYear">
    /// Число периодов в году для пересчёта показателей в годовое выражение.
    /// </param>
    public static DescriptiveStatisticsResult Calculate(
        IReadOnlyList<double> returns,
        int periodsPerYear = 252)
    {
        ArgumentNullException.ThrowIfNull(returns);

        var n = returns.Count;

        if (n < 2)
        {
            throw new ArgumentException(
                "Для расчёта описательных статистик требуется не менее двух наблюдений.",
                nameof(returns));
        }

        var mean = 0.0;

        for (var i = 0; i < n; i++)
        {
            mean += returns[i];
        }

        mean /= n;

        // Центральные моменты второго, третьего и четвёртого порядков.
        double m2 = 0.0, m3 = 0.0, m4 = 0.0;
        var minimum = double.MaxValue;
        var maximum = double.MinValue;

        for (var i = 0; i < n; i++)
        {
            var deviation = returns[i] - mean;
            var squared = deviation * deviation;

            m2 += squared;
            m3 += squared * deviation;
            m4 += squared * squared;

            if (returns[i] < minimum)
            {
                minimum = returns[i];
            }

            if (returns[i] > maximum)
            {
                maximum = returns[i];
            }
        }

        // Несмещённая оценка дисперсии применяется как показатель разброса;
        // моменты для коэффициентов формы рассчитываются с делителем n.
        var variance = m2 / (n - 1);
        var standardDeviation = Math.Sqrt(variance);

        m2 /= n;
        m3 /= n;
        m4 /= n;

        double skewness = 0.0, excessKurtosis = 0.0;

        if (m2 > 0)
        {
            skewness = m3 / Math.Pow(m2, 1.5);
            excessKurtosis = m4 / (m2 * m2) - 3.0;
        }

        // Поправка на объём выборки. Требует не менее четырёх наблюдений
        // для коэффициента эксцесса.
        var skewnessAdjusted = n > 2
            ? skewness * Math.Sqrt(n * (n - 1.0)) / (n - 2.0)
            : skewness;

        var excessKurtosisAdjusted = n > 3
            ? (n - 1.0) / ((n - 2.0) * (n - 3.0)) * ((n + 1.0) * excessKurtosis + 6.0)
            : excessKurtosis;

        return new DescriptiveStatisticsResult(
            Count: n,
            Mean: mean,
            Variance: variance,
            StandardDeviation: standardDeviation,
            Skewness: skewness,
            ExcessKurtosis: excessKurtosis,
            SkewnessAdjusted: skewnessAdjusted,
            ExcessKurtosisAdjusted: excessKurtosisAdjusted,
            Minimum: minimum,
            Maximum: maximum,
            Median: Median(returns),

            // Средняя доходность пересчитывается в годовое выражение
            // умножением на число периодов, волатильность — умножением
            // на корень из числа периодов (правило корня из времени).
            AnnualizedMean: mean * periodsPerYear,
            AnnualizedVolatility: standardDeviation * Math.Sqrt(periodsPerYear));
    }

    /// <summary>Рассчитывает медиану ряда.</summary>
    public static double Median(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
        {
            throw new ArgumentException("Ряд не содержит наблюдений.", nameof(values));
        }

        var sorted = values.ToArray();
        Array.Sort(sorted);

        var middle = sorted.Length / 2;

        return sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }

    /// <summary>
    /// Рассчитывает выборочный квантиль уровня <paramref name="level"/>
    /// методом линейной интерполяции между порядковыми статистиками.
    ///
    /// Метод соответствует функции ПРОЦЕНТИЛЬ табличного процессора Microsoft
    /// Excel и применяется далее при расчёте стоимостной меры риска методом
    /// исторического моделирования.
    /// </summary>
    /// <param name="values">Ряд наблюдений.</param>
    /// <param name="level">Уровень квантиля в интервале от нуля до единицы.</param>
    public static double Quantile(IReadOnlyList<double> values, double level)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
        {
            throw new ArgumentException("Ряд не содержит наблюдений.", nameof(values));
        }

        if (level is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(level), "Уровень квантиля должен принадлежать отрезку от нуля до единицы.");
        }

        var sorted = values.ToArray();
        Array.Sort(sorted);

        if (sorted.Length == 1)
        {
            return sorted[0];
        }

        var position = level * (sorted.Length - 1);
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);

        if (lowerIndex == upperIndex)
        {
            return sorted[lowerIndex];
        }

        var weight = position - lowerIndex;

        return sorted[lowerIndex] * (1.0 - weight) + sorted[upperIndex] * weight;
    }
}
