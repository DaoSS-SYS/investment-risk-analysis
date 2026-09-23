using System.Globalization;

namespace RiskAnalysis.RiskEngine.Returns;

/// <summary>
/// Расчёт доходностей финансового инструмента по ценовому ряду.
///
/// Доходность является исходной величиной для всех методов количественной
/// оценки риска: по ряду доходностей оцениваются волатильность, стоимостная
/// мера риска VaR, ожидаемые потери, ковариационная матрица портфеля
/// и коэффициенты эффективности.
///
/// Выбор между простой и логарифмической доходностью определяется решаемой
/// задачей:
///
/// — логарифмическая доходность обладает свойством аддитивности по времени:
///   сумма дневных логарифмических доходностей за период в точности равна
///   логарифмической доходности за весь период. Это свойство используется при
///   пересчёте оценок риска на горизонт в несколько дней по правилу корня
///   из времени;
///
/// — простая доходность обладает свойством аддитивности по инструментам:
///   доходность портфеля равна сумме доходностей его составляющих, взвешенных
///   по их долям. Логарифмическая доходность этим свойством не обладает.
///
/// В расчётах риска по отдельному инструменту применяется логарифмическая
/// доходность; при агрегировании доходностей в портфель используется простая.
/// </summary>
public static class ReturnCalculator
{
    /// <summary>
    /// Число торговых дней в году, принимаемое для пересчёта показателей
    /// в годовое выражение. Значение 252 соответствует сложившейся практике
    /// и приблизительно равно фактическому числу торговых дней на Московской
    /// Бирже: за период с 2016 по 2026 год оно составляет в среднем 251 день.
    /// </summary>
    public const int TradingDaysPerYear = 252;

    /// <summary>Число торговых недель в году.</summary>
    public const int TradingWeeksPerYear = 52;

    /// <summary>Число месяцев в году.</summary>
    public const int MonthsPerYear = 12;

    /// <summary>
    /// Возвращает число периодов в году для заданной периодичности.
    /// Применяется при пересчёте показателей в годовое выражение.
    /// </summary>
    public static int PeriodsPerYear(ReturnFrequency frequency) => frequency switch
    {
        ReturnFrequency.Daily => TradingDaysPerYear,
        ReturnFrequency.Weekly => TradingWeeksPerYear,
        ReturnFrequency.Monthly => MonthsPerYear,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency))
    };

    /// <summary>
    /// Рассчитывает ряд доходностей по ценовому ряду.
    /// </summary>
    /// <param name="prices">
    /// Ценовой ряд, упорядоченный по возрастанию даты. Цены должны быть
    /// положительными и скорректированными на корпоративные действия.
    /// </param>
    /// <param name="returnType">Способ расчёта доходности.</param>
    /// <param name="frequency">
    /// Периодичность. При периодичности, отличной от дневной, ценовой ряд
    /// предварительно приводится к требуемой периодичности.
    /// </param>
    /// <returns>
    /// Ряд доходностей. Его длина на единицу меньше длины ценового ряда:
    /// для первого наблюдения доходность не определена.
    /// </returns>
    public static ReturnObservation[] Calculate(
        IReadOnlyList<PriceObservation> prices,
        ReturnType returnType = ReturnType.Logarithmic,
        ReturnFrequency frequency = ReturnFrequency.Daily)
    {
        ArgumentNullException.ThrowIfNull(prices);

        var series = frequency == ReturnFrequency.Daily
            ? prices
            : Resample(prices, frequency);

        if (series.Count < 2)
        {
            return [];
        }

        var result = new ReturnObservation[series.Count - 1];

        for (var i = 1; i < series.Count; i++)
        {
            var previous = series[i - 1].Price;
            var current = series[i].Price;

            if (previous <= 0 || current <= 0)
            {
                throw new ArgumentException(
                    "Ценовой ряд содержит неположительную цену на дату " +
                    series[i].Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) +
                    ". Расчёт доходности невозможен.",
                    nameof(prices));
            }

            var value = returnType == ReturnType.Logarithmic
                ? Math.Log(current / previous)
                : current / previous - 1.0;

            result[i - 1] = new ReturnObservation(series[i].Date, value);
        }

        return result;
    }

    /// <summary>
    /// Приводит дневной ценовой ряд к недельной или месячной периодичности.
    ///
    /// В качестве цены периода принимается цена последнего торгового дня
    /// периода — способ, принятый при построении рядов доходностей меньшей
    /// частоты. Неполные периоды на границах выборки сохраняются: их
    /// исключение сократило бы и без того ограниченный объём месячных
    /// наблюдений.
    /// </summary>
    public static IReadOnlyList<PriceObservation> Resample(
        IReadOnlyList<PriceObservation> prices,
        ReturnFrequency frequency)
    {
        ArgumentNullException.ThrowIfNull(prices);

        if (frequency == ReturnFrequency.Daily || prices.Count == 0)
        {
            return prices;
        }

        var result = new List<PriceObservation>();

        for (var i = 0; i < prices.Count; i++)
        {
            var isLastInPeriod =
                i == prices.Count - 1 ||
                !SamePeriod(prices[i].Date, prices[i + 1].Date, frequency);

            if (isLastInPeriod)
            {
                result.Add(prices[i]);
            }
        }

        return result;
    }

    /// <summary>Определяет, относятся ли две даты к одному периоду.</summary>
    private static bool SamePeriod(DateOnly first, DateOnly second, ReturnFrequency frequency)
    {
        return frequency switch
        {
            ReturnFrequency.Weekly => IsoWeekKey(first) == IsoWeekKey(second),
            ReturnFrequency.Monthly => first.Year == second.Year && first.Month == second.Month,
            _ => true
        };
    }

    /// <summary>
    /// Возвращает ключ недели по стандарту ISO 8601. Стандарт применяется,
    /// поскольку определяет принадлежность недель, приходящихся на границу
    /// календарных годов, однозначным образом.
    /// </summary>
    private static (int Year, int Week) IsoWeekKey(DateOnly date)
    {
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        var week = System.Globalization.ISOWeek.GetWeekOfYear(dateTime);
        var year = System.Globalization.ISOWeek.GetYear(dateTime);

        return (year, week);
    }

    /// <summary>
    /// Преобразует логарифмическую доходность в простую: r = exp(R) − 1.
    /// </summary>
    public static double ToSimple(double logarithmicReturn) => Math.Exp(logarithmicReturn) - 1.0;

    /// <summary>
    /// Преобразует простую доходность в логарифмическую: R = ln(1 + r).
    /// </summary>
    public static double ToLogarithmic(double simpleReturn)
    {
        if (simpleReturn <= -1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(simpleReturn),
                "Простая доходность не может быть меньше или равна минус единице.");
        }

        return Math.Log(1.0 + simpleReturn);
    }

    /// <summary>Извлекает значения доходностей в виде массива.</summary>
    public static double[] Values(IReadOnlyList<ReturnObservation> returns)
    {
        ArgumentNullException.ThrowIfNull(returns);

        var values = new double[returns.Count];

        for (var i = 0; i < returns.Count; i++)
        {
            values[i] = returns[i].Value;
        }

        return values;
    }
}
