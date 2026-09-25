namespace RiskAnalysis.Application.Abstractions;

/// <summary>Сформированный отчёт.</summary>
/// <param name="FileName">Имя файла, предлагаемое пользователю.</param>
/// <param name="ContentType">Тип содержимого.</param>
/// <param name="Content">Содержимое файла.</param>
public sealed record GeneratedReport(string FileName, string ContentType, byte[] Content);

/// <summary>
/// Формирование отчётов по результатам анализа риска.
///
/// Отчёт объединяет сведения, рассчитываемые различными подсистемами:
/// состав портфеля с текущей переоценкой, оценки риска всеми реализованными
/// методами, разложение риска по позициям и результаты стресс-тестирования.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Формирует отчёт по риску портфеля в формате переносимого документа.
    /// Предназначен для представления результатов руководству и приобщения
    /// к документации.
    /// </summary>
    /// <param name="portfolioId">Идентификатор портфеля.</param>
    /// <param name="confidenceLevel">Уровень доверия.</param>
    /// <param name="horizonDays">Горизонт оценки в торговых днях.</param>
    /// <param name="includeStressTest">Включать ли результаты стресс-тестирования.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<GeneratedReport> BuildPortfolioPdfAsync(
        int portfolioId,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        bool includeStressTest = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Формирует отчёт по риску портфеля в формате электронной таблицы.
    /// Предназначен для дальнейшей обработки данных пользователем.
    /// </summary>
    Task<GeneratedReport> BuildPortfolioWorkbookAsync(
        int portfolioId,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        bool includeStressTest = true,
        CancellationToken cancellationToken = default);
}
