namespace RiskAnalysis.RiskEngine.Returns;

/// <summary>
/// Наблюдение ценового ряда. Собственный тип расчётного ядра: ядро не зависит
/// от сущностей предметной области и оперирует только числовыми данными.
/// </summary>
/// <param name="Date">Дата наблюдения.</param>
/// <param name="Price">Цена, скорректированная на корпоративные действия.</param>
public readonly record struct PriceObservation(DateOnly Date, double Price);

/// <summary>
/// Наблюдение ряда доходностей.
/// </summary>
/// <param name="Date">Дата окончания периода, за который рассчитана доходность.</param>
/// <param name="Value">Значение доходности в долях единицы.</param>
public readonly record struct ReturnObservation(DateOnly Date, double Value);

/// <summary>Способ расчёта доходности.</summary>
public enum ReturnType
{
    /// <summary>
    /// Простая (арифметическая) доходность: r = P(t) / P(t-1) − 1.
    /// Складывается по инструментам портфеля, но не складывается по времени.
    /// </summary>
    Simple = 1,

    /// <summary>
    /// Логарифмическая (непрерывно начисляемая) доходность: r = ln(P(t) / P(t-1)).
    /// Складывается по времени, но не складывается по инструментам портфеля.
    /// </summary>
    Logarithmic = 2
}

/// <summary>Периодичность расчёта доходности.</summary>
public enum ReturnFrequency
{
    /// <summary>Дневная доходность по данным каждого торгового дня.</summary>
    Daily = 1,

    /// <summary>Недельная доходность по цене последнего торгового дня недели.</summary>
    Weekly = 2,

    /// <summary>Месячная доходность по цене последнего торгового дня месяца.</summary>
    Monthly = 3
}
