using RiskAnalysis.Application.Models;

namespace RiskAnalysis.Application.Abstractions;

/// <summary>
/// Источник макроэкономических показателей.
/// </summary>
public interface IMacroDataClient
{
    /// <summary>Наименование источника, фиксируемое в журнале загрузки.</summary>
    string SourceName { get; }

    /// <summary>
    /// Получает значения ключевой ставки Банка России за период.
    /// Ключевая ставка применяется в качестве безрисковой ставки.
    /// </summary>
    Task<IReadOnlyList<MacroObservation>> GetKeyRateAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
