using RiskAnalysis.RiskEngine.Optimization;

namespace RiskAnalysis.Application.Models;

/// <summary>Структура портфеля с указанием инструментов.</summary>
/// <param name="Name">Наименование портфеля.</param>
/// <param name="ExpectedReturn">Ожидаемая доходность в годовом выражении.</param>
/// <param name="Volatility">Волатильность в годовом выражении.</param>
/// <param name="SharpeRatio">Коэффициент Шарпа.</param>
/// <param name="Weights">Доли инструментов с указанием биржевых кодов.</param>
public sealed record NamedPortfolio(
    string Name,
    double ExpectedReturn,
    double Volatility,
    double SharpeRatio,
    IReadOnlyList<InstrumentWeight> Weights);

/// <summary>Доля инструмента в портфеле.</summary>
/// <param name="InstrumentId">Идентификатор инструмента.</param>
/// <param name="Ticker">Биржевой код инструмента.</param>
/// <param name="Weight">Доля в портфеле.</param>
/// <param name="CurrentWeight">Текущая доля в портфеле.</param>
/// <param name="Change">Изменение доли относительно текущей.</param>
public sealed record InstrumentWeight(
    int InstrumentId,
    string Ticker,
    double Weight,
    double CurrentWeight,
    double Change);

/// <summary>Точка множества портфелей для построения диаграммы.</summary>
/// <param name="Volatility">Волатильность в годовом выражении.</param>
/// <param name="ExpectedReturn">Ожидаемая доходность в годовом выражении.</param>
/// <param name="SharpeRatio">Коэффициент Шарпа.</param>
public sealed record FrontierPoint(double Volatility, double ExpectedReturn, double SharpeRatio);

/// <summary>
/// Результат оптимизации структуры инвестиционного портфеля.
/// </summary>
/// <param name="PortfolioId">Идентификатор портфеля.</param>
/// <param name="PortfolioName">Наименование портфеля.</param>
/// <param name="From">Начало периода выборки.</param>
/// <param name="To">Конец периода выборки.</param>
/// <param name="ObservationCount">Число наблюдений общего торгового календаря.</param>
/// <param name="PortfolioValue">Стоимость портфеля.</param>
/// <param name="RiskFreeRateAnnual">Безрисковая ставка в годовом выражении.</param>
/// <param name="MaximumWeight">Предельная доля одного инструмента.</param>
/// <param name="Current">Текущая структура портфеля.</param>
/// <param name="MinimumVariance">Портфель наименьшей дисперсии.</param>
/// <param name="MaximumSharpe">Касательный портфель.</param>
/// <param name="EfficientFrontier">Точки эффективной границы.</param>
/// <param name="RandomPortfolios">Портфели со случайной структурой.</param>
/// <param name="Conclusion">Вывод по результатам оптимизации.</param>
/// <param name="DurationMs">Длительность расчёта в миллисекундах.</param>
public sealed record OptimizationReport(
    int PortfolioId,
    string PortfolioName,
    DateOnly From,
    DateOnly To,
    int ObservationCount,
    double PortfolioValue,
    double RiskFreeRateAnnual,
    double MaximumWeight,
    NamedPortfolio? Current,
    NamedPortfolio MinimumVariance,
    NamedPortfolio MaximumSharpe,
    IReadOnlyList<FrontierPoint> EfficientFrontier,
    IReadOnlyList<FrontierPoint> RandomPortfolios,
    string Conclusion,
    int DurationMs);
