using RiskAnalysis.RiskEngine.Var;

namespace RiskAnalysis.RiskEngine.Backtesting;

/// <summary>
/// Зона надзорной оценки качества модели по подходу Базельского комитета.
/// </summary>
public enum BaselZone
{
    /// <summary>
    /// Зелёная зона: число нарушений согласуется с заявленным уровнем
    /// доверия, модель признаётся приемлемой.
    /// </summary>
    Green = 1,

    /// <summary>
    /// Жёлтая зона: число нарушений превышает ожидаемое, однако может быть
    /// объяснено случайностью. Требуется дополнительная проверка модели,
    /// к нормативу достаточности капитала применяется надбавка.
    /// </summary>
    Yellow = 2,

    /// <summary>
    /// Красная зона: число нарушений практически исключает случайное
    /// объяснение. Модель признаётся непригодной.
    /// </summary>
    Red = 3
}

/// <summary>Наблюдение бэктеста.</summary>
/// <param name="Date">Дата наблюдения.</param>
/// <param name="ActualReturn">Фактическая доходность.</param>
/// <param name="VarEstimate">
/// Оценка стоимостной меры риска, полученная по предшествующим наблюдениям.
/// Положительная величина, выражающая потерю.
/// </param>
/// <param name="IsViolation">Признак нарушения: фактический убыток превысил оценку.</param>
public readonly record struct BacktestPoint(
    DateOnly Date,
    double ActualReturn,
    double VarEstimate,
    bool IsViolation);

/// <summary>
/// Результат проверки по критерию Купца (критерий безусловного покрытия).
/// </summary>
/// <param name="Statistic">Наблюдаемое значение статистики критерия.</param>
/// <param name="PValue">Достигаемый уровень значимости.</param>
/// <param name="CriticalValue">Критическое значение.</param>
/// <param name="IsRejected">Признак отклонения гипотезы о корректности модели.</param>
/// <param name="Conclusion">Словесная формулировка вывода.</param>
public sealed record KupiecTestResult(
    double Statistic,
    double PValue,
    double CriticalValue,
    bool IsRejected,
    string Conclusion);

/// <summary>
/// Результат проверки по критерию Кристоферсена.
/// </summary>
/// <param name="IndependenceStatistic">Статистика критерия независимости нарушений.</param>
/// <param name="IndependencePValue">Достигаемый уровень значимости критерия независимости.</param>
/// <param name="IndependenceRejected">Признак отклонения гипотезы о независимости.</param>
/// <param name="ConditionalCoverageStatistic">Статистика критерия условного покрытия.</param>
/// <param name="ConditionalCoveragePValue">Достигаемый уровень значимости условного покрытия.</param>
/// <param name="ConditionalCoverageRejected">Признак отклонения гипотезы условного покрытия.</param>
/// <param name="N00">Число случаев, когда за отсутствием нарушения следовало отсутствие нарушения.</param>
/// <param name="N01">Число случаев, когда за отсутствием нарушения следовало нарушение.</param>
/// <param name="N10">Число случаев, когда за нарушением следовало отсутствие нарушения.</param>
/// <param name="N11">Число случаев, когда за нарушением следовало нарушение.</param>
/// <param name="Conclusion">Словесная формулировка вывода.</param>
public sealed record ChristoffersenTestResult(
    double IndependenceStatistic,
    double IndependencePValue,
    bool IndependenceRejected,
    double ConditionalCoverageStatistic,
    double ConditionalCoveragePValue,
    bool ConditionalCoverageRejected,
    int N00,
    int N01,
    int N10,
    int N11,
    string Conclusion);

/// <summary>
/// Результат бэктестирования модели оценки стоимостной меры риска.
/// </summary>
/// <param name="Method">Проверяемый метод.</param>
/// <param name="ConfidenceLevel">Уровень доверия.</param>
/// <param name="WindowSize">Глубина скользящего окна оценивания, наблюдений.</param>
/// <param name="Observations">Число проверенных наблюдений.</param>
/// <param name="Violations">Число нарушений.</param>
/// <param name="ViolationRate">Фактическая доля нарушений.</param>
/// <param name="ExpectedViolationRate">Ожидаемая доля нарушений, равная 1 − α.</param>
/// <param name="ExpectedViolations">Ожидаемое число нарушений.</param>
/// <param name="Kupiec">Результат критерия Купца.</param>
/// <param name="Christoffersen">Результат критерия Кристоферсена.</param>
/// <param name="Zone">Зона надзорной оценки по подходу Базельского комитета.</param>
/// <param name="CapitalMultiplierAddOn">Надбавка к множителю достаточности капитала.</param>
/// <param name="AverageVar">Средняя оценка стоимостной меры риска за период проверки.</param>
/// <param name="AverageViolationSize">
/// Средняя величина превышения фактического убытка над оценкой в случаях нарушения.
/// </param>
/// <param name="Series">Ряд наблюдений бэктеста.</param>
/// <param name="Conclusion">Обобщённый вывод.</param>
public sealed record BacktestResult(
    VarMethod Method,
    double ConfidenceLevel,
    int WindowSize,
    int Observations,
    int Violations,
    double ViolationRate,
    double ExpectedViolationRate,
    double ExpectedViolations,
    KupiecTestResult Kupiec,
    ChristoffersenTestResult Christoffersen,
    BaselZone Zone,
    double CapitalMultiplierAddOn,
    double AverageVar,
    double AverageViolationSize,
    IReadOnlyList<BacktestPoint> Series,
    string Conclusion);
