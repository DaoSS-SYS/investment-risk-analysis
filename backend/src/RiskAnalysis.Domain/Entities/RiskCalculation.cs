using RiskAnalysis.Domain.Enums;

namespace RiskAnalysis.Domain.Entities;

/// <summary>
/// Сеанс расчёта над портфелем. Хранит параметры запуска и результат,
/// что обеспечивает воспроизводимость расчётов, их кэширование
/// и построение динамики риск-показателей во времени.
/// </summary>
public class RiskCalculation
{
    public Guid Id { get; set; }

    public int PortfolioId { get; set; }
    public Portfolio? Portfolio { get; set; }

    /// <summary>Вид выполняемого расчёта.</summary>
    public CalculationType CalculationType { get; set; }

    /// <summary>Состояние расчёта.</summary>
    public CalculationStatus Status { get; set; }

    /// <summary>
    /// Параметры запуска в формате JSON (уровень доверия, горизонт, глубина выборки,
    /// число сценариев Монте-Карло и т. п.). Состав параметров различается
    /// в зависимости от вида расчёта, поэтому хранится в неструктурированном виде
    /// в поле типа jsonb.
    /// </summary>
    public string ParametersJson { get; set; } = "{}";

    /// <summary>Результат расчёта в формате JSON. Заполняется после успешного завершения.</summary>
    public string? ResultJson { get; set; }

    /// <summary>Текст ошибки, если расчёт завершился неуспешно.</summary>
    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>
    /// Длительность расчёта в миллисекундах. Используется при оценке
    /// производительности системы.
    /// </summary>
    public int? DurationMs { get; set; }
}
