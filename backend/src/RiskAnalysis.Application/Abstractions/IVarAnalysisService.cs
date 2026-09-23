using RiskAnalysis.Application.Models;

namespace RiskAnalysis.Application.Abstractions;

/// <summary>
/// Оценка стоимостной меры риска финансовых инструментов.
/// </summary>
public interface IVarAnalysisService
{
    /// <summary>
    /// Рассчитывает стоимостную меру риска и ожидаемые потери всеми
    /// реализованными методами и сопоставляет полученные оценки.
    /// </summary>
    /// <param name="instrumentId">Идентификатор инструмента.</param>
    /// <param name="from">Начало периода выборки.</param>
    /// <param name="to">Конец периода выборки.</param>
    /// <param name="confidenceLevel">Уровень доверия.</param>
    /// <param name="horizonDays">Горизонт оценки в торговых днях.</param>
    /// <param name="portfolioValue">Стоимость позиции.</param>
    /// <param name="scenarioCount">Число сценариев для метода Монте-Карло.</param>
    Task<VarComparisonResult> CompareMethodsAsync(
        int instrumentId,
        DateOnly from,
        DateOnly to,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        double portfolioValue = 1_000_000.0,
        int scenarioCount = 100_000,
        CancellationToken cancellationToken = default);
}
