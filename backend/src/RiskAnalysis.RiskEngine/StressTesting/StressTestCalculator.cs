using RiskAnalysis.RiskEngine.Returns;

namespace RiskAnalysis.RiskEngine.StressTesting;

/// <summary>Вид стрессового сценария.</summary>
public enum StressScenarioKind
{
    /// <summary>
    /// Исторический сценарий: воспроизведение фактически наблюдавшихся
    /// доходностей за определённый период.
    /// </summary>
    Historical = 1,

    /// <summary>
    /// Гипотетический сценарий: задаваемое изменение стоимости инструментов,
    /// не наблюдавшееся в прошлом.
    /// </summary>
    Hypothetical = 2
}

/// <summary>Влияние сценария на отдельную позицию портфеля.</summary>
/// <param name="Index">Порядковый номер инструмента в портфеле.</param>
/// <param name="Weight">Доля инструмента в портфеле.</param>
/// <param name="InstrumentReturn">Доходность инструмента в сценарии.</param>
/// <param name="Contribution">Вклад позиции в доходность портфеля.</param>
/// <param name="LossAmount">Величина изменения стоимости позиции.</param>
public readonly record struct PositionImpact(
    int Index,
    double Weight,
    double InstrumentReturn,
    double Contribution,
    double LossAmount);

/// <summary>Результат применения стрессового сценария к портфелю.</summary>
/// <param name="Name">Наименование сценария.</param>
/// <param name="Kind">Вид сценария.</param>
/// <param name="From">Начало периода исторического сценария.</param>
/// <param name="To">Конец периода исторического сценария.</param>
/// <param name="TradingDays">Продолжительность сценария в торговых днях.</param>
/// <param name="PortfolioReturn">Доходность портфеля в сценарии.</param>
/// <param name="LossAmount">Изменение стоимости портфеля.</param>
/// <param name="ValueAfter">Стоимость портфеля после реализации сценария.</param>
/// <param name="Impacts">Влияние сценария на отдельные позиции.</param>
/// <param name="ValueAtRisk">
/// Стоимостная мера риска, с которой сопоставляется результат сценария.
/// </param>
/// <param name="LossToVarRatio">
/// Отношение потерь в сценарии к стоимостной мере риска. Превышение единицы
/// означает, что сценарий выходит за пределы, охватываемые оценкой риска.
/// </param>
/// <param name="Description">Пояснение к сценарию и результату.</param>
public sealed record StressScenarioResult(
    string Name,
    StressScenarioKind Kind,
    DateOnly? From,
    DateOnly? To,
    int TradingDays,
    double PortfolioReturn,
    double LossAmount,
    double ValueAfter,
    IReadOnlyList<PositionImpact> Impacts,
    double ValueAtRisk,
    double LossToVarRatio,
    string Description);

/// <summary>
/// Стресс-тестирование инвестиционного портфеля.
///
/// Стоимостная мера риска отвечает на вопрос о величине потерь, которые не
/// будут превышены с заданной вероятностью. Она ничего не говорит о том,
/// что произойдёт в оставшихся случаях, и по построению не охватывает
/// события, находящиеся за пределами уровня доверия.
///
/// Стресс-тестирование отвечает на другой вопрос: какими окажутся потери при
/// реализации определённого неблагоприятного события. Вероятность события
/// при этом не оценивается — рассматриваются его последствия.
///
/// Применяются два вида сценариев:
///
/// — исторические, воспроизводящие фактически наблюдавшиеся изменения цен
///   за периоды рыночных потрясений. Их достоинство состоит в внутренней
///   согласованности: взаимосвязи между инструментами в таком сценарии
///   не постулируются, а берутся из наблюдений, включая свойственный
///   кризисам рост корреляций;
///
/// — гипотетические, задающие изменения цен, не наблюдавшиеся в прошлом.
///   Они позволяют проверить устойчивость портфеля к событиям, которых
///   не было в рассматриваемом периоде.
///
/// Ключевой показатель — отношение потерь в сценарии к стоимостной мере
/// риска. Оно количественно выражает, во сколько раз стрессовое событие
/// превосходит потери, охватываемые обычной оценкой риска.
/// </summary>
public static class StressTestCalculator
{
    /// <summary>
    /// Применяет исторический сценарий: воспроизводит доходности инструментов
    /// за указанный период применительно к текущей структуре портфеля.
    /// </summary>
    /// <param name="name">Наименование сценария.</param>
    /// <param name="weights">Вектор долей инструментов.</param>
    /// <param name="returns">
    /// Логарифмические доходности инструментов: первый индекс — инструмент,
    /// второй — наблюдение.
    /// </param>
    /// <param name="dates">Даты наблюдений.</param>
    /// <param name="from">Начало периода сценария.</param>
    /// <param name="to">Конец периода сценария.</param>
    /// <param name="portfolioValue">Стоимость портфеля.</param>
    /// <param name="valueAtRisk">Стоимостная мера риска для сопоставления.</param>
    public static StressScenarioResult ApplyHistorical(
        string name,
        IReadOnlyList<double> weights,
        IReadOnlyList<double[]> returns,
        IReadOnlyList<DateOnly> dates,
        DateOnly from,
        DateOnly to,
        double portfolioValue,
        double valueAtRisk)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(returns);
        ArgumentNullException.ThrowIfNull(dates);

