using RiskAnalysis.Application.Models;

namespace RiskAnalysis.Application.Abstractions;

/// <summary>
/// Управление инвестиционными портфелями и их позициями.
/// </summary>
public interface IPortfolioService
{
    /// <summary>Возвращает перечень портфелей с текущей оценкой.</summary>
    Task<IReadOnlyList<PortfolioView>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Возвращает портфель с текущей оценкой позиций.</summary>
    Task<PortfolioView?> GetAsync(int portfolioId, CancellationToken cancellationToken = default);

    /// <summary>Создаёт портфель.</summary>
    Task<PortfolioView> CreateAsync(PortfolioRequest request, CancellationToken cancellationToken = default);

    /// <summary>Изменяет реквизиты портфеля.</summary>
    Task<PortfolioView?> UpdateAsync(
        int portfolioId, PortfolioRequest request, CancellationToken cancellationToken = default);

    /// <summary>Удаляет портфель вместе с позициями и историей расчётов.</summary>
    Task<bool> DeleteAsync(int portfolioId, CancellationToken cancellationToken = default);

    /// <summary>Добавляет позицию в портфель.</summary>
    Task<PortfolioView?> AddPositionAsync(
        int portfolioId, PositionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Удаляет позицию портфеля.</summary>
    Task<PortfolioView?> RemovePositionAsync(
        int portfolioId, int positionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Оценка риска инвестиционного портфеля.
/// </summary>
public interface IPortfolioRiskService
{
    /// <summary>
    /// Рассчитывает риск портфеля всеми реализованными методами и выполняет
    /// разложение риска по позициям.
    /// </summary>
    Task<PortfolioRiskReport> CalculateAsync(
        int portfolioId,
        RiskCalculationParameters parameters,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Очередь асинхронных расчётов.
///
/// Расчёт риска портфеля методом Монте-Карло на ста тысячах сценариев
/// занимает сотни миллисекунд, а при увеличении числа сценариев и размера
/// портфеля — секунды. Удержание соединения на всё время расчёта приводит
/// к истечению времени ожидания на стороне клиента, поэтому запрос
/// на выполнение расчёта принимается, ставится в очередь и выполняется
/// в фоновом режиме.
/// </summary>
public interface ICalculationQueue
{
    /// <summary>Ставит расчёт в очередь на выполнение.</summary>
    ValueTask EnqueueAsync(Guid calculationId, CancellationToken cancellationToken = default);

    /// <summary>Извлекает очередной расчёт. Ожидает поступления, если очередь пуста.</summary>
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);

    /// <summary>Текущее число расчётов в очереди.</summary>
    int Count { get; }
}

/// <summary>
/// Приём, выполнение и хранение асинхронных расчётов.
/// </summary>
public interface ICalculationService
{
    /// <summary>
    /// Принимает расчёт к выполнению: сохраняет запись с параметрами
    /// и ставит её в очередь. Возвращает идентификатор для последующего
    /// получения результата.
    /// </summary>
    Task<CalculationView> EnqueueAsync(
        int portfolioId,
        RiskCalculationParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>Возвращает состояние и результат расчёта.</summary>
    Task<CalculationView?> GetAsync(Guid calculationId, CancellationToken cancellationToken = default);

    /// <summary>Возвращает историю расчётов по портфелю.</summary>
    Task<IReadOnlyList<CalculationView>> GetHistoryAsync(
        int portfolioId, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполняет расчёт, ранее поставленный в очередь. Вызывается фоновой
    /// службой обработки очереди.
    /// </summary>
    Task ExecuteAsync(Guid calculationId, CancellationToken cancellationToken = default);
}
