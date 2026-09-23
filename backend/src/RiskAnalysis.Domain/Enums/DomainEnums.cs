namespace RiskAnalysis.Domain.Enums;

/// <summary>Тип финансового инструмента.</summary>
public enum SecurityType
{
    /// <summary>Акция.</summary>
    Share = 1,
    /// <summary>Облигация.</summary>
    Bond = 2,
    /// <summary>Биржевой индекс (используется как бенчмарк).</summary>
    Index = 3,
    /// <summary>Валютная пара.</summary>
    Currency = 4,
    /// <summary>Биржевой инвестиционный фонд.</summary>
    Etf = 5
}

/// <summary>Вид расчёта, выполняемого системой над портфелем.</summary>
public enum CalculationType
{
    /// <summary>Оценка риска: VaR, CVaR, коэффициенты эффективности.</summary>
    RiskAssessment = 1,
    /// <summary>Бэктестирование модели VaR (тесты Купца и Кристоферсена).</summary>
    Backtest = 2,
    /// <summary>Оптимизация структуры портфеля по модели Марковица.</summary>
    PortfolioOptimization = 3,
    /// <summary>Стресс-тестирование по историческим и гипотетическим сценариям.</summary>
    StressTest = 4
}

/// <summary>Состояние асинхронного расчёта.</summary>
public enum CalculationStatus
{
    /// <summary>Поставлен в очередь.</summary>
    Queued = 1,
    /// <summary>Выполняется.</summary>
    Running = 2,
    /// <summary>Завершён успешно.</summary>
    Succeeded = 3,
    /// <summary>Завершён с ошибкой.</summary>
    Failed = 4,
    /// <summary>Отменён пользователем.</summary>
    Cancelled = 5
}

/// <summary>Метод оценки Value at Risk.</summary>
public enum VarMethod
{
    /// <summary>Параметрический (дельта-нормальный) метод.</summary>
    Parametric = 1,
    /// <summary>Метод исторического моделирования.</summary>
    Historical = 2,
    /// <summary>Метод Монте-Карло.</summary>
    MonteCarlo = 3
}

/// <summary>Результат сеанса загрузки данных из внешнего источника.</summary>
public enum ImportStatus
{
    /// <summary>Загрузка выполнена полностью.</summary>
    Success = 1,
    /// <summary>Загрузка выполнена частично.</summary>
    PartialSuccess = 2,
    /// <summary>Загрузка не выполнена.</summary>
    Failed = 3
}

/// <summary>Код макроэкономического показателя (источник — Банк России).</summary>
public enum MacroIndicatorCode
{
    /// <summary>Ключевая ставка Банка России, % годовых. Используется как безрисковая ставка.</summary>
    KeyRate = 1,
    /// <summary>Официальный курс доллара США к рублю.</summary>
    UsdRub = 2,
    /// <summary>Официальный курс евро к рублю.</summary>
    EurRub = 3
}
