using RiskAnalysis.Application.Models;

namespace RiskAnalysis.Application.Abstractions;

/// <summary>
/// Предобработка данных: выявление аномалий ценовых рядов и их классификация
/// на корпоративные действия и действительные рыночные события.
/// </summary>
public interface ICorporateActionService
{
    /// <summary>
    /// Проверяет ценовой ряд инструмента и сохраняет обнаруженные аномалии.
    /// Ранее сохранённые записи не перезаписываются: подтверждённая
    /// пользователем классификация имеет приоритет над автоматической.
    /// </summary>
    Task<AnomalyDetectionResult> DetectAsync(
        int instrumentId,
        CancellationToken cancellationToken = default);

    /// <summary>Проверяет ценовые ряды всех инструментов справочника.</summary>
    Task<IReadOnlyList<AnomalyDetectionResult>> DetectAllAsync(
        CancellationToken cancellationToken = default);
}
