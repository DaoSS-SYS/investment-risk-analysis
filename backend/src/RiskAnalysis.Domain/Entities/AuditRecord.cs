using RiskAnalysis.Domain.Enums;

namespace RiskAnalysis.Domain.Entities;

/// <summary>
/// Запись журнала действий пользователей.
///
/// Журнал ведётся по действиям, изменяющим состояние системы. Его назначение
/// в предметной области состоит в том, чтобы при изменении оценки риска можно
/// было установить причину: изменение рыночных условий либо изменение состава
/// портфеля пользователем.
///
/// Наименование пользователя сохраняется в записи, а не выбирается по ссылке:
/// журнал должен оставаться читаемым и после того, как учётная запись
/// помечена недействующей.
/// </summary>
public class AuditRecord
{
    public long Id { get; set; }

    /// <summary>
    /// Идентификатор пользователя, выполнившего действие. Связь по внешнему
    /// ключу не устанавливается: слой предметной области не зависит
    /// от подсистемы удостоверения личности.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>Имя пользователя на момент совершения действия.</summary>
    public string UserName { get; set; } = null!;

    /// <summary>Вид действия.</summary>
    public AuditAction Action { get; set; }

    /// <summary>Вид объекта, к которому относится действие.</summary>
    public string EntityType { get; set; } = null!;

    /// <summary>Идентификатор объекта.</summary>
    public string? EntityId { get; set; }

    /// <summary>Описание действия на русском языке.</summary>
    public string Description { get; set; } = null!;

    /// <summary>Сетевой адрес, с которого выполнено действие.</summary>
    public string? IpAddress { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
