namespace RiskAnalysis.RiskEngine.Var;

/// <summary>Метод оценки стоимостной меры риска.</summary>
public enum VarMethod
{
    /// <summary>
    /// Параметрический (дельта-нормальный) метод. Исходит из предположения
    /// о нормальном распределении доходностей.
    /// </summary>
    Parametric = 1,

    /// <summary>
    /// Метод исторического моделирования. Использует эмпирическое
    /// распределение доходностей без предположений о его виде.
    /// </summary>
    Historical = 2,

    /// <summary>
    /// Метод Монте-Карло. Оценка строится по множеству смоделированных
    /// сценариев изменения стоимости портфеля.
    /// </summary>
    MonteCarlo = 3,

    /// <summary>
    /// Параметрический метод с экспоненциально взвешенной оценкой
    /// волатильности (EWMA). Оценка волатильности зависит от недавних
    /// наблюдений и реагирует на изменение рыночных условий немедленно.
    /// </summary>
    EwmaParametric = 4,

    /// <summary>
    /// Параметрический метод с оценкой волатильности по модели GARCH(1,1).
    /// В отличие от экспоненциально взвешенной оценки обладает свойством
    /// возврата к долгосрочному уровню волатильности.
    /// </summary>
    GarchParametric = 5,

    /// <summary>
    /// Фильтрованное историческое моделирование: квантиль оценивается по
    /// эмпирическому распределению доходностей, стандартизованных на условную
    /// волатильность, и масштабируется её прогнозом. Учитывает одновременно
    /// изменчивость волатильности и отклонение распределения доходностей
    /// от нормального.
    /// </summary>
    FilteredHistorical = 6
}

/// <summary>
/// Закон распределения, применяемый при моделировании методом Монте-Карло.
/// </summary>
public enum SimulationDistribution
{
    /// <summary>
    /// Нормальное распределение. Даёт оценку, асимптотически совпадающую
    /// с параметрическим методом, и служит для его проверки.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Распределение Стьюдента с заданным числом степеней свободы,
    /// нормированное на единичную дисперсию. Воспроизводит «тяжёлые хвосты»
    /// фактического распределения доходностей.
    /// </summary>
    StudentT = 2,

    /// <summary>
    /// Историческая бутстрэп-выборка: сценарии формируются случайным выбором
    /// из фактически наблюдавшихся доходностей. Сохраняет как форму
    /// распределения, так и взаимосвязи между инструментами, не требуя
    /// предположений о законе распределения.
    /// </summary>
    HistoricalBootstrap = 3
}

/// <summary>
/// Результат оценки стоимостной меры риска.
/// </summary>
/// <param name="Method">Применённый метод.</param>
/// <param name="ConfidenceLevel">Уровень доверия.</param>
/// <param name="HorizonDays">Горизонт оценки в торговых днях.</param>
/// <param name="ValueAtRiskRelative">
/// Стоимостная мера риска в долях стоимости портфеля. Положительное значение
/// означает потерю.
/// </param>
/// <param name="ExpectedShortfallRelative">
/// Ожидаемые потери (условная стоимостная мера риска) в долях стоимости
/// портфеля — средняя величина потерь при условии превышения стоимостной
/// меры риска.
/// </param>
/// <param name="ValueAtRiskAbsolute">Стоимостная мера риска в денежном выражении.</param>
/// <param name="ExpectedShortfallAbsolute">Ожидаемые потери в денежном выражении.</param>
/// <param name="PortfolioValue">Стоимость портфеля, принятая при расчёте.</param>
/// <param name="ObservationCount">
/// Число наблюдений выборки либо число смоделированных сценариев.
/// </param>
/// <param name="Mean">Средняя доходность на горизонте оценки.</param>
/// <param name="StandardDeviation">Среднеквадратическое отклонение на горизонте оценки.</param>
/// <param name="Description">Пояснение к полученной оценке.</param>
public sealed record VarResult(
    VarMethod Method,
    double ConfidenceLevel,
    int HorizonDays,
    double ValueAtRiskRelative,
    double ExpectedShortfallRelative,
    double ValueAtRiskAbsolute,
    double ExpectedShortfallAbsolute,
    double PortfolioValue,
    int ObservationCount,
    double Mean,
    double StandardDeviation,
    string Description);

/// <summary>
/// Общие правила расчёта стоимостной меры риска.
/// </summary>
public static class VarConventions
{
    /// <summary>
    /// Проверяет допустимость уровня доверия. Применяемые на практике уровни —
    /// 0,95 и 0,99; последний предусмотрен требованиями Базельского комитета
    /// к расчёту рыночного риска.
    /// </summary>
    public static void ValidateConfidenceLevel(double confidenceLevel)
    {
        if (confidenceLevel is <= 0.5 or >= 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidenceLevel),
                "Уровень доверия должен принадлежать интервалу от 0,5 до 1. " +
                "На практике применяются значения 0,95 и 0,99.");
        }
    }

    /// <summary>Проверяет допустимость горизонта оценки.</summary>
    public static void ValidateHorizon(int horizonDays)
    {
        if (horizonDays < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(horizonDays),
                "Горизонт оценки должен составлять не менее одного торгового дня.");
        }
    }

    /// <summary>Проверяет допустимость стоимости портфеля.</summary>
    public static void ValidatePortfolioValue(double portfolioValue)
    {
        if (portfolioValue <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(portfolioValue),
                "Стоимость портфеля должна быть положительной.");
        }
    }
}
