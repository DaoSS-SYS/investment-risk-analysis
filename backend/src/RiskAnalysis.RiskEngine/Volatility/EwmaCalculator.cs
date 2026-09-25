using RiskAnalysis.RiskEngine.Statistics;

namespace RiskAnalysis.RiskEngine.Volatility;

/// <summary>
/// Экспоненциально взвешенная оценка волатильности (EWMA).
///
/// Выборочное среднеквадратическое отклонение учитывает все наблюдения с
/// равными весами. Следствием является запаздывание: после рыночного шока
/// оценка растёт медленно, поскольку новое наблюдение составляет лишь малую
/// долю выборки, а после завершения периода повышенной волатильности столь же
/// медленно снижается.
///
/// Бэктестирование, выполненное на предыдущем этапе, показало следствие этого
/// свойства: нарушения группируются во времени, и гипотеза об их
/// независимости отвергается.
///
/// Экспоненциально взвешенная оценка присваивает наблюдениям веса, убывающие
/// в геометрической прогрессии по мере удаления в прошлое:
///
///     σ²(t) = λ · σ²(t−1) + (1 − λ) · r²(t−1).
///
/// Раскрывая рекуррентное соотношение, получаем
///
///     σ²(t) = (1 − λ) · Σ λ^k · r²(t−1−k),
///
/// то есть вес наблюдения, отстоящего на k периодов, равен (1 − λ)·λ^k.
/// Оценка реагирует на изменение рыночных условий немедленно: очередное
/// наблюдение входит в неё с весом 1 − λ.
///
/// Значение параметра сглаживания λ = 0,94 для дневных данных установлено в
/// методике RiskMetrics и получило широкое распространение. Ему соответствует
/// период полураспада веса около одиннадцати торговых дней: вес наблюдения
/// уменьшается вдвое за ln(2) / ln(1/λ) ≈ 11,2 дня.
///
/// Модель не содержит постоянного члена, поэтому не обладает свойством
/// возврата к долгосрочному среднему уровню волатильности: прогноз на любой
/// горизонт равен текущей оценке. Этим она отличается от моделей типа GARCH.
/// </summary>
public static class EwmaCalculator
{
    /// <summary>
    /// Параметр сглаживания для дневных данных по методике RiskMetrics.
    /// </summary>
    public const double DefaultLambda = 0.94;

    /// <summary>
    /// Число начальных наблюдений, по которым определяется начальное значение
    /// оценки. Рекуррентное соотношение требует задания σ²(0); влияние
    /// начального значения убывает как λ^t и после ста наблюдений становится
    /// пренебрежимо малым.
    /// </summary>
    private const int InitializationLength = 30;

    /// <summary>
    /// Рассчитывает ряд экспоненциально взвешенных оценок волатильности.
    /// </summary>
    /// <param name="returns">Ряд логарифмических доходностей.</param>
    /// <param name="lambda">Параметр сглаживания в интервале от нуля до единицы.</param>
    public static VolatilityResult Calculate(
        IReadOnlyList<double> returns,
        double lambda = DefaultLambda)
    {
        ArgumentNullException.ThrowIfNull(returns);

        if (returns.Count < InitializationLength + 10)
        {
            throw new ArgumentException(
                $"Для оценки требуется не менее {InitializationLength + 10} наблюдений.",
                nameof(returns));
        }

        if (lambda is <= 0.0 or >= 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lambda),
                "Параметр сглаживания должен принадлежать интервалу от нуля до единицы.");
        }

        // Начальное значение — дисперсия первых наблюдений ряда.
        double variance = 0.0;

        for (var i = 0; i < InitializationLength; i++)
        {
            variance += returns[i] * returns[i];
        }

        variance /= InitializationLength;

        var conditional = new double[returns.Count];
        double logLikelihood = 0.0;

        for (var t = 0; t < returns.Count; t++)
        {
            conditional[t] = Math.Sqrt(variance);

            // Логарифмическое правдоподобие при нормальном условном
            // распределении. Начальный участок исключается, поскольку
            // на нём оценка определяется выбором начального значения.
            if (t >= InitializationLength && variance > 0)
            {
                logLikelihood -= 0.5 * (Math.Log(2.0 * Math.PI) + Math.Log(variance) +
                                        returns[t] * returns[t] / variance);
            }

            // Обновление оценки по очередному наблюдению.
            variance = lambda * variance + (1.0 - lambda) * returns[t] * returns[t];
        }

        var forecast = Math.Sqrt(variance);

        var statistics = DescriptiveStatisticsCalculator.Calculate(returns);

        var halfLife = Math.Log(2.0) / Math.Log(1.0 / lambda);

        return new VolatilityResult(
            Model: "EWMA",
            ConditionalVolatility: conditional,
            Forecast: forecast,
            UnconditionalVolatility: statistics.StandardDeviation,
            MinimumVolatility: conditional.Skip(InitializationLength).Min(),
            MaximumVolatility: conditional.Max(),
            Parameters: new Dictionary<string, double>
            {
                ["lambda"] = lambda,
                ["halfLifeDays"] = halfLife
            },
            LogLikelihood: logLikelihood,
            Description:
                $"Экспоненциально взвешенная оценка волатильности, параметр сглаживания " +
                $"λ = {lambda:F2}, период полураспада веса {halfLife:F1} торг. дн. " +
                $"Прогноз волатильности на следующий период {forecast:P2} против " +
                $"безусловной оценки {statistics.StandardDeviation:P2}.");
    }
}