        if (returns.Count != weights.Count)
        {
            throw new ArgumentException(
                "Число долей не соответствует числу рядов доходностей.", nameof(weights));
        }

        // Накопленная логарифмическая доходность каждого инструмента
        // за период сценария. Логарифмические доходности складываются
        // по времени, что и позволяет получить итог простым суммированием.
        var accumulated = new double[weights.Count];
        var tradingDays = 0;

        for (var t = 0; t < dates.Count; t++)
        {
            if (dates[t] < from || dates[t] > to)
            {
                continue;
            }

            tradingDays++;

            for (var i = 0; i < weights.Count; i++)
            {
                accumulated[i] += returns[i][t];
            }
        }

        if (tradingDays == 0)
        {
            throw new ArgumentException(
                $"За период с {from:dd.MM.yyyy} по {to:dd.MM.yyyy} нет наблюдений. " +
                "Проверьте полноту загруженной истории котировок.",
                nameof(from));
        }

        var instrumentReturns = accumulated
            .Select(value => Math.Exp(value) - 1.0)
            .ToArray();

        return Build(
            name, StressScenarioKind.Historical, from, to, tradingDays,
            weights, instrumentReturns, portfolioValue, valueAtRisk,
            $"Исторический сценарий: воспроизведение фактических изменений цен " +
            $"за период с {from:dd.MM.yyyy} по {to:dd.MM.yyyy} ({tradingDays} торг. дн.) " +
            "применительно к текущей структуре портфеля.");
    }

    /// <summary>
    /// Применяет гипотетический сценарий: задаваемые изменения стоимости
    /// инструментов.
    /// </summary>
    /// <param name="name">Наименование сценария.</param>
    /// <param name="weights">Вектор долей инструментов.</param>
    /// <param name="instrumentReturns">
    /// Простые доходности инструментов в сценарии, например −0,3 для снижения
    /// на тридцать процентов.
    /// </param>
    /// <param name="portfolioValue">Стоимость портфеля.</param>
    /// <param name="valueAtRisk">Стоимостная мера риска для сопоставления.</param>
    /// <param name="description">Пояснение к сценарию.</param>
    public static StressScenarioResult ApplyHypothetical(
        string name,
        IReadOnlyList<double> weights,
        IReadOnlyList<double> instrumentReturns,
        double portfolioValue,
        double valueAtRisk,
        string description)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(instrumentReturns);

        if (weights.Count != instrumentReturns.Count)
        {
            throw new ArgumentException(
                "Число долей не соответствует числу заданных доходностей.", nameof(weights));
        }

        return Build(
            name, StressScenarioKind.Hypothetical, null, null, 0,
            weights, instrumentReturns, portfolioValue, valueAtRisk, description);
    }

    /// <summary>
    /// Отыскивает периоды наибольших потерь портфеля заданной
    /// продолжительности.
    ///
    /// В отличие от сценариев, задаваемых по известным датам, эти периоды
    /// определяются из самих данных: рассматриваются все скользящие окна
    /// заданной длины, и отбираются те, в которых портфель понёс наибольшие
    /// потери. Отбираются непересекающиеся периоды, иначе результат
    /// состоял бы из сдвинутых на один день копий одного и того же периода.
    /// </summary>
    /// <param name="weights">Вектор долей инструментов.</param>
    /// <param name="returns">Логарифмические доходности инструментов.</param>
    /// <param name="dates">Даты наблюдений.</param>
    /// <param name="windowDays">Продолжительность окна в торговых днях.</param>
    /// <param name="count">Число отбираемых периодов.</param>
    /// <param name="portfolioValue">Стоимость портфеля.</param>
    /// <param name="valueAtRisk">Стоимостная мера риска для сопоставления.</param>
    public static IReadOnlyList<StressScenarioResult> FindWorstPeriods(
        IReadOnlyList<double> weights,
        IReadOnlyList<double[]> returns,
        IReadOnlyList<DateOnly> dates,
        int windowDays,
        int count,
        double portfolioValue,
        double valueAtRisk)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(returns);
        ArgumentNullException.ThrowIfNull(dates);

        if (windowDays < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(windowDays));
        }

        var observations = dates.Count;

        if (observations < windowDays)
        {
            return [];
        }

        // Доходность портфеля по дням. Переход к простым доходностям
        // необходим, поскольку только они аддитивны по составу портфеля.
        var portfolioDaily = new double[observations];

        for (var t = 0; t < observations; t++)
        {
            double value = 0.0;

            for (var i = 0; i < weights.Count; i++)
            {
                value += weights[i] * (Math.Exp(returns[i][t]) - 1.0);
            }

            portfolioDaily[t] = value;
        }

        // Накопленная доходность портфеля в каждом окне.
        var candidates = new List<(int Start, double Return)>(observations - windowDays + 1);

        for (var start = 0; start + windowDays <= observations; start++)
        {
            double cumulative = 1.0;

            for (var t = start; t < start + windowDays; t++)
            {
                cumulative *= 1.0 + portfolioDaily[t];
            }

            candidates.Add((start, cumulative - 1.0));
        }

        var selected = new List<(int Start, double Return)>();

        foreach (var candidate in candidates.OrderBy(item => item.Return))
        {
            if (selected.Count >= count)
            {
                break;
            }

            // Отбираются только непересекающиеся периоды.
            var overlaps = selected.Any(item => Math.Abs(item.Start - candidate.Start) < windowDays);

            if (!overlaps)
            {
                selected.Add(candidate);
            }
        }

        var results = new List<StressScenarioResult>(selected.Count);
        var ordinal = 1;

        foreach (var (start, _) in selected)
        {
            var from = dates[start];
            var to = dates[start + windowDays - 1];

            results.Add(ApplyHistorical(
                $"Период наибольших потерь № {ordinal} ({windowDays} торг. дн.)",
                weights, returns, dates, from, to, portfolioValue, valueAtRisk));

            ordinal++;
        }

        return results;
    }

    /// <summary>
    /// Формирует результат сценария по заданным доходностям инструментов.
    /// </summary>
    private static StressScenarioResult Build(
        string name,
        StressScenarioKind kind,
        DateOnly? from,
        DateOnly? to,
        int tradingDays,
        IReadOnlyList<double> weights,
        IReadOnlyList<double> instrumentReturns,
        double portfolioValue,
        double valueAtRisk,
        string description)
    {
        var impacts = new PositionImpact[weights.Count];

        double portfolioReturn = 0.0;

        for (var i = 0; i < weights.Count; i++)
        {
            var contribution = weights[i] * instrumentReturns[i];
            portfolioReturn += contribution;

            impacts[i] = new PositionImpact(
                Index: i,
                Weight: weights[i],
                InstrumentReturn: instrumentReturns[i],
                Contribution: contribution,
                LossAmount: contribution * portfolioValue);
        }

        var lossAmount = portfolioReturn * portfolioValue;

        return new StressScenarioResult(
            Name: name,
            Kind: kind,
            From: from,
            To: to,
            TradingDays: tradingDays,
            PortfolioReturn: portfolioReturn,
            LossAmount: lossAmount,
            ValueAfter: portfolioValue + lossAmount,
            Impacts: impacts,
            ValueAtRisk: valueAtRisk,
            LossToVarRatio: valueAtRisk > 0 ? -lossAmount / valueAtRisk : 0.0,
            Description: description);
    }

    /// <summary>
    /// Число торговых дней в году, применяемое при задании продолжительности
    /// сценариев.
    /// </summary>
    public const int TradingDaysPerYear = ReturnCalculator.TradingDaysPerYear;
}
