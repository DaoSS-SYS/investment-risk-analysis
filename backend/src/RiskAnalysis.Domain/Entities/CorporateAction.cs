using RiskAnalysis.Domain.Enums;

namespace RiskAnalysis.Domain.Entities;

/// <summary>
/// Запись об аномалии ценового ряда инструмента и её классификации.
///
/// Биржа публикует котировки без корректировки на корпоративные действия.
/// Дробление акций снижает цену кратно, не меняя стоимости позиции инвестора,
/// однако в ценовом ряду выглядит как падение на десятки процентов. Без
/// корректировки такое значение искажает оценку волатильности и все
/// производные от неё показатели риска.
///
/// В таблице сохраняются все обнаруженные аномалии, включая признанные
/// действительными рыночными событиями: это документирует факт проверки
/// и обеспечивает воспроизводимость предобработки данных.
/// </summary>
public class CorporateAction
{
    public int Id { get; set; }

    public int InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    /// <summary>
    /// Дата, начиная с которой действует новая цена. Корректировке подлежат
    /// котировки, предшествующие этой дате.
    /// </summary>
    public DateOnly ActionDate { get; set; }

    /// <summary>Классификация аномалии.</summary>
    public CorporateActionType ActionType { get; set; }

    /// <summary>
    /// Коэффициент корпоративного действия. Для дробления 1 к 100 равен 100.
    /// Для рыночного события — фактическое отношение цен.
    /// </summary>
    public decimal Ratio { get; set; }

    /// <summary>
    /// Множитель, применяемый к котировкам, предшествующим дате события.
    /// Для дробления 1 к 100 равен 0,01; для рыночного события равен единице.
    /// </summary>
    public decimal AdjustmentFactor { get; set; }

    /// <summary>
    /// Фактическое отношение цены закрытия к цене закрытия предыдущего дня.
    /// Сохраняется как исходное наблюдение, на основании которого
    /// выполнена классификация.
    /// </summary>
    public decimal ObservedRatio { get; set; }

    /// <summary>Логарифмическая доходность дня аномалии до корректировки.</summary>
    public decimal ObservedLogReturn { get; set; }

    /// <summary>Способ выявления.</summary>
    public DetectionSource Source { get; set; }

    /// <summary>
    /// Признак подтверждения классификации пользователем. Автоматически
    /// выявленные записи требуют проверки перед применением.
    /// </summary>
    public bool IsConfirmed { get; set; }

    /// <summary>
    /// Признак применения корректировки при построении ценового ряда
    /// для расчётов. Для рыночных событий не устанавливается.
    /// </summary>
    public bool IsApplied { get; set; }

    /// <summary>Пояснение к классификации.</summary>
    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
