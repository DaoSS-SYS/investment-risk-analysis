using RiskAnalysis.RiskEngine.Returns;
using RiskAnalysis.RiskEngine.Statistics;

namespace RiskAnalysis.Application.Models;

/// <summary>
/// Результат статистического анализа ряда доходностей инструмента.
/// </summary>
/// <param name="InstrumentId">Идентификатор инструмента.</param>
/// <param name="Ticker">Биржевой код инструмента.</param>
/// <param name="ShortName">Краткое наименование инструмента.</param>
/// <param name="From">Начало периода анализа.</param>
/// <param name="To">Конец периода анализа.</param>
/// <param name="ReturnType">Применённый способ расчёта доходности.</param>
/// <param name="Frequency">Периодичность доходностей.</param>
/// <param name="PriceCount">Число котировок в выборке.</param>
/// <param name="ReturnCount">Число рассчитанных доходностей.</param>
/// <param name="AppliedAdjustments">Число применённых корректировок на корпоративные действия.</param>
/// <param name="Statistics">Описательные статистики.</param>
/// <param name="Normality">Результат проверки гипотезы о нормальности.</param>
/// <param name="Histogram">Гистограмма распределения с теоретическими частотами.</param>
/// <param name="Returns">
/// Ряд доходностей. Включается в результат только по явному запросу,
/// поскольку при десятилетнем периоде содержит свыше двух тысяч значений.
/// </param>
public sealed record ReturnAnalysisResult(
    int InstrumentId,
    string Ticker,
    string ShortName,
    DateOnly From,
    DateOnly To,
    ReturnType ReturnType,
    ReturnFrequency Frequency,
    int PriceCount,
    int ReturnCount,
    int AppliedAdjustments,
    DescriptiveStatisticsResult Statistics,
    NormalityTestResult Normality,
    IReadOnlyList<HistogramBin> Histogram,
    IReadOnlyList<ReturnObservation>? Returns);
