using RiskAnalysis.RiskEngine.Statistics;

namespace RiskAnalysis.RiskEngine.Performance;

/// <summary>
/// Коэффициенты эффективности вложения, не требующие эталонного портфеля.
/// </summary>
/// <param name="Count">Число наблюдений.</param>
/// <param name="RiskFreeRateAnnual">Безрисковая ставка в годовом выражении.</param>
/// <param name="AnnualizedLogReturn">Средняя логарифмическая доходность в годовом выражении.</param>
/// <param name="AnnualizedReturn">
/// Доходность в годовом выражении, приведённая к простой: exp(R) − 1.
/// </param>
/// <param name="AnnualizedVolatility">Волатильность в годовом выражении.</param>
/// <param name="AnnualizedExcessReturn">Доходность сверх безрисковой ставки.</param>
/// <param name="SharpeRatio">Коэффициент Шарпа.</param>
/// <param name="SortinoRatio">Коэффициент Сортино.</param>
/// <param name="DownsideDeviationAnnualized">Отклонение вниз в годовом выражении.</param>
/// <param name="CalmarRatio">Коэффициент Кальмара.</param>
/// <param name="MaxDrawdown">Максимальная просадка.</param>
/// <param name="Conclusion">Словесная характеристика полученных значений.</param>
public sealed record PerformanceMetricsResult(
    int Count,
    double RiskFreeRateAnnual,
    double AnnualizedLogReturn,
    double AnnualizedReturn,
    double AnnualizedVolatility,
    double AnnualizedExcessReturn,
    double SharpeRatio,
    double SortinoRatio,
    double DownsideDeviationAnnualized,
    double CalmarRatio,
    double MaxDrawdown,
    string Conclusion);

/// <summary>
/// Расчёт коэффициентов эффективности вложения.
///
/// Коэффициенты эффективности соотносят полученную доходность с принятым
/// риском и позволяют сопоставлять вложения, различающиеся и доходностью,
/// и риском. Различаются они тем, какая величина принимается за меру риска.
///
/// Коэффициент Шарпа соотносит доходность сверх безрисковой ставки с общим
/// среднеквадратическим отклонением доходности:
///
///     Sharpe = (R − Rf) / σ.
///
/// Коэффициент Сортино использует в знаменателе отклонение вниз, то есть
/// учитывает только доходности ниже целевого уровня:
///
///     Sortino = (R − Rf) / σ(вниз),    σ(вниз) = √( (1/n) · Σ min(0; r − MAR)² ).
///
/// Различие существенно: коэффициент Шарпа относит к риску и благоприятные
/// отклонения доходности, тогда как инвестор считает риском только
/// неблагоприятные.
///
/// Соотношение коэффициентов. При принятом определении отклонения вниз,
/// в котором знаменатель суммы равен общему числу наблюдений, справедливо
/// строгое утверждение: при неотрицательной избыточной доходности
/// коэффициент Сортино не может оказаться ниже коэффициента Шарпа.
///
/// Обозначим X = r − MAR. Тогда σ² = E[X²] − (E[X])², а квадрат отклонения
/// вниз равен E[X²·1{X&lt;0}], откуда
///
///     DD² = σ² + (E[X])² − E[X²·1{X&gt;0}].
///
/// По неравенству Коши — Буняковского (E[X])² ≤ (E[X·1{X&gt;0}])²
/// ≤ E[X²·1{X&gt;0}], следовательно DD² ≤ σ².
///
/// Таким образом, распространённое утверждение о том, что превышение
/// коэффициента Шарпа над коэффициентом Сортино свидетельствует об
/// отрицательной асимметрии распределения, для данного определения неверно.
/// Содержательной характеристикой служит величина превышения коэффициента
/// Сортино: чем она меньше, тем большую долю общего разброса доходности
/// составляют отклонения вниз.
///
/// Коэффициент Кальмара соотносит доходность с максимальной просадкой:
///
///     Calmar = R / MDD.
///
/// В отличие от первых двух, он основан на фактически наблюдавшемся
/// наибольшем убытке, а не на характеристике разброса.
///
/// Безрисковая ставка. В качестве безрисковой ставки принимается ключевая
/// ставка Банка России. Поскольку расчёты ведутся по логарифмическим
/// доходностям, годовая ставка приводится к дневной логарифмической величине:
///
///     rf(дн.) = ln(1 + rf(год)) / 252.
/// </summary>
public static class PerformanceCalculator
{
    /// <summary>
    /// Приводит годовую безрисковую ставку к дневной логарифмической
    /// доходности.
    /// </summary>
    /// <param name="annualRate">Годовая ставка в долях единицы (0,14 для 14 %).</param>
    /// <param name="periodsPerYear">Число периодов в году.</param>
    public static double PeriodRiskFreeRate(double annualRate, int periodsPerYear = 252)
    {
        if (annualRate <= -1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(annualRate), "Безрисковая ставка не может быть меньше минус единицы.");
        }

