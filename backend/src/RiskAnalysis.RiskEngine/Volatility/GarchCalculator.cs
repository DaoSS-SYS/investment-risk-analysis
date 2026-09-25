using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
using RiskAnalysis.RiskEngine.Statistics;

namespace RiskAnalysis.RiskEngine.Volatility;

/// <summary>
/// Обобщённая авторегрессионная модель условной гетероскедастичности
/// GARCH(1,1).
///
/// Модель описывает изменение условной дисперсии доходности соотношением
///
///     σ²(t) = ω + α · r²(t−1) + β · σ²(t−1),
///
/// где ω — постоянная составляющая, α — коэффициент при квадрате предыдущей
/// доходности, β — коэффициент при предыдущей оценке дисперсии.
///
/// Отличие от экспоненциально взвешенной оценки состоит в наличии постоянной
/// составляющей ω. Благодаря ей модель обладает свойством возврата к
/// долгосрочному уровню волатильности
///
///     σ²(∞) = ω / (1 − α − β),
///
/// определённому при α + β &lt; 1. После рыночного шока оценка волатильности
/// возрастает, а затем постепенно возвращается к долгосрочному уровню, тогда
/// как экспоненциально взвешенная оценка такого свойства не имеет и даёт на
/// любом горизонте прогноз, равный текущему значению.
///
/// Сумма α + β определяет скорость возврата: чем ближе она к единице, тем
/// устойчивее периоды повышенной волатильности. Для доходностей акций
/// характерны значения от 0,95 до 0,99.
///
/// Оценивание параметров. Параметры оцениваются методом максимального
/// правдоподобия. При предположении об условной нормальности доходностей
/// логарифмическая функция правдоподобия имеет вид
///
///     lnL = −0,5 · Σ [ ln(2π) + ln σ²(t) + r²(t) / σ²(t) ].
///
/// Максимизация выполняется численно методом деформируемого многогранника
/// Нелдера — Мида.
///
/// Параметры модели подчинены ограничениям ω &gt; 0, α ≥ 0, β ≥ 0, α + β &lt; 1.
/// Применяемый метод оптимизации не поддерживает ограничений, поэтому
/// используется замена переменных, при которой ограничения выполняются
/// тождественно: ω = exp(p₀), α = s(p₁) · c, β = s(p₂) · (c − α), где
/// s — логистическая функция, c — предел суммы коэффициентов. Оптимизация
/// ведётся по неограниченным величинам p₀, p₁, p₂.
/// </summary>
public static class GarchCalculator
{
    /// <summary>
    /// Предел суммы коэффициентов α + β. Значение, строго меньшее единицы,
    /// обеспечивает существование долгосрочного уровня волатильности.
    /// </summary>
    private const double PersistenceLimit = 0.999;

    /// <summary>Максимальное число итераций численной оптимизации.</summary>
    private const int MaximumIterations = 3000;

