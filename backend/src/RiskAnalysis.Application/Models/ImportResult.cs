using RiskAnalysis.Domain.Enums;

namespace RiskAnalysis.Application.Models;

/// <summary>
/// Итог сеанса загрузки котировок из внешнего источника.
/// </summary>
/// <param name="InstrumentId">Инструмент, по которому выполнялась загрузка.</param>
/// <param name="Ticker">Биржевой код инструмента.</param>
/// <param name="DateFrom">Начало запрошенного периода.</param>
/// <param name="DateTo">Конец запрошенного периода.</param>
/// <param name="RowsReceived">Количество записей, полученных от источника.</param>
/// <param name="RowsInserted">Количество добавленных записей.</param>
/// <param name="RowsUpdated">Количество обновлённых записей.</param>
/// <param name="Status">Результат загрузки.</param>
/// <param name="Message">Диагностическое сообщение.</param>
/// <param name="DurationMs">Длительность загрузки в миллисекундах.</param>
public sealed record ImportResult(
    int InstrumentId,
    string Ticker,
    DateOnly DateFrom,
    DateOnly DateTo,
    int RowsReceived,
    int RowsInserted,
    int RowsUpdated,
    ImportStatus Status,
    string? Message,
    int DurationMs);
