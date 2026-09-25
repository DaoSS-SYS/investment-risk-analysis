using RiskAnalysis.Application.Models;

namespace RiskAnalysis.Application.Abstractions;

/// <summary>
/// Бэктестирование моделей оценки стоимостной меры риска.
///
/// Проверка отвечает на вопрос о пригодности модели: согласуются ли её
/// оценки с фактически наблюдавшимися потерями.
/// </summary>
public interface IBacktestService
{
    /// <summary>
    /// Выполняет бэктестирование всех реализованных методов оценки риска
    /// по одному инструменту и сопоставляет результаты.
    /// </summary>
    /// <param name="instrumentId">Идентификатор инструмента.</param>
    /// <param name="from">Начало периода выборки.</param>
    /// <param name="to">Конец периода выборки.</param>
    /// <param name="confidenceLevel">Уровень доверия.</param>
    /// <param name="windowSize">Глубина скользящего окна оценивания.</param>
    /// <param name="scenarioCount">Число сценариев метода Монте-Карло.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<BacktestReport> RunAsync(
        int instrumentId,
        DateOnly from,
        DateOnly to,
        double confidenceLevel = 0.99,
        int windowSize = 250,
        int scenarioCount = 10_000,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Стресс-тестирование инвестиционного портфеля.
/// </summary>
public interface IStressTestService
{
    /// <summary>
    /// Применяет к портфелю набор стрессовых сценариев и сопоставляет
    /// полученные потери со стоимостной мерой риска.
    /// </summary>
    /// <param name="portfolioId">Идентификатор портфеля.</param>
    /// <param name="from">Начало периода выборки.</param>
    /// <param name="to">Конец периода выборки.</param>
    /// <param name="confidenceLevel">Уровень доверия для расчёта меры риска.</param>
    /// <param name="horizonDays">Горизонт оценки меры риска в торговых днях.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<StressTestReport> RunAsync(
        int portfolioId,
        DateOnly from,
        DateOnly to,
        double confidenceLevel = 0.99,
        int horizonDays = 10,
        CancellationToken cancellationToken = default);
}
