using RiskAnalysis.Domain.Enums;

namespace RiskAnalysis.Domain.Entities;

/// <summary>
/// Журнал загрузки данных из внешних источников.
/// Обеспечивает контроль полноты исходных данных и диагностику отказов
/// внешних сервисов.
/// </summary>
public class ImportLog
{
    public long Id { get; set; }

    /// <summary>Источник данных: MOEX_ISS, CBR, CSV.</summary>
    public string Source { get; set; } = null!;

    /// <summary>Инструмент, по которому выполнялась загрузка (если применимо).</summary>
    public int? InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    /// <summary>Начало запрошенного периода.</summary>
    public DateOnly? DateFrom { get; set; }

    /// <summary>Конец запрошенного периода.</summary>
    public DateOnly? DateTo { get; set; }

    /// <summary>Количество записей, полученных от источника.</summary>
    public int RowsReceived { get; set; }

    /// <summary>Количество добавленных записей.</summary>
    public int RowsInserted { get; set; }

    /// <summary>Количество обновлённых записей.</summary>
    public int RowsUpdated { get; set; }

    public ImportStatus Status { get; set; }

    /// <summary>Диагностическое сообщение или текст ошибки.</summary>
    public string? Message { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }

    /// <summary>Длительность загрузки в миллисекундах.</summary>
    public int DurationMs { get; set; }
}
