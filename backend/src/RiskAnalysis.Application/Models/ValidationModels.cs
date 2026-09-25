using RiskAnalysis.RiskEngine.Backtesting;
using RiskAnalysis.RiskEngine.StressTesting;

namespace RiskAnalysis.Application.Models;

/// <summary>
/// Результат бэктестирования моделей оценки риска по одному инструменту.
/// </summary>
/// <param name="InstrumentId">Идентификатор инструмента.</param>
/// <param name="Ticker">Биржевой код инструмента.</param>
/// <param name="From">Начало периода выборки.</param>
/// <param name="To">Конец периода выборки.</param>
/// <param name="ConfidenceLevel">Уровень доверия.</param>
/// <param name="WindowSize">Глубина скользящего окна оценивания.</param>
/// <param name="TotalReturns">Общее число наблюдений доходности.</param>
/// <param name="Results">Результаты по каждому проверенному методу.</param>
/// <param name="Conclusion">Обобщённый вывод по результатам сопоставления.</param>
/// <param name="DurationMs">Длительность расчёта в миллисекундах.</param>
public sealed record BacktestReport(
    int InstrumentId,
    string Ticker,
    DateOnly From,
    DateOnly To,
    double ConfidenceLevel,
    int WindowSize,
    int TotalReturns,
    IReadOnlyList<BacktestResult> Results,
    string Conclusion,
    int DurationMs);

/// <summary>
/// Влияние стрессового сценария на позицию с указанием инструмента.
/// </summary>
/// <param name="InstrumentId">Идентификатор инструмента.</param>
/// <param name="Ticker">Биржевой код инструмента.</param>
/// <param name="Weight">Доля инструмента в портфеле.</param>
/// <param name="InstrumentReturn">Доходность инструмента в сценарии.</param>
/// <param name="Contribution">Вклад позиции в доходность портфеля.</param>
/// <param name="LossAmount">Изменение стоимости позиции.</param>
public sealed record ScenarioPositionImpact(
    int InstrumentId,
    string Ticker,
    double Weight,
    double InstrumentReturn,
    double Contribution,
    double LossAmount);

/// <summary>Сценарий с раскрытием влияния на позиции портфеля.</summary>
/// <param name="Name">Наименование сценария.</param>
/// <param name="Kind">Вид сценария.</param>
/// <param name="From">Начало периода исторического сценария.</param>
/// <param name="To">Конец периода исторического сценария.</param>
/// <param name="TradingDays">Продолжительность в торговых днях.</param>
/// <param name="PortfolioReturn">Доходность портфеля в сценарии.</param>
/// <param name="LossAmount">Изменение стоимости портфеля.</param>
/// <param name="ValueAfter">Стоимость портфеля после реализации сценария.</param>
/// <param name="LossToVarRatio">Отношение потерь к стоимостной мере риска.</param>
/// <param name="Impacts">Влияние на отдельные позиции.</param>
/// <param name="Description">Пояснение к сценарию.</param>
public sealed record ScenarioView(
    string Name,
    StressScenarioKind Kind,
    DateOnly? From,
    DateOnly? To,
    int TradingDays,
    double PortfolioReturn,
    double LossAmount,
    double ValueAfter,
    double LossToVarRatio,
    IReadOnlyList<ScenarioPositionImpact> Impacts,
    string Description);

/// <summary>
/// Результат стресс-тестирования портфеля.
/// </summary>
/// <param name="PortfolioId">Идентификатор портфеля.</param>
/// <param name="PortfolioName">Наименование портфеля.</param>
/// <param name="PortfolioValue">Стоимость портфеля.</param>
/// <param name="From">Начало периода выборки.</param>
/// <param name="To">Конец периода выборки.</param>
/// <param name="ConfidenceLevel">Уровень доверия, принятый при расчёте меры риска.</param>
/// <param name="HorizonDays">Горизонт оценки меры риска.</param>
/// <param name="ValueAtRisk">
/// Стоимостная мера риска, с которой сопоставляются результаты сценариев.
/// </param>
/// <param name="ExpectedShortfall">Ожидаемые потери при том же уровне доверия.</param>
/// <param name="Scenarios">Результаты сценариев.</param>
/// <param name="Conclusion">Вывод по результатам стресс-тестирования.</param>
/// <param name="DurationMs">Длительность расчёта в миллисекундах.</param>
public sealed record StressTestReport(
    int PortfolioId,
    string PortfolioName,
    double PortfolioValue,
    DateOnly From,
    DateOnly To,
    double ConfidenceLevel,
    int HorizonDays,
    double ValueAtRisk,
    double ExpectedShortfall,
    IReadOnlyList<ScenarioView> Scenarios,
    string Conclusion,
    int DurationMs);
