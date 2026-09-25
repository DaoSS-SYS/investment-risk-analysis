using MathNet.Numerics.Distributions;
using RiskAnalysis.RiskEngine.Returns;
using RiskAnalysis.RiskEngine.Var;

namespace RiskAnalysis.RiskEngine.Backtesting;

/// <summary>
/// Бэктестирование моделей оценки стоимостной меры риска.
///
/// Оценка риска сама по себе не подтверждает пригодность модели: любой метод
/// возвращает некоторое число. Бэктестирование отвечает на вопрос о том,
/// согласуется ли полученная оценка с фактически наблюдавшимися потерями.
///
/// Порядок проверки. Применяется скользящее окно оценивания: для каждого дня
/// периода проверки стоимостная мера риска рассчитывается исключительно по
/// предшествующим наблюдениям, после чего сопоставляется с фактической
/// доходностью этого дня. Если убыток превысил оценку, фиксируется нарушение.
/// Использование только предшествующих данных принципиально: модель, знающая
/// будущее, показала бы недостижимую на практике точность.
///
/// Если модель корректна, доля нарушений должна приближаться к величине 1 − α,
/// а сами нарушения должны быть независимы во времени. Оба свойства
/// проверяются отдельно:
///
/// — критерий Купца проверяет соответствие частоты нарушений заявленному
///   уровню доверия (безусловное покрытие);
/// — критерий Кристоферсена проверяет независимость нарушений: их
///   группирование указывает на то, что модель не успевает реагировать
///   на изменение рыночных условий;
/// — совместная проверка обоих свойств образует критерий условного покрытия.
///
/// Дополнительно применяется подход Базельского комитета, относящий модель
/// к одной из трёх зон по числу нарушений.
/// </summary>
public static class BacktestCalculator
{
    /// <summary>
    /// Число наблюдений, для которого Базельским комитетом установлена
    /// шкала надбавок к множителю достаточности капитала.
    /// </summary>
    private const int BaselObservationBase = 250;

