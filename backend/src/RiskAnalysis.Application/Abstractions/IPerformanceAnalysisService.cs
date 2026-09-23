using RiskAnalysis.Application.Models;

namespace RiskAnalysis.Application.Abstractions;

/// <summary>
/// Загрузка макроэкономических показателей из открытых источников.
/// </summary>
public interface IMacroImportService
{
    /// <summary>Загружает ключевую ставку Банка России за период.</summary>
    Task<ImportResult> ImportKeyRateAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает безрисковую ставку за период как среднее значение ключевой
    /// ставки Банка России по имеющимся в базе данных наблюдениям.
    /// Если данные отсутствуют, применяется значение по умолчанию из
    /// конфигурации, что обеспечивает работоспособность расчётов при
    /// недоступности внешнего источника.
    /// </summary>
    Task<RiskFreeRate> GetRiskFreeRateAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Анализ эффективности вложений и корреляционный анализ.
/// </summary>
public interface IPerformanceAnalysisService
{
    /// <summary>
    /// Рассчитывает коэффициенты эффективности, просадки и параметры модели
    /// CAPM по инструменту.
    /// </summary>
    /// <param name="instrumentId">Идентификатор инструмента.</param>
    /// <param name="benchmarkInstrumentId">
    /// Идентификатор эталонного портфеля. При отсутствии значения параметры
    /// модели CAPM не рассчитываются.
    /// </param>
    /// <param name="from">Начало периода.</param>
    /// <param name="to">Конец периода.</param>
    /// <param name="includeDrawdownSeries">Включать ли кривую просадки.</param>
    Task<PerformanceAnalysisResult> AnalyzeAsync(
        int instrumentId,
        int? benchmarkInstrumentId,
        DateOnly from,
        DateOnly to,
        bool includeDrawdownSeries = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполняет корреляционный анализ группы инструментов и количественно
    /// оценивает эффект диверсификации.
    /// </summary>
    Task<CorrelationAnalysisResult> AnalyzeCorrelationAsync(
        IReadOnlyCollection<int> instrumentIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
