namespace RiskAnalysis.Domain.Entities;

/// <summary>
/// Инвестиционный портфель — совокупность позиций по финансовым инструментам,
/// выступающая объектом количественной оценки риска.
/// </summary>
public class Portfolio
{
    public int Id { get; set; }

    /// <summary>
    /// Владелец портфеля. Заполняется после подключения подсистемы аутентификации;
    /// до этого момента портфели являются общими.
    /// </summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>Наименование портфеля.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Описание портфеля или его инвестиционной стратегии.</summary>
    public string? Description { get; set; }

    /// <summary>Валюта, в которой производится оценка портфеля.</summary>
    public string BaseCurrency { get; set; } = "RUB";

    /// <summary>
    /// Инструмент-бенчмарк для расчёта коэффициента бета, альфы Йенсена
    /// и информационного коэффициента. Как правило — индекс МосБиржи (IMOEX).
    /// </summary>
    public int? BenchmarkInstrumentId { get; set; }
    public Instrument? BenchmarkInstrument { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Position> Positions { get; set; } = new List<Position>();
    public ICollection<RiskCalculation> Calculations { get; set; } = new List<RiskCalculation>();
}
