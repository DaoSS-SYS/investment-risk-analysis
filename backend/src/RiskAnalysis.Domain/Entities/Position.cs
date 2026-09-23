namespace RiskAnalysis.Domain.Entities;

/// <summary>
/// Позиция портфеля — вложение в конкретный финансовый инструмент.
/// </summary>
public class Position
{
    public int Id { get; set; }

    public int PortfolioId { get; set; }
    public Portfolio? Portfolio { get; set; }

    public int InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    /// <summary>Количество единиц инструмента в позиции.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Цена приобретения единицы инструмента.</summary>
    public decimal PurchasePrice { get; set; }

    /// <summary>Дата приобретения позиции.</summary>
    public DateOnly PurchaseDate { get; set; }

    /// <summary>Произвольный комментарий инвестора.</summary>
    public string? Note { get; set; }
}
