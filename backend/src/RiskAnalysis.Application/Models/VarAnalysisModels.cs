using RiskAnalysis.RiskEngine.Statistics;
using RiskAnalysis.RiskEngine.Var;

namespace RiskAnalysis.Application.Models;

/// <summary>
/// Сопоставление оценок стоимостной меры риска, полученных различными
/// методами на одной выборке.
///
/// Одновременный расчёт несколькими методами позволяет оценить влияние
/// предположений, лежащих в основе каждого метода, на итоговую оценку риска
/// и составляет содержание апробации системы.
/// </summary>
/// <param name="InstrumentId">Идентификатор инструмента.</param>
/// <param name="Ticker">Биржевой код инструмента.</param>
/// <param name="From">Начало периода выборки.</param>
/// <param name="To">Конец периода выборки.</param>
/// <param name="ReturnCount">Число наблюдений доходности.</param>
/// <param name="ConfidenceLevel">Уровень доверия.</param>
/// <param name="HorizonDays">Горизонт оценки в торговых днях.</param>
/// <param name="PortfolioValue">Стоимость позиции, принятая при расчёте.</param>
/// <param name="Statistics">Описательные статистики выборки.</param>
/// <param name="Normality">Результат проверки гипотезы о нормальности.</param>
/// <param name="Estimates">Оценки, полученные каждым из методов.</param>
/// <param name="Conclusion">Вывод по результатам сопоставления.</param>
/// <param name="DurationMs">Длительность расчёта в миллисекундах.</param>
public sealed record VarComparisonResult(
    int InstrumentId,
    string Ticker,
    DateOnly From,
    DateOnly To,
    int ReturnCount,
    double ConfidenceLevel,
    int HorizonDays,
    double PortfolioValue,
    DescriptiveStatisticsResult Statistics,
    NormalityTestResult Normality,
    IReadOnlyList<VarEstimate> Estimates,
    string Conclusion,
    int DurationMs);

/// <summary>
/// Оценка стоимостной меры риска, полученная одним методом.
/// </summary>
/// <param name="Name">Наименование метода для представления пользователю.</param>
/// <param name="Result">Результат расчёта.</param>
/// <param name="DurationMs">Длительность расчёта в миллисекундах.</param>
public sealed record VarEstimate(string Name, VarResult Result, int DurationMs);