    /// <summary>
    /// Выполняет бэктестирование модели оценки стоимостной меры риска.
    /// </summary>
    /// <param name="returns">
    /// Ряд логарифмических доходностей с датами, упорядоченный по возрастанию.
    /// </param>
    /// <param name="method">Проверяемый метод.</param>
    /// <param name="confidenceLevel">Уровень доверия.</param>
    /// <param name="windowSize">
    /// Глубина скользящего окна оценивания. Значение 250 соответствует одному
    /// году торгов и предусмотрено требованиями Базельского комитета;
    /// увеличение глубины повышает устойчивость оценки, но замедляет
    /// реакцию модели на изменение рыночных условий.
    /// </param>
    /// <param name="scenarioCount">
    /// Число сценариев метода Монте-Карло. При бэктестировании метод
    /// применяется к каждому дню периода проверки, поэтому число сценариев
    /// принимается меньшим, чем при разовом расчёте.
    /// </param>
    /// <param name="significanceLevel">Уровень значимости критериев.</param>
    public static BacktestResult Run(
        IReadOnlyList<ReturnObservation> returns,
        VarMethod method,
        double confidenceLevel = 0.99,
        int windowSize = 250,
        int scenarioCount = 10_000,
        double significanceLevel = 0.05)
    {
        ArgumentNullException.ThrowIfNull(returns);

        VarConventions.ValidateConfidenceLevel(confidenceLevel);

        if (windowSize < 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowSize),
                "Глубина окна оценивания должна составлять не менее ста наблюдений.");
        }

        if (returns.Count <= windowSize + 30)
        {
            throw new ArgumentException(
                $"Для бэктестирования при глубине окна {windowSize} требуется не менее " +
                $"{windowSize + 31} наблюдений, представлено {returns.Count}.",
                nameof(returns));
        }

        var observations = returns.Count - windowSize;
        var series = new List<BacktestPoint>(observations);

        var window = new double[windowSize];
        var violations = 0;
        double varSum = 0.0;
        double violationExcessSum = 0.0;

        for (var t = windowSize; t < returns.Count; t++)
        {
            // Окно оценивания содержит только предшествующие наблюдения.
            for (var i = 0; i < windowSize; i++)
            {
                window[i] = returns[t - windowSize + i].Value;
            }

            var estimate = EstimateVar(window, method, confidenceLevel, scenarioCount);
            var actual = returns[t].Value;

            // Нарушение: фактический убыток превысил оценку.
            // Доходности отрицательны при убытке, оценка положительна.
            var isViolation = actual < -estimate;

            if (isViolation)
            {
                violations++;
                violationExcessSum += -actual - estimate;
            }

            varSum += estimate;

            series.Add(new BacktestPoint(returns[t].Date, actual, estimate, isViolation));
        }

        var expectedRate = 1.0 - confidenceLevel;
        var violationRate = (double)violations / observations;

        var kupiec = KupiecTest(observations, violations, expectedRate, significanceLevel);
        var christoffersen = ChristoffersenTest(series, kupiec.Statistic, significanceLevel);
        var zone = DetermineBaselZone(observations, violations, expectedRate);

        return new BacktestResult(
            Method: method,
            ConfidenceLevel: confidenceLevel,
            WindowSize: windowSize,
            Observations: observations,
            Violations: violations,
            ViolationRate: violationRate,
            ExpectedViolationRate: expectedRate,
            ExpectedViolations: expectedRate * observations,
            Kupiec: kupiec,
            Christoffersen: christoffersen,
            Zone: zone,
            CapitalMultiplierAddOn: CapitalAddOn(observations, violations),
            AverageVar: varSum / observations,
            AverageViolationSize: violations > 0 ? violationExcessSum / violations : 0.0,
            Series: series,
            Conclusion: BuildConclusion(method, kupiec, christoffersen, zone, violationRate, expectedRate));
    }

    /// <summary>
    /// Рассчитывает оценку стоимостной меры риска по окну наблюдений
    /// указанным методом.
    /// </summary>
    private static double EstimateVar(
        double[] window, VarMethod method, double confidenceLevel, int scenarioCount)
    {
        return method switch
        {
            VarMethod.Parametric =>
                ParametricVarCalculator.Calculate(window, confidenceLevel).ValueAtRiskRelative,

            VarMethod.Historical =>
                HistoricalVarCalculator.Calculate(window, confidenceLevel).ValueAtRiskRelative,

            VarMethod.MonteCarlo =>
                MonteCarloVarCalculator
                    .Calculate(
                        window, confidenceLevel, 1, 1.0,
                        new MonteCarloOptions(scenarioCount, SimulationDistribution.Normal))
                    .ValueAtRiskRelative,

            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };
    }

    // -----------------------------------------------------------------------
    // Критерий Купца
    // -----------------------------------------------------------------------

    /// <summary>
    /// Критерий Купца (критерий доли нарушений, proportion of failures).
    ///
    /// Проверяется гипотеза о том, что вероятность нарушения равна
    /// заявленной величине p = 1 − α. Статистика критерия отношения
    /// правдоподобия имеет вид
    ///
    ///     LR = −2 · ln[ ((1−p)^(T−N) · p^N) / ((1−N/T)^(T−N) · (N/T)^N) ]
    ///
    /// и при справедливости гипотезы асимптотически подчиняется
    /// распределению хи-квадрат с одной степенью свободы.
    ///
    /// Критерий выявляет как недооценку риска (нарушений больше ожидаемого),
    /// так и его переоценку (нарушений меньше ожидаемого). Второе также
    /// нежелательно: избыточно высокая оценка риска ведёт к неоправданному
    /// отвлечению капитала.
    /// </summary>
    public static KupiecTestResult KupiecTest(
        int observations, int violations, double expectedRate, double significanceLevel = 0.05)
    {
        if (observations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(observations));
        }

        var observedRate = (double)violations / observations;

        double statistic;

        if (violations == 0)
        {
            // Предельный случай: логарифм правдоподобия при наблюдаемой
            // частоте, равной нулю, вычисляется непосредственно.
            statistic = -2.0 * observations * Math.Log(1.0 - expectedRate);
        }
        else if (violations == observations)
        {
            statistic = -2.0 * observations * Math.Log(expectedRate);
        }
        else
        {
            var restricted =
                (observations - violations) * Math.Log(1.0 - expectedRate) +
                violations * Math.Log(expectedRate);

            var unrestricted =
                (observations - violations) * Math.Log(1.0 - observedRate) +
                violations * Math.Log(observedRate);

            statistic = -2.0 * (restricted - unrestricted);
        }

        var chiSquared = new ChiSquared(1);
        var criticalValue = chiSquared.InverseCumulativeDistribution(1.0 - significanceLevel);
        var pValue = 1.0 - chiSquared.CumulativeDistribution(statistic);

        var isRejected = statistic > criticalValue;

        var direction = observedRate > expectedRate ? "занижает" : "завышает";

        var conclusion = isRejected
            ? $"Гипотеза о соответствии частоты нарушений уровню доверия отвергается: " +
              $"фактическая доля нарушений {observedRate:P2} при ожидаемой {expectedRate:P2}. " +
              $"Модель {direction} риск."
            : $"Гипотеза о соответствии частоты нарушений уровню доверия не отвергается: " +
              $"фактическая доля нарушений {observedRate:P2} при ожидаемой {expectedRate:P2}. " +
              "Частота нарушений согласуется с заявленным уровнем доверия.";

        return new KupiecTestResult(statistic, pValue, criticalValue, isRejected, conclusion);
    }

    // -----------------------------------------------------------------------
    // Критерий Кристоферсена
    // -----------------------------------------------------------------------

    /// <summary>
    /// Критерий Кристоферсена: проверка независимости нарушений
    /// и условного покрытия.
    ///
    /// Соответствие частоты нарушений заявленному уровню доверия является
    /// необходимым, но не достаточным условием пригодности модели. Если
    /// нарушения группируются — несколько подряд в период рыночного шока, —
    /// это означает, что модель не успевает реагировать на изменение
    /// волатильности, даже когда общее число нарушений допустимо.
    ///
    /// Проверяется гипотеза о независимости нарушений от предшествующего
    /// состояния. По ряду нарушений строится матрица переходов, и
    /// сопоставляются правдоподобия при условных вероятностях, различающихся
    /// в зависимости от предшествующего состояния, и при единой вероятности.
    /// Статистика асимптотически подчиняется распределению хи-квадрат
    /// с одной степенью свободы.
    ///
    /// Критерий условного покрытия объединяет обе проверки: его статистика
    /// равна сумме статистик критерия Купца и критерия независимости и
    /// подчиняется распределению хи-квадрат с двумя степенями свободы.
    /// </summary>
    public static ChristoffersenTestResult ChristoffersenTest(
        IReadOnlyList<BacktestPoint> series,
        double kupiecStatistic,
        double significanceLevel = 0.05)
    {
        ArgumentNullException.ThrowIfNull(series);

        int n00 = 0, n01 = 0, n10 = 0, n11 = 0;

        for (var i = 1; i < series.Count; i++)
        {
            var previous = series[i - 1].IsViolation;
            var current = series[i].IsViolation;

            if (!previous && !current) n00++;
            else if (!previous && current) n01++;
            else if (previous && !current) n10++;
            else n11++;
        }

        // Условные вероятности нарушения в зависимости от предшествующего
        // состояния и безусловная вероятность.
        var denominator0 = n00 + n01;
        var denominator1 = n10 + n11;
        var total = denominator0 + denominator1;

        var pi01 = denominator0 > 0 ? (double)n01 / denominator0 : 0.0;
        var pi11 = denominator1 > 0 ? (double)n11 / denominator1 : 0.0;
        var pi = total > 0 ? (double)(n01 + n11) / total : 0.0;

        double independenceStatistic;

        // Если нарушения отсутствуют либо ни одно нарушение не следует
        // за другим, статистика независимости не определена и принимается
        // равной нулю: оснований отвергнуть независимость нет.
        if (pi <= 0.0 || pi >= 1.0 || denominator1 == 0)
        {
            independenceStatistic = 0.0;
        }
        else
        {
            var restricted = (n00 + n10) * Math.Log(1.0 - pi) + (n01 + n11) * Math.Log(pi);

            var unrestricted =
                SafeLogTerm(n00, 1.0 - pi01) +
                SafeLogTerm(n01, pi01) +
                SafeLogTerm(n10, 1.0 - pi11) +
                SafeLogTerm(n11, pi11);

            independenceStatistic = Math.Max(0.0, -2.0 * (restricted - unrestricted));
        }

        var conditionalCoverageStatistic = kupiecStatistic + independenceStatistic;

        var chi1 = new ChiSquared(1);
        var chi2 = new ChiSquared(2);

        var independencePValue = 1.0 - chi1.CumulativeDistribution(independenceStatistic);
        var conditionalPValue = 1.0 - chi2.CumulativeDistribution(conditionalCoverageStatistic);

        var independenceRejected =
            independenceStatistic > chi1.InverseCumulativeDistribution(1.0 - significanceLevel);

        var conditionalRejected =
            conditionalCoverageStatistic > chi2.InverseCumulativeDistribution(1.0 - significanceLevel);

        var conclusion = independenceRejected
            ? "Гипотеза о независимости нарушений отвергается: нарушения группируются " +
              "во времени, что указывает на запаздывание модели при изменении " +
              "рыночных условий."
            : "Гипотеза о независимости нарушений не отвергается: нарушения " +
              "распределены во времени случайным образом.";

        return new ChristoffersenTestResult(
            IndependenceStatistic: independenceStatistic,
            IndependencePValue: independencePValue,
            IndependenceRejected: independenceRejected,
            ConditionalCoverageStatistic: conditionalCoverageStatistic,
            ConditionalCoveragePValue: conditionalPValue,
            ConditionalCoverageRejected: conditionalRejected,
            N00: n00, N01: n01, N10: n10, N11: n11,
            Conclusion: conclusion);
    }

    /// <summary>
    /// Слагаемое логарифма правдоподобия. При нулевом числе наблюдений
    /// слагаемое обращается в ноль независимо от значения вероятности.
    /// </summary>
    private static double SafeLogTerm(int count, double probability)
    {
        return count == 0 ? 0.0 : count * Math.Log(probability);
    }

    // -----------------------------------------------------------------------
    // Подход Базельского комитета
    // -----------------------------------------------------------------------

    /// <summary>
    /// Определяет зону надзорной оценки модели.
    ///
    /// Зона определяется вероятностью получить наблюдаемое либо меньшее число
    /// нарушений при справедливости модели. Границы зон установлены
    /// Базельским комитетом на уровнях 95 % и 99,99 %: для 250 наблюдений
    /// при уровне доверия 99 % им соответствуют 4 и 9 нарушений.
    /// Расчёт через биномиальное распределение позволяет применять подход
    /// при любом числе наблюдений.
    /// </summary>
    public static BaselZone DetermineBaselZone(int observations, int violations, double expectedRate)
    {
        var binomial = new Binomial(expectedRate, observations);
        var cumulative = binomial.CumulativeDistribution(violations);

        return cumulative switch
        {
            < 0.95 => BaselZone.Green,
            < 0.9999 => BaselZone.Yellow,
            _ => BaselZone.Red
        };
    }

    /// <summary>
    /// Надбавка к множителю достаточности капитала по шкале Базельского
    /// комитета. Шкала установлена для 250 наблюдений, поэтому фактическое
    /// число нарушений приводится к этой базе.
    /// </summary>
    public static double CapitalAddOn(int observations, int violations)
    {
        var scaled = (int)Math.Round((double)violations * BaselObservationBase / observations);

        return scaled switch
        {
            <= 4 => 0.00,
            5 => 0.40,
            6 => 0.50,
            7 => 0.65,
            8 => 0.75,
            9 => 0.85,
            _ => 1.00
        };
    }

    private static string BuildConclusion(
        VarMethod method,
        KupiecTestResult kupiec,
        ChristoffersenTestResult christoffersen,
        BaselZone zone,
        double violationRate,
        double expectedRate)
    {
        var methodName = method switch
        {
            VarMethod.Parametric => "Параметрический метод",
            VarMethod.Historical => "Метод исторического моделирования",
            VarMethod.MonteCarlo => "Метод Монте-Карло",
            _ => "Метод"
        };

        var zoneName = zone switch
        {
            BaselZone.Green => "зелёной",
            BaselZone.Yellow => "жёлтой",
            BaselZone.Red => "красной",
            _ => "неопределённой"
        };

        var verdict =
            !kupiec.IsRejected && !christoffersen.ConditionalCoverageRejected
                ? "Модель признаётся пригодной: частота нарушений согласуется с уровнем " +
                  "доверия, нарушения независимы."
                : kupiec.IsRejected && violationRate > expectedRate
                    ? "Модель признаётся непригодной: она систематически занижает риск."
                    : kupiec.IsRejected
                        ? "Модель признаётся непригодной: она систематически завышает риск, " +
                          "что ведёт к неоправданному отвлечению капитала."
                        : "Частота нарушений допустима, однако нарушения группируются " +
                          "во времени: модель запаздывает при изменении рыночных условий.";

        return $"{methodName}: фактическая доля нарушений {violationRate:P2} при ожидаемой " +
               $"{expectedRate:P2}, модель относится к {zoneName} зоне надзорной оценки. {verdict}";
    }
}
