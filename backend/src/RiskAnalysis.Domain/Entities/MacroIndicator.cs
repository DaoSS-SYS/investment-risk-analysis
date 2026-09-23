using RiskAnalysis.Domain.Enums;

namespace RiskAnalysis.Domain.Entities;

/// <summary>
/// Значение макроэкономического показателя на дату.
/// Источник данных — открытые сервисы Банка России.
/// Ключевая ставка используется в качестве безрисковой ставки
/// при расчёте коэффициентов Шарпа, Сортино, Трейнора и модели CAPM.
/// </summary>
public class MacroIndicator
{
    public long Id { get; set; }

    /// <summary>Код показателя.</summary>
    public MacroIndicatorCode Code { get; set; }

    /// <summary>Дата, на которую действует значение показателя.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Значение показателя.</summary>
    public decimal Value { get; set; }
}
