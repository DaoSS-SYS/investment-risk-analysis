using RiskAnalysis.Application.Models;

namespace RiskAnalysis.Application.Abstractions;

/// <summary>
/// Подсистема загрузки котировок: получение данных от внешнего источника,
/// их сохранение в базе данных и журналирование результата.
/// </summary>
public interface IQuoteImportService
{
    /// <summary>
    /// Загружает историю котировок инструмента за указанный период.
    /// Операция идемпотентна: повторная загрузка того же периода не создаёт
    /// дубликатов, а обновляет ранее сохранённые записи.
    /// </summary>
    /// <param name="instrumentId">Идентификатор инструмента в справочнике системы.</param>
    /// <param name="from">Начало периода.</param>
    /// <param name="to">Конец периода.</param>
    Task<ImportResult> ImportQuotesAsync(
        int instrumentId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Догружает котировки по всем активным инструментам справочника
    /// с даты последней имеющейся котировки по указанную дату.
    /// Применяется при работе по расписанию.
    /// </summary>
    Task<IReadOnlyList<ImportResult>> ImportAllActiveAsync(
        DateOnly to,
        CancellationToken cancellationToken = default);
}
