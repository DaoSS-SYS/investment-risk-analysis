using MathNet.Numerics.Distributions;

namespace RiskAnalysis.RiskEngine.Statistics;

/// <summary>
/// Интервал гистограммы распределения доходностей.
/// </summary>
/// <param name="LowerBound">Нижняя граница интервала.</param>
/// <param name="UpperBound">Верхняя граница интервала.</param>
/// <param name="Count">Число наблюдений, попавших в интервал.</param>
/// <param name="ObservedFrequency">Доля наблюдений, попавших в интервал.</param>
/// <param name="NormalFrequency">
/// Вероятность попадания в интервал при нормальном распределении с теми же
/// средним и среднеквадратическим отклонением. Сопоставление наблюдаемой и
/// теоретической частот показывает отклонение фактического распределения
/// доходностей от нормального.
/// </param>
public sealed record HistogramBin(
    double LowerBound,
    double UpperBound,
    int Count,
    double ObservedFrequency,
    double NormalFrequency);

/// <summary>
/// Построение гистограммы распределения доходностей с наложением
/// теоретического нормального распределения.
///
/// Гистограмма служит наглядным представлением результата проверки гипотезы о
/// нормальности: расхождение наблюдаемых и теоретических частот в области
/// крайних значений соответствует «тяжёлым хвостам» распределения доходностей
/// финансовых активов.
/// </summary>
public static class HistogramBuilder
{
    /// <summary>
    /// Строит гистограмму распределения.
    /// </summary>
    /// <param name="values">Ряд наблюдений.</param>
    /// <param name="binCount">
    /// Число интервалов. При нулевом значении определяется по правилу
    /// квадратного корня из числа наблюдений и ограничивается сверху.
    /// </param>
    public static HistogramBin[] Build(IReadOnlyList<double> values, int binCount = 0)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count < 2)
        {
            return [];
        }

        if (binCount <= 0)
        {
            binCount = Math.Clamp((int)Math.Ceiling(Math.Sqrt(values.Count)), 10, 60);
        }

        var statistics = DescriptiveStatisticsCalculator.Calculate(values);

        var minimum = statistics.Minimum;
        var maximum = statistics.Maximum;

        if (Math.Abs(maximum - minimum) < double.Epsilon)
        {
            return [];
        }

        var width = (maximum - minimum) / binCount;
        var counts = new int[binCount];

        foreach (var value in values)
        {
            var index = (int)((value - minimum) / width);

            // Максимальное наблюдение попадает на правую границу последнего
            // интервала и должно быть отнесено к нему.
            if (index >= binCount)
            {
                index = binCount - 1;
            }

            counts[index]++;
        }

        var normal = new Normal(statistics.Mean, statistics.StandardDeviation);
        var bins = new HistogramBin[binCount];

        for (var i = 0; i < binCount; i++)
        {
            var lower = minimum + i * width;
            var upper = lower + width;

            bins[i] = new HistogramBin(
                LowerBound: lower,
                UpperBound: upper,
                Count: counts[i],
                ObservedFrequency: (double)counts[i] / values.Count,
                NormalFrequency: normal.CumulativeDistribution(upper) -
                                 normal.CumulativeDistribution(lower));
        }

        return bins;
    }
}
