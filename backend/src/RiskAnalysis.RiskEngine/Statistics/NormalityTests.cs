using MathNet.Numerics.Distributions;

namespace RiskAnalysis.RiskEngine.Statistics;

/// <summary>
/// Результат проверки гипотезы о нормальности распределения доходностей
/// по критерию Жарка — Бера.
/// </summary>
/// <param name="Statistic">Наблюдаемое значение статистики критерия.</param>
/// <param name="PValue">
/// Достигаемый уровень значимости: вероятность получить значение статистики
/// не меньше наблюдаемого при справедливости гипотезы о нормальности.
/// </param>
/// <param name="CriticalValue">Критическое значение при заданном уровне значимости.</param>
/// <param name="SignificanceLevel">Принятый уровень значимости.</param>
/// <param name="IsNormalityRejected">
/// Результат проверки: истина, если гипотеза о нормальности отвергается.
/// </param>
/// <param name="Skewness">Коэффициент асимметрии, использованный в расчёте.</param>
/// <param name="ExcessKurtosis">Коэффициент эксцесса, использованный в расчёте.</param>
/// <param name="Conclusion">Словесная формулировка вывода.</param>
public sealed record NormalityTestResult(
    double Statistic,
    double PValue,
    double CriticalValue,
    double SignificanceLevel,
    bool IsNormalityRejected,
    double Skewness,
    double ExcessKurtosis,
    string Conclusion);

/// <summary>
/// Проверка гипотезы о нормальности распределения доходностей.
///
/// Проверка имеет определяющее значение для выбора метода оценки риска.
/// Параметрический (дельта-нормальный) метод расчёта стоимостной меры риска
/// исходит из предположения о нормальном распределении доходностей: квантиль
/// распределения выражается через среднеквадратическое отклонение, умноженное
/// на квантиль стандартного нормального распределения. Если предположение
/// не выполняется, параметрическая оценка систематически занижает риск, так как
/// не учитывает «тяжёлые хвосты» фактического распределения.
///
/// Для доходностей финансовых активов предположение о нормальности, как
/// правило, отвергается. Именно этот результат обосновывает применение метода
/// исторического моделирования и метода Монте-Карло, не связанных
/// предположением о виде распределения.
///
/// Критерий Жарка — Бера основан на том, что нормальное распределение обладает
/// нулевой асимметрией и нулевым эксцессом. Статистика критерия
///
///     JB = n / 6 · (S² + K² / 4),
///
/// где n — число наблюдений, S — коэффициент асимметрии, K — коэффициент
/// эксцесса, при справедливости гипотезы о нормальности асимптотически
/// подчиняется распределению хи-квадрат с двумя степенями свободы.
/// Гипотеза отвергается при превышении статистикой критического значения:
/// 5,99 при уровне значимости 0,05 и 9,21 при уровне значимости 0,01.
/// </summary>
public static class NormalityTests
{
    /// <summary>
    /// Выполняет проверку гипотезы о нормальности по критерию Жарка — Бера.
    /// </summary>
    /// <param name="returns">Ряд доходностей.</param>
    /// <param name="significanceLevel">Уровень значимости, по умолчанию 0,05.</param>
    public static NormalityTestResult JarqueBera(
        IReadOnlyList<double> returns,
        double significanceLevel = 0.05)
    {
        ArgumentNullException.ThrowIfNull(returns);

        if (returns.Count < 8)
        {
            throw new ArgumentException(
                "Критерий Жарка — Бера основан на асимптотическом распределении " +
                "статистики и требует не менее восьми наблюдений.",
                nameof(returns));
        }

        if (significanceLevel is <= 0.0 or >= 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(significanceLevel),
                "Уровень значимости должен принадлежать интервалу от нуля до единицы.");
        }

        var statistics = DescriptiveStatisticsCalculator.Calculate(returns);

        return JarqueBera(
            returns.Count,
            statistics.Skewness,
            statistics.ExcessKurtosis,
            significanceLevel);
    }

    /// <summary>
    /// Выполняет проверку по ранее рассчитанным коэффициентам формы.
    /// Перегрузка позволяет не повторять расчёт моментов, если описательные
    /// статистики уже получены.
    /// </summary>
    /// <param name="count">Число наблюдений.</param>
    /// <param name="skewness">Коэффициент асимметрии, оценка по моментам.</param>
    /// <param name="excessKurtosis">Коэффициент эксцесса, оценка по моментам.</param>
    /// <param name="significanceLevel">Уровень значимости.</param>
    public static NormalityTestResult JarqueBera(
        int count,
        double skewness,
        double excessKurtosis,
        double significanceLevel = 0.05)
    {
        var statistic = count / 6.0 *
                        (skewness * skewness + excessKurtosis * excessKurtosis / 4.0);

        // Распределение хи-квадрат с двумя степенями свободы.
        // Для него достигаемый уровень значимости выражается в замкнутом виде:
        // P(X > x) = exp(−x / 2). Расчёт выполняется средствами библиотеки
        // MathNet.Numerics для единообразия с остальными критериями системы.
        var chiSquared = new ChiSquared(2);

        var pValue = 1.0 - chiSquared.CumulativeDistribution(statistic);
        var criticalValue = chiSquared.InverseCumulativeDistribution(1.0 - significanceLevel);

        var isRejected = statistic > criticalValue;

        var conclusion = isRejected
            ? $"Гипотеза о нормальности распределения доходностей отвергается на уровне " +
              $"значимости {significanceLevel:P0}: наблюдаемое значение статистики {statistic:F2} " +
              $"превышает критическое значение {criticalValue:F2}. " +
              "Применение параметрического метода оценки стоимостной меры риска " +
              "приведёт к занижению оценки; требуется применение метода исторического " +
              "моделирования либо метода Монте-Карло."
            : $"Гипотеза о нормальности распределения доходностей не отвергается на уровне " +
              $"значимости {significanceLevel:P0}: наблюдаемое значение статистики {statistic:F2} " +
              $"не превышает критического значения {criticalValue:F2}. " +
              "Применение параметрического метода оценки стоимостной меры риска допустимо.";

        return new NormalityTestResult(
            Statistic: statistic,
            PValue: pValue,
            CriticalValue: criticalValue,
            SignificanceLevel: significanceLevel,
            IsNormalityRejected: isRejected,
            Skewness: skewness,
            ExcessKurtosis: excessKurtosis,
            Conclusion: conclusion);
    }
}
