using RiskAnalysis.Domain.Enums;
using RiskAnalysis.RiskEngine.Performance;
using RiskAnalysis.RiskEngine.Statistics;

namespace RiskAnalysis.Application.Models;

/// <summary>
/// Значение макроэкономического показателя, полученное от внешнего источника.
/// </summary>
/// <param name="Code">Код показателя.</param>
/// <param name="Date">Дата, на которую действует значение.</param>
/// <param name="Value">Значение показателя.</param>
public sealed record MacroObservation(MacroIndicatorCode Code, DateOnly Date, decimal Value);

/// <summary>
/// Безрисковая ставка, применяемая при расчёте коэффициентов эффективности.
/// </summary>
/// <param name="AnnualRate">Ставка в годовом выражении, в долях единицы.</param>
/// <param name="Source">Источник значения.</param>
/// <param name="ObservationCount">Число значений, по которым получена оценка.</param>
/// <param name="Minimum">Наименьшее значение ставки за период.</param>
/// <param name="Maximum">Наибольшее значение ставки за период.</param>
public sealed record RiskFreeRate(
    double AnnualRate,
    string Source,
    int ObservationCount,
    double Minimum,
    double Maximum);

/// <summary>
/// Результат анализа эффективности вложения в финансовый инструмент.
/// </summary>
/// <param name="InstrumentId">Идентификатор инструмента.</param>
/// <param name="Ticker">Биржевой код инструмента.</param>
/// <param name="BenchmarkTicker">Биржевой код эталонного портфеля.</param>
/// <param name="From">Начало периода.</param>
/// <param name="To">Конец периода.</param>
/// <param name="ReturnCount">Число наблюдений доходности.</param>
/// <param name="RiskFreeRate">Применённая безрисковая ставка.</param>
/// <param name="Statistics">Описательные статистики доходностей.</param>
/// <param name="Performance">Коэффициенты эффективности.</param>
/// <param name="Drawdown">Анализ просадок.</param>
/// <param name="Capm">Параметры модели CAPM. Не определены, если эталонный портфель не задан.</param>
/// <param name="DurationMs">Длительность расчёта в миллисекундах.</param>
public sealed record PerformanceAnalysisResult(
    int InstrumentId,
    string Ticker,
    string? BenchmarkTicker,
    DateOnly From,
    DateOnly To,
    int ReturnCount,
    RiskFreeRate RiskFreeRate,
    DescriptiveStatisticsResult Statistics,
    PerformanceMetricsResult Performance,
    DrawdownResult Drawdown,
    CapmResult? Capm,
    int DurationMs);

/// <summary>
/// Результат корреляционного анализа группы инструментов.
/// </summary>
/// <param name="Tickers">Биржевые коды инструментов в порядке следования в матрице.</param>
/// <param name="From">Начало периода.</param>
/// <param name="To">Конец периода.</param>
/// <param name="ObservationCount">Число наблюдений общего торгового календаря.</param>
/// <param name="Correlation">Корреляционная матрица.</param>
/// <param name="AnnualizedVolatility">Волатильность инструментов в годовом выражении.</param>
/// <param name="EqualWeightedPortfolioVolatility">
/// Волатильность равновзвешенного портфеля из рассматриваемых инструментов.
/// </param>
/// <param name="WeightedAverageVolatility">
/// Средневзвешенная волатильность инструментов — значение, которое имел бы
/// портфель при полной положительной корреляции составляющих.
/// </param>
/// <param name="DiversificationEffect">
/// Эффект диверсификации: относительное снижение волатильности портфеля
/// по сравнению со средневзвешенной волатильностью составляющих.
/// </param>
/// <param name="AverageCorrelation">Средняя парная корреляция.</param>
/// <param name="Conclusion">Вывод по результатам анализа.</param>
public sealed record CorrelationAnalysisResult(
    IReadOnlyList<string> Tickers,
    DateOnly From,
    DateOnly To,
    int ObservationCount,
    double[][] Correlation,
    IReadOnlyList<double> AnnualizedVolatility,
    double EqualWeightedPortfolioVolatility,
    double WeightedAverageVolatility,
    double DiversificationEffect,
    double AverageCorrelation,
    string Conclusion);