        return Math.Log(1.0 + annualRate) / periodsPerYear;
    }

    /// <summary>
    /// Рассчитывает коэффициенты эффективности.
    /// </summary>
    /// <param name="returns">Ряд логарифмических доходностей.</param>
    /// <param name="riskFreeRateAnnual">Безрисковая ставка в годовом выражении.</param>
    /// <param name="maxDrawdown">
    /// Максимальная просадка, рассчитанная по ценовому ряду. Требуется для
    /// коэффициента Кальмара.
    /// </param>
    /// <param name="periodsPerYear">Число периодов в году.</param>
    public static PerformanceMetricsResult Calculate(
        IReadOnlyList<double> returns,
        double riskFreeRateAnnual,
        double maxDrawdown,
        int periodsPerYear = 252)
    {
        ArgumentNullException.ThrowIfNull(returns);

        if (returns.Count < 2)
        {
            throw new ArgumentException(
                "Для расчёта коэффициентов эффективности требуется не менее двух наблюдений.",
                nameof(returns));
        }

        var statistics = DescriptiveStatisticsCalculator.Calculate(returns, periodsPerYear);

        var riskFreePerPeriod = PeriodRiskFreeRate(riskFreeRateAnnual, periodsPerYear);

        var annualizedLogReturn = statistics.Mean * periodsPerYear;
        var annualizedVolatility = statistics.AnnualizedVolatility;

        var excessPerPeriod = statistics.Mean - riskFreePerPeriod;
        var annualizedExcessReturn = excessPerPeriod * periodsPerYear;

        var sharpe = annualizedVolatility > 0
            ? annualizedExcessReturn / annualizedVolatility
            : 0.0;

        // Отклонение вниз: в знаменателе суммы стоит общее число наблюдений,
        // а не число отрицательных отклонений. Такое определение принято
        // в исходной работе Ф. Сортино и обеспечивает сопоставимость
        // коэффициента для рядов с различной долей убыточных периодов.
        double downsideSum = 0.0;

        for (var i = 0; i < returns.Count; i++)
        {
            var shortfall = returns[i] - riskFreePerPeriod;

            if (shortfall < 0.0)
            {
                downsideSum += shortfall * shortfall;
            }
        }

        var downsideDeviation = Math.Sqrt(downsideSum / returns.Count);
        var downsideAnnualized = downsideDeviation * Math.Sqrt(periodsPerYear);

        var sortino = downsideAnnualized > 0
            ? annualizedExcessReturn / downsideAnnualized
            : 0.0;

        var annualizedReturn = Math.Exp(annualizedLogReturn) - 1.0;

        var calmar = maxDrawdown > 0 ? annualizedReturn / maxDrawdown : 0.0;

        return new PerformanceMetricsResult(
            Count: returns.Count,
            RiskFreeRateAnnual: riskFreeRateAnnual,
            AnnualizedLogReturn: annualizedLogReturn,
            AnnualizedReturn: annualizedReturn,
            AnnualizedVolatility: annualizedVolatility,
            AnnualizedExcessReturn: annualizedExcessReturn,
            SharpeRatio: sharpe,
            SortinoRatio: sortino,
            DownsideDeviationAnnualized: downsideAnnualized,
            CalmarRatio: calmar,
            MaxDrawdown: maxDrawdown,
            Conclusion: BuildConclusion(sharpe, sortino, annualizedExcessReturn));
    }

    private static string BuildConclusion(double sharpe, double sortino, double excessReturn)
    {
        if (excessReturn <= 0.0)
        {
            return "Доходность вложения не превысила безрисковую ставку: коэффициенты " +
                   "эффективности отрицательны, вложение не обеспечило вознаграждения " +
                   "за принятый риск.";
        }

        var sharpeAssessment = sharpe switch
        {
            < 0.5 => "невысокое вознаграждение за риск",
            < 1.0 => "умеренное вознаграждение за риск",
            _ => "высокое вознаграждение за риск"
        };

        // При положительной избыточной доходности коэффициент Сортино
        // не может оказаться ниже коэффициента Шарпа (см. пояснение
        // к классу). Содержательной характеристикой служит не знак
        // разности, а её величина: чем больше превышение, тем меньшую
        // долю общего разброса доходности составляют отклонения вниз.
        var excess = sharpe > 0 ? (sortino - sharpe) / sharpe : 0.0;

        var sortinoNote = excess switch
        {
            < 0.15 => "Коэффициент Сортино близок к коэффициенту Шарпа: разброс " +
                      "доходности в значительной части образован отклонениями вниз.",
            < 0.50 => "Коэффициент Сортино умеренно превышает коэффициент Шарпа: " +
                      "благоприятные отклонения доходности вносят заметный вклад " +
                      "в общий разброс.",
            _ => "Коэффициент Сортино существенно превышает коэффициент Шарпа: " +
                 "общий разброс доходности в основном образован благоприятными " +
                 "отклонениями."
        };

        return $"Коэффициент Шарпа {sharpe:F2} — {sharpeAssessment}. {sortinoNote}";
    }
}