    /// <summary>
    /// Оценивает параметры модели GARCH(1,1) методом максимального
    /// правдоподобия и рассчитывает ряд условных волатильностей.
    /// </summary>
    /// <param name="returns">Ряд логарифмических доходностей.</param>
    public static VolatilityResult Calculate(IReadOnlyList<double> returns)
    {
        ArgumentNullException.ThrowIfNull(returns);

        if (returns.Count < 100)
        {
            throw new ArgumentException(
                "Для оценивания параметров модели GARCH требуется не менее ста наблюдений.",
                nameof(returns));
        }

        var statistics = DescriptiveStatisticsCalculator.Calculate(returns);

        // Доходности центрируются: модель описывает условную дисперсию
        // относительно среднего значения.
        var centered = new double[returns.Count];

        for (var i = 0; i < returns.Count; i++)
        {
            centered[i] = returns[i] - statistics.Mean;
        }

        var sampleVariance = statistics.Variance;

        // Начальное приближение соответствует типичным для доходностей акций
        // значениям: α = 0,10, β = 0,85, долгосрочная дисперсия равна
        // выборочной.
        const double initialAlpha = 0.10;
        const double initialBeta = 0.85;

        var initialOmega = sampleVariance * (1.0 - initialAlpha - initialBeta);

        var initialGuess = Vector<double>.Build.DenseOfArray(
        [
            Math.Log(Math.Max(initialOmega, 1e-12)),
            InverseLogistic(initialAlpha / PersistenceLimit),
            InverseLogistic(initialBeta / (PersistenceLimit - initialAlpha))
        ]);

        var objective = ObjectiveFunction.Value(parameters =>
        {
            var (omega, alpha, beta) = Decode(parameters);

            // Метод оптимизации отыскивает минимум, поэтому возвращается
            // логарифмическое правдоподобие с обратным знаком.
            return -LogLikelihood(centered, sampleVariance, omega, alpha, beta);
        });

        double omegaEstimate, alphaEstimate, betaEstimate;

        try
        {
            var result = NelderMeadSimplex.Minimum(
                objective, initialGuess, convergenceTolerance: 1e-10, maximumIterations: MaximumIterations);

            (omegaEstimate, alphaEstimate, betaEstimate) = Decode(result.MinimizingPoint);
        }
        catch (MaximumIterationsException)
        {
            // Отсутствие сходимости не должно приводить к отказу расчёта:
            // применяются начальные приближения, соответствующие типичным
            // для доходностей акций значениям.
            omegaEstimate = initialOmega;
            alphaEstimate = initialAlpha;
            betaEstimate = initialBeta;
        }

        var conditional = ConditionalVolatilitySeries(
            centered, sampleVariance, omegaEstimate, alphaEstimate, betaEstimate, out var forecast);

        var persistence = alphaEstimate + betaEstimate;

        var longRunVariance = persistence < 1.0
            ? omegaEstimate / (1.0 - persistence)
            : sampleVariance;

        var halfLife = persistence is > 0.0 and < 1.0
            ? Math.Log(0.5) / Math.Log(persistence)
            : double.PositiveInfinity;

        var logLikelihood = LogLikelihood(
            centered, sampleVariance, omegaEstimate, alphaEstimate, betaEstimate);

        return new VolatilityResult(
            Model: "GARCH(1,1)",
            ConditionalVolatility: conditional,
            Forecast: forecast,
            UnconditionalVolatility: statistics.StandardDeviation,
            MinimumVolatility: conditional.Skip(30).Min(),
            MaximumVolatility: conditional.Max(),
            Parameters: new Dictionary<string, double>
            {
                ["omega"] = omegaEstimate,
                ["alpha"] = alphaEstimate,
                ["beta"] = betaEstimate,
                ["persistence"] = persistence,
                ["longRunVolatility"] = Math.Sqrt(longRunVariance),
                ["halfLifeDays"] = halfLife
            },
            LogLikelihood: logLikelihood,
            Description:
                $"Модель GARCH(1,1): ω = {omegaEstimate:E3}, α = {alphaEstimate:F4}, " +
                $"β = {betaEstimate:F4}, сумма коэффициентов {persistence:F4}. " +
                $"Долгосрочная волатильность {Math.Sqrt(longRunVariance):P2}, период " +
                $"полураспада отклонения от неё {halfLife:F1} торг. дн. Прогноз " +
                $"волатильности на следующий период {forecast:P2} против безусловной " +
                $"оценки {statistics.StandardDeviation:P2}.");
    }

    /// <summary>
    /// Рассчитывает ряд условных среднеквадратических отклонений и прогноз
    /// на следующий период при заданных параметрах модели.
    /// </summary>
    private static double[] ConditionalVolatilitySeries(
        double[] centered,
        double initialVariance,
        double omega,
        double alpha,
        double beta,
        out double forecast)
    {
        var conditional = new double[centered.Length];
        var variance = initialVariance;

        for (var t = 0; t < centered.Length; t++)
        {
            conditional[t] = Math.Sqrt(variance);
            variance = omega + alpha * centered[t] * centered[t] + beta * variance;
        }

        forecast = Math.Sqrt(variance);

        return conditional;
    }

    /// <summary>
    /// Рассчитывает логарифмическую функцию правдоподобия при условной
    /// нормальности доходностей.
    /// </summary>
    private static double LogLikelihood(
        double[] centered, double initialVariance, double omega, double alpha, double beta)
    {
        var variance = initialVariance;
        double logLikelihood = 0.0;

        for (var t = 0; t < centered.Length; t++)
        {
            if (variance <= 0.0 || double.IsNaN(variance) || double.IsInfinity(variance))
            {
                return double.NegativeInfinity;
            }

            logLikelihood -= 0.5 * (Math.Log(2.0 * Math.PI) + Math.Log(variance) +
                                    centered[t] * centered[t] / variance);

            variance = omega + alpha * centered[t] * centered[t] + beta * variance;
        }

        return logLikelihood;
    }

    /// <summary>
    /// Преобразует неограниченные величины оптимизации в параметры модели,
    /// удовлетворяющие ограничениям ω &gt; 0, α ≥ 0, β ≥ 0, α + β &lt; 1.
    /// </summary>
    private static (double Omega, double Alpha, double Beta) Decode(Vector<double> parameters)
    {
        var omega = Math.Exp(parameters[0]);
        var alpha = Logistic(parameters[1]) * PersistenceLimit;
        var beta = Logistic(parameters[2]) * (PersistenceLimit - alpha);

        return (omega, alpha, beta);
    }

    /// <summary>Логистическая функция.</summary>
    private static double Logistic(double value) => 1.0 / (1.0 + Math.Exp(-value));

    /// <summary>Обратная логистическая функция.</summary>
    private static double InverseLogistic(double value)
    {
        var bounded = Math.Clamp(value, 1e-6, 1.0 - 1e-6);

        return Math.Log(bounded / (1.0 - bounded));
    }
}
