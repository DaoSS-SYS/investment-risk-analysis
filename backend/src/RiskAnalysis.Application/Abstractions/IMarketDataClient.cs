using RiskAnalysis.Application.Models;

namespace RiskAnalysis.Application.Abstractions;

/// <summary>
/// Источник рыночных данных. Абстракция введена для того, чтобы слой приложения
/// не зависел от конкретной биржи: реализация для Московской Биржи может быть
/// заменена или дополнена другим источником без изменения расчётной части.
/// </summary>
public interface IMarketDataClient
{
    /// <summary>Наименование источника, фиксируемое в журнале загрузки.</summary>
    string SourceName { get; }

    /// <summary>
    /// Поиск финансовых инструментов в справочнике биржи по строке запроса
    /// (биржевому коду или части наименования).
    /// </summary>
    Task<IReadOnlyList<SecurityInfo>> SearchSecuritiesAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получение истории дневных котировок инструмента за указанный период.
    /// Реализация обязана самостоятельно обрабатывать постраничную выдачу
    /// источника и возвращать записи, упорядоченные по дате торгов.
    /// </summary>
    Task<IReadOnlyList<MarketQuote>> GetHistoryAsync(
        string engine,
        string market,
        string board,
        string ticker,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
