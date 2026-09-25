using MathNet.Numerics.Distributions;
using RiskAnalysis.RiskEngine.Backtesting;
using RiskAnalysis.RiskEngine.Returns;
using RiskAnalysis.RiskEngine.StressTesting;
using RiskAnalysis.RiskEngine.Var;
using Xunit;

namespace RiskAnalysis.RiskEngine.Tests;

/// <summary>
/// Проверка корректности бэктестирования моделей оценки риска
/// и стресс-тестирования.
///
/// Эталонные значения критерия Купца получены аналитически. При 1000
/// наблюдениях, 10 нарушениях и ожидаемой доле 0,01 наблюдаемая доля в
/// точности совпадает с ожидаемой, поэтому статистика критерия равна нулю.
/// </summary>
public class BacktestTests
{
    private const double Tolerance = 1e-9;

    private static ReturnObservation[] Series(int count, double deviation, int shift = 0)
    {
        var start = new DateOnly(2016, 1, 4);
        var result = new ReturnObservation[count];

        var raw = new double[count];

        for (var i = 0; i < count; i++)
        {
            raw[i] = Normal.InvCDF(0.0, 1.0, (i + 0.5) / count);
        }

        for (var i = 0; i < count; i++)
        {
            result[i] = new ReturnObservation(
                start.AddDays(i),
                raw[(i * 7 + shift) % count] * deviation);
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // Критерий Купца
    // -----------------------------------------------------------------------

    [Fact]
    public void Статистика_Купца_равна_нулю_при_совпадении_долей()
    {
        // Наблюдаемая доля нарушений 10 / 1000 = 0,01 в точности равна
        // ожидаемой, поэтому отношение правдоподобий равно единице,
        // а статистика — нулю.
        var result = BacktestCalculator.KupiecTest(1000, 10, 0.01);

        Assert.Equal(0.0, result.Statistic, Tolerance);
        Assert.False(result.IsRejected);
    }

    [Fact]
    public void Критическое_значение_Купца_соответствует_табличному()
    {
        var result = BacktestCalculator.KupiecTest(1000, 10, 0.01);

        // Распределение хи-квадрат с одной степенью свободы, уровень 0,05.
        Assert.Equal(3.841, result.CriticalValue, 3);
    }

    [Fact]
    public void Статистика_Купца_соответствует_формуле()
    {
        const int observations = 1000;
        const int violations = 25;
        const double expected = 0.01;

        var result = BacktestCalculator.KupiecTest(observations, violations, expected);

        var observedRate = (double)violations / observations;

        var manual = -2.0 * (
            (observations - violations) * Math.Log(1.0 - expected) +
            violations * Math.Log(expected) -
            (observations - violations) * Math.Log(1.0 - observedRate) -
            violations * Math.Log(observedRate));

        Assert.Equal(manual, result.Statistic, 1e-10);
    }

    [Fact]
    public void Существенное_превышение_числа_нарушений_отвергает_модель()
    {
        // 30 нарушений на 1000 наблюдений при ожидаемых 10.
        var result = BacktestCalculator.KupiecTest(1000, 30, 0.01);

        Assert.True(result.IsRejected);
        Assert.True(result.Statistic > result.CriticalValue);
        Assert.Contains("занижает риск", result.Conclusion);
    }

    [Fact]
    public void Существенный_недостаток_нарушений_также_отвергает_модель()
    {
        // Критерий двусторонний: завышение оценки риска также означает
        // непригодность модели.
        var result = BacktestCalculator.KupiecTest(1000, 0, 0.01);

        Assert.True(result.IsRejected);
        Assert.Contains("завышает риск", result.Conclusion);
    }

    [Fact]
    public void Отсутствие_нарушений_не_приводит_к_ошибке_вычисления()
    {
        var result = BacktestCalculator.KupiecTest(200, 0, 0.01);

        Assert.False(double.IsNaN(result.Statistic));
        Assert.False(double.IsInfinity(result.Statistic));
    }

    // -----------------------------------------------------------------------
    // Критерий Кристоферсена
    // -----------------------------------------------------------------------

    [Fact]
    public void Независимость_не_отвергается_при_равномерных_нарушениях()
    {
        // Нарушения расставлены равномерно через равные промежутки:
        // ни одно нарушение не следует непосредственно за другим.
        var series = new List<BacktestPoint>();
        var date = new DateOnly(2020, 1, 1);

        for (var i = 0; i < 1000; i++)
        {
            series.Add(new BacktestPoint(date.AddDays(i), 0.0, 0.02, i % 100 == 0));
        }

        var result = BacktestCalculator.ChristoffersenTest(series, 0.0);

        Assert.False(result.IndependenceRejected);
        Assert.Equal(0, result.N11);
    }

    [Fact]
    public void Независимость_отвергается_при_группировании_нарушений()
    {
        // Все нарушения сосредоточены в одном непрерывном отрезке.
        var series = new List<BacktestPoint>();
        var date = new DateOnly(2020, 1, 1);

        for (var i = 0; i < 1000; i++)
        {
            series.Add(new BacktestPoint(date.AddDays(i), 0.0, 0.02, i >= 500 && i < 520));
        }

        var result = BacktestCalculator.ChristoffersenTest(series, 0.0);

        Assert.True(result.IndependenceRejected);
        Assert.True(result.N11 > 0);
    }

    [Fact]
    public void Статистика_условного_покрытия_равна_сумме_статистик()
    {
        var series = new List<BacktestPoint>();
        var date = new DateOnly(2020, 1, 1);

        for (var i = 0; i < 500; i++)
        {
            series.Add(new BacktestPoint(date.AddDays(i), 0.0, 0.02, i % 37 == 0));
        }

        const double kupiecStatistic = 2.5;

        var result = BacktestCalculator.ChristoffersenTest(series, kupiecStatistic);

        Assert.Equal(
            kupiecStatistic + result.IndependenceStatistic,
            result.ConditionalCoverageStatistic,
            Tolerance);
    }

    [Fact]
    public void Матрица_переходов_суммируется_в_число_переходов()
    {
        var series = new List<BacktestPoint>();
        var date = new DateOnly(2020, 1, 1);

        for (var i = 0; i < 300; i++)
        {
            series.Add(new BacktestPoint(date.AddDays(i), 0.0, 0.02, i % 11 == 0));
        }

        var result = BacktestCalculator.ChristoffersenTest(series, 0.0);

        Assert.Equal(299, result.N00 + result.N01 + result.N10 + result.N11);
    }

    // -----------------------------------------------------------------------
    // Подход Базельского комитета
    // -----------------------------------------------------------------------

    [Fact]
    public void Зоны_надзорной_оценки_соответствуют_установленным_границам()
    {
        // Для 250 наблюдений при уровне доверия 99 % Базельским комитетом
        // установлены границы: до 4 нарушений — зелёная зона,
        // от 5 до 9 — жёлтая, от 10 — красная.
        Assert.Equal(BaselZone.Green, BacktestCalculator.DetermineBaselZone(250, 4, 0.01));
        Assert.Equal(BaselZone.Yellow, BacktestCalculator.DetermineBaselZone(250, 5, 0.01));
        Assert.Equal(BaselZone.Yellow, BacktestCalculator.DetermineBaselZone(250, 9, 0.01));
        Assert.Equal(BaselZone.Red, BacktestCalculator.DetermineBaselZone(250, 10, 0.01));
    }

    [Fact]
    public void Надбавка_к_множителю_соответствует_шкале()
    {
        Assert.Equal(0.00, BacktestCalculator.CapitalAddOn(250, 4), Tolerance);
        Assert.Equal(0.40, BacktestCalculator.CapitalAddOn(250, 5), Tolerance);
        Assert.Equal(0.85, BacktestCalculator.CapitalAddOn(250, 9), Tolerance);
        Assert.Equal(1.00, BacktestCalculator.CapitalAddOn(250, 12), Tolerance);
    }

    // -----------------------------------------------------------------------
    // Бэктестирование в целом
    // -----------------------------------------------------------------------

    [Fact]
    public void Бэктест_проверяет_все_наблюдения_за_вычетом_окна()
    {
        var returns = Series(800, 0.015);

        var result = BacktestCalculator.Run(returns, VarMethod.Historical, 0.99, windowSize: 250);

        Assert.Equal(800 - 250, result.Observations);
        Assert.Equal(result.Observations, result.Series.Count);
    }

    [Fact]
    public void Оценка_строится_только_по_предшествующим_наблюдениям()
    {
        // Оценка для каждого дня должна совпадать с оценкой, рассчитанной
        // непосредственно по предшествующему окну. Совпадение подтверждает,
        // что будущие наблюдения в расчёт не попадают.
        var returns = Series(500, 0.02);

        var result = BacktestCalculator.Run(returns, VarMethod.Historical, 0.99, windowSize: 250);

        var window = returns.Skip(0).Take(250).Select(item => item.Value).ToArray();
        var expected = HistoricalVarCalculator.Calculate(window, 0.99).ValueAtRiskRelative;

        Assert.Equal(expected, result.Series[0].VarEstimate, 1e-12);
        Assert.Equal(returns[250].Date, result.Series[0].Date);
    }

    [Fact]
    public void Нарушение_фиксируется_при_превышении_убытком_оценки()
    {
        var returns = Series(600, 0.02);

        var result = BacktestCalculator.Run(returns, VarMethod.Historical, 0.99, windowSize: 250);

        foreach (var point in result.Series)
        {
            Assert.Equal(point.ActualReturn < -point.VarEstimate, point.IsViolation);
        }

        Assert.Equal(result.Series.Count(point => point.IsViolation), result.Violations);
    }

    [Fact]
    public void Недостаточная_длина_ряда_приводит_к_исключению()
    {
        var returns = Series(200, 0.02);

        var exception = Assert.Throws<ArgumentException>(
            () => BacktestCalculator.Run(returns, VarMethod.Historical, 0.99, windowSize: 250));

        Assert.Contains("требуется не менее", exception.Message);
    }

    // -----------------------------------------------------------------------
    // Стресс-тестирование
    // -----------------------------------------------------------------------

    [Fact]
    public void Гипотетический_сценарий_взвешивает_доходности_по_долям()
    {
        // Портфель из двух инструментов: снижение на 30 % и на 10 %
        // при долях 0,6 и 0,4 даёт потерю 0,6·(−0,3) + 0,4·(−0,1) = −0,22.
        var result = StressTestCalculator.ApplyHypothetical(
            "Проверка", [0.6, 0.4], [-0.30, -0.10],
            portfolioValue: 1_000_000.0, valueAtRisk: 50_000.0,
            description: "Эталонный расчёт");

        Assert.Equal(-0.22, result.PortfolioReturn, Tolerance);
        Assert.Equal(-220_000.0, result.LossAmount, 1e-6);
        Assert.Equal(780_000.0, result.ValueAfter, 1e-6);
    }

    [Fact]
    public void Отношение_потерь_к_мере_риска_рассчитано_верно()
    {
        var result = StressTestCalculator.ApplyHypothetical(
            "Проверка", [1.0], [-0.20],
            portfolioValue: 1_000_000.0, valueAtRisk: 50_000.0,
            description: "Эталонный расчёт");

        // Потеря 200 000 при мере риска 50 000 — превышение в четыре раза.
        Assert.Equal(4.0, result.LossToVarRatio, Tolerance);
    }

    [Fact]
    public void Вклады_позиций_суммируются_в_доходность_портфеля()
    {
        var result = StressTestCalculator.ApplyHypothetical(
            "Проверка", [0.5, 0.3, 0.2], [-0.25, -0.15, 0.05],
            portfolioValue: 2_000_000.0, valueAtRisk: 80_000.0,
            description: "Эталонный расчёт");

        Assert.Equal(
            result.PortfolioReturn,
            result.Impacts.Sum(impact => impact.Contribution),
            Tolerance);

        Assert.Equal(
            result.LossAmount,
            result.Impacts.Sum(impact => impact.LossAmount),
            1e-6);
    }

    [Fact]
    public void Исторический_сценарий_накапливает_доходности_за_период()
    {
        var dates = Enumerable.Range(0, 100)
            .Select(i => new DateOnly(2020, 1, 1).AddDays(i))
            .ToList();

        // Инструмент теряет по одному проценту в день на протяжении
        // пяти дней сценария.
        var returns = new double[100];

        for (var i = 20; i < 25; i++)
        {
            returns[i] = Math.Log(0.99);
        }

        var result = StressTestCalculator.ApplyHistorical(
            "Проверка", [1.0], [returns], dates,
            dates[20], dates[24], 1_000_000.0, 50_000.0);

        Assert.Equal(5, result.TradingDays);

        // Накопленное изменение: 0,99^5 − 1.
        Assert.Equal(Math.Pow(0.99, 5) - 1.0, result.PortfolioReturn, 1e-12);
    }

    [Fact]
    public void Периоды_наибольших_потерь_не_пересекаются()
    {
        var dates = Enumerable.Range(0, 400)
            .Select(i => new DateOnly(2020, 1, 1).AddDays(i))
            .ToList();

        var returns = new double[400];

        // Два обособленных периода потерь.
        for (var i = 100; i < 110; i++) returns[i] = Math.Log(0.97);
        for (var i = 300; i < 310; i++) returns[i] = Math.Log(0.96);

        var worst = StressTestCalculator.FindWorstPeriods(
            [1.0], [returns], dates, windowDays: 10, count: 2,
            portfolioValue: 1_000_000.0, valueAtRisk: 50_000.0);

        Assert.Equal(2, worst.Count);

        var first = worst[0];
        var second = worst[1];

        // Периоды не должны пересекаться.
        Assert.True(first.To < second.From || second.To < first.From);

        // Наибольшие потери — в периоде с более глубоким падением.
        Assert.True(first.PortfolioReturn < second.PortfolioReturn);
    }

    [Fact]
    public void Несуществующий_период_сценария_приводит_к_исключению()
    {
        var dates = Enumerable.Range(0, 50)
            .Select(i => new DateOnly(2020, 1, 1).AddDays(i))
            .ToList();

        var exception = Assert.Throws<ArgumentException>(
            () => StressTestCalculator.ApplyHistorical(
                "Проверка", [1.0], [new double[50]], dates,
                new DateOnly(2030, 1, 1), new DateOnly(2030, 2, 1),
                1_000_000.0, 50_000.0));

        Assert.Contains("нет наблюдений", exception.Message);
    }
}
