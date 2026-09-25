using RiskAnalysis.Application.Models;

namespace RiskAnalysis.Application.Abstractions;

/// <summary>
/// Оптимизация структуры инвестиционного портфеля по модели Марковица.
/// </summary>
public interface IPortfolioOptimizationService
{
    /// <summary>
    /// Строит эффективную границу, определяет портфель наименьшей дисперсии
    /// и касательный портфель, сопоставляет их с текущей структурой.
    /// </summary>
    /// <param name="portfolioId">Идентификатор портфеля.</param>
    /// <param name="from">Начало периода выборки.</param>
    /// <param name="to">Конец периода выборки.</param>
    /// <param name="maximumWeight">Предельная доля одного инструмента.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<OptimizationReport> OptimizeAsync(
        int portfolioId,
        DateOnly from,
        DateOnly to,
        double maximumWeight = 1.0,
        CancellationToken cancellationToken = default);
}
