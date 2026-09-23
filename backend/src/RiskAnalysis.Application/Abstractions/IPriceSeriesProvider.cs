using RiskAnalysis.Application.Models;

namespace RiskAnalysis.Application.Abstractions;

/// <summary>
/// Поставщик подготовленных ценовых рядов.
///
/// Единственная точка, через которую расчётная часть системы получает цены.
/// Гарантирует, что во все расчёты попадают котировки, скорректированные
/// на корпоративные действия, тогда как исходные данные остаются неизменными.
/// </summary>
public interface IPriceSeriesProvider
{
    /// <summary>
    /// Возвращает ценовой ряд инструмента за указанный период
    /// с применёнными корректировками.
    /// </summary>
    Task<PriceSeries> GetSeriesAsync(
        int instrumentId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает ценовые ряды нескольких инструментов, приведённые к общему
    /// торговому календарю: в результат попадают только те даты, по которым
    /// имеются котировки всех запрошенных инструментов.
    ///
    /// Выравнивание календарей обязательно при расчёте ковариационной матрицы
    /// портфеля: несовпадение дат приводит к сопоставлению доходностей
    /// разных торговых дней и искажению оценок корреляции.
    /// </summary>
    Task<IReadOnlyList<PriceSeries>> GetAlignedSeriesAsync(
        IReadOnlyCollection<int> instrumentIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
