using RiskAnalysis.Domain.Enums;

namespace RiskAnalysis.Domain.Entities;

/// <summary>
/// Финансовый инструмент (ценная бумага, индекс, валютная пара),
/// по которому система хранит историю котировок.
/// </summary>
public class Instrument
{
    public int Id { get; set; }

    /// <summary>Биржевой код инструмента (SECID в терминологии Московской Биржи), например SBER.</summary>
    public string Ticker { get; set; } = null!;

    /// <summary>Краткое наименование инструмента.</summary>
    public string ShortName { get; set; } = null!;

    /// <summary>Полное наименование инструмента.</summary>
    public string? FullName { get; set; }

    /// <summary>Тип инструмента.</summary>
    public SecurityType SecurityType { get; set; }

    /// <summary>Торговая система Московской Биржи (engine), например stock.</summary>
    public string Engine { get; set; } = "stock";

    /// <summary>Рынок в составе торговой системы (market), например shares или index.</summary>
    public string Market { get; set; } = "shares";

    /// <summary>Режим торгов (board), например TQBR для основного режима торгов акциями.</summary>
    public string Board { get; set; } = "TQBR";

    /// <summary>Валюта номинала инструмента.</summary>
    public string Currency { get; set; } = "RUB";

    /// <summary>Международный идентификационный код ценной бумаги.</summary>
    public string? Isin { get; set; }

    /// <summary>Отрасль эмитента. Используется при анализе отраслевой концентрации портфеля.</summary>
    public string? Sector { get; set; }

    /// <summary>Размер лота в штуках.</summary>
    public int? LotSize { get; set; }

    /// <summary>Признак того, что инструмент участвует в автоматической загрузке котировок.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Дата самой ранней котировки, фактически загруженной в базу данных.</summary>
    public DateOnly? HistoryFrom { get; set; }

    /// <summary>Дата самой поздней котировки, фактически загруженной в базу данных.</summary>
    public DateOnly? HistoryTo { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>История котировок инструмента.</summary>
    public ICollection<Quote> Quotes { get; set; } = new List<Quote>();
}
