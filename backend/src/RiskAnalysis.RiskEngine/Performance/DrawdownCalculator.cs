using RiskAnalysis.RiskEngine.Returns;

namespace RiskAnalysis.RiskEngine.Performance;

/// <summary>Точка кривой просадки.</summary>
/// <param name="Date">Дата.</param>
/// <param name="Value">Значение накопленной стоимости.</param>
/// <param name="Peak">Достигнутый к этой дате максимум стоимости.</param>
/// <param name="Drawdown">
/// Относительная просадка в долях единицы: отклонение текущей стоимости
/// от достигнутого максимума. Неотрицательная величина.
/// </param>
public readonly record struct DrawdownPoint(DateOnly Date, double Value, double Peak, double Drawdown);

/// <summary>
/// Результат анализа просадок.
/// </summary>
/// <param name="MaxDrawdown">
/// Максимальная просадка в долях единицы. Значение 0,5 означает снижение
/// стоимости вложения вдвое относительно достигнутого максимума.
/// </param>
/// <param name="PeakDate">Дата максимума, предшествовавшего наибольшей просадке.</param>
/// <param name="TroughDate">Дата наименьшей стоимости в ходе наибольшей просадки.</param>
/// <param name="RecoveryDate">
/// Дата восстановления стоимости до предшествовавшего максимума.
/// Не определена, если восстановление не произошло до конца периода.
/// </param>
/// <param name="DeclineTradingDays">Число торговых дней от максимума до минимума.</param>
/// <param name="RecoveryTradingDays">
/// Число торговых дней от минимума до восстановления. Равно минус единице,
/// если восстановление не произошло.
/// </param>
/// <param name="CurrentDrawdown">Просадка на конец рассматриваемого периода.</param>
/// <param name="Series">Кривая просадки. Заполняется по запросу.</param>
public sealed record DrawdownResult(
    double MaxDrawdown,
    DateOnly PeakDate,
    DateOnly TroughDate,
    DateOnly? RecoveryDate,
    int DeclineTradingDays,
    int RecoveryTradingDays,
    double CurrentDrawdown,
    IReadOnlyList<DrawdownPoint> Series);

/// <summary>
/// Расчёт просадок накопленной стоимости вложения.
///
/// Просадка представляет собой относительное отклонение текущей стоимости
/// вложения от ранее достигнутого максимума:
///
///     DD(t) = (max{V(s) : s ≤ t} − V(t)) / max{V(s) : s ≤ t}.
///
/// Максимальная просадка — наибольшее значение этой величины за период —
/// характеризует риск иначе, чем среднеквадратическое отклонение: она
/// показывает фактический наибольший убыток, который понёс бы инвестор,
/// приобретя актив в наименее удачный момент периода.
///
/// В отличие от стоимостной меры риска, относящейся к заданному горизонту,
/// максимальная просадка описывает риск на всём протяжении владения активом
/// и потому служит естественным дополнением к стоимостной мере риска.
/// </summary>
public static class DrawdownCalculator
{
    /// <summary>
    /// Рассчитывает просадки по ценовому ряду.
    /// </summary>
    /// <param name="prices">Ценовой ряд, упорядоченный по возрастанию даты.</param>
    /// <param name="includeSeries">Включать ли в результат кривую просадки.</param>
    public static DrawdownResult Calculate(
        IReadOnlyList<PriceObservation> prices,
        bool includeSeries = false)
    {
        ArgumentNullException.ThrowIfNull(prices);

        if (prices.Count < 2)
        {
            throw new ArgumentException(
                "Для расчёта просадок требуется не менее двух наблюдений.", nameof(prices));
        }

        var series = includeSeries ? new List<DrawdownPoint>(prices.Count) : null;

        var peak = prices[0].Price;
        var peakIndex = 0;

        var maxDrawdown = 0.0;
        var maxPeakIndex = 0;
        var maxTroughIndex = 0;

        for (var i = 0; i < prices.Count; i++)
        {
            var value = prices[i].Price;

            if (value > peak)
            {
                peak = value;
                peakIndex = i;
            }

            var drawdown = peak > 0 ? (peak - value) / peak : 0.0;

            if (drawdown > maxDrawdown)
            {
                maxDrawdown = drawdown;
                maxPeakIndex = peakIndex;
                maxTroughIndex = i;
            }

            series?.Add(new DrawdownPoint(prices[i].Date, value, peak, drawdown));
        }

        // Дата восстановления: первый торговый день после минимума,
        // когда стоимость вновь достигла предшествовавшего максимума.
        var peakValue = prices[maxPeakIndex].Price;
        int? recoveryIndex = null;

        for (var i = maxTroughIndex + 1; i < prices.Count; i++)
        {
            if (prices[i].Price >= peakValue)
            {
                recoveryIndex = i;
                break;
            }
        }

        var currentPeak = prices.Max(p => p.Price);
        var currentDrawdown = currentPeak > 0
            ? (currentPeak - prices[^1].Price) / currentPeak
            : 0.0;

        return new DrawdownResult(
            MaxDrawdown: maxDrawdown,
            PeakDate: prices[maxPeakIndex].Date,
            TroughDate: prices[maxTroughIndex].Date,
            RecoveryDate: recoveryIndex is null ? null : prices[recoveryIndex.Value].Date,
            DeclineTradingDays: maxTroughIndex - maxPeakIndex,
            RecoveryTradingDays: recoveryIndex is null ? -1 : recoveryIndex.Value - maxTroughIndex,
            CurrentDrawdown: currentDrawdown,
            Series: series ?? []);
    }
}
