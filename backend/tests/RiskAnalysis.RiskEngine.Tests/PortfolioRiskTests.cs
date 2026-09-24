using MathNet.Numerics.Distributions;
using RiskAnalysis.RiskEngine.Statistics;
using RiskAnalysis.RiskEngine.Var;
using Xunit;

namespace RiskAnalysis.RiskEngine.Tests;

/// <summary>
/// Проверка корректности оценки риска портфеля и разложения риска
/// по инструментам.
///
/// Основной проверяемой величиной является тождество Эйлера: сумма
/// компонентных мер риска должна в точности равняться стоимостной мере риска
/// портфеля. Выполнение тождества служит доказательством корректности
/// разложения.
/// </summary>
public class PortfolioRiskTests
{
    private const double Tolerance = 1e-9;

    /// <summary>
    /// Детерминированный ряд доходностей: значения равны квантилям
    /// нормального распределения в равноотстоящих точках, переставленным
    /// заданным образом для получения требуемой взаимосвязи рядов.
    /// </summary>
    private static double[] Series(int count, double deviation, int shift)
    {
        var raw = new double[count];

        for (var i = 0; i < count; i++)
        {
            raw[i] = Normal.InvCDF(0.0, 1.0, (i + 0.5) / count);
        }

        var statistics = DescriptiveStatisticsCalculator.Calculate(raw);
        var result = new double[count];

        for (var i = 0; i < count; i++)
        {
            var source = raw[(i * 7 + shift) % count];
            result[i] = (source - statistics.Mean) / statistics.StandardDeviation * deviation;
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // Разложение риска по инструментам
    // -----------------------------------------------------------------------

    [Fact]
    public void Сумма_компонентных_мер_равна_риску_портфеля()
    {
        // Тождество Эйлера. Проверяется на портфеле из трёх инструментов
        // с различными долями и различной взаимосвязью рядов.
        double[][] returns =
        [
            Series(1000, 0.020, 0),
            Series(1000, 0.015, 13),
            Series(1000, 0.025, 47)
        ];

        double[] weights = [0.5, 0.3, 0.2];

        var means = CovarianceCalculator.Means(returns);
        var covariance = CovarianceCalculator.Covariance(returns);

        var contributions = ComponentVarCalculator.Decompose(
            weights, means, covariance,
            confidenceLevel: 0.99, horizonDays: 1, portfolioValue: 1_000_000.0);

        var portfolio = ParametricVarCalculator.Calculate(
            mean: weights.Select((w, i) => w * means[i]).Sum(),
            standardDeviation: CovarianceCalculator.PortfolioStandardDeviation(weights, covariance),
            observationCount: 1000,
            confidenceLevel: 0.99,
            horizonDays: 1,
            portfolioValue: 1_000_000.0);

        var sum = contributions.Sum(c => c.ComponentVar);

        Assert.Equal(portfolio.ValueAtRiskAbsolute, sum, 1e-6);
    }

    [Fact]
    public void Тождество_Эйлера_выполняется_на_разных_горизонтах_и_уровнях()
    {
        double[][] returns =
        [
            Series(800, 0.018, 3),
            Series(800, 0.022, 29)
        ];

        double[] weights = [0.6, 0.4];

        var means = CovarianceCalculator.Means(returns);
        var covariance = CovarianceCalculator.Covariance(returns);
        var deviation = CovarianceCalculator.PortfolioStandardDeviation(weights, covariance);
        var portfolioMean = weights.Select((w, i) => w * means[i]).Sum();

        foreach (var level in new[] { 0.90, 0.95, 0.99 })
        {
            foreach (var horizon in new[] { 1, 10 })
            {
                var contributions = ComponentVarCalculator.Decompose(
                    weights, means, covariance, level, horizon, 1_000_000.0);

                var expected = ParametricVarCalculator.Calculate(
                    portfolioMean, deviation, 800, level, horizon, 1_000_000.0);

                Assert.Equal(
                    expected.ValueAtRiskAbsolute,
                    contributions.Sum(c => c.ComponentVar),
                    1e-6);
            }
        }
    }

    [Fact]
    public void Доли_вклада_в_риск_суммируются_в_единицу()
    {
        double[][] returns =
        [
            Series(600, 0.02, 0),
            Series(600, 0.02, 11),
            Series(600, 0.02, 23),
            Series(600, 0.02, 37)
        ];

        double[] weights = [0.4, 0.3, 0.2, 0.1];

        var contributions = ComponentVarCalculator.Decompose(
            weights,
            CovarianceCalculator.Means(returns),
            CovarianceCalculator.Covariance(returns));

        Assert.Equal(1.0, contributions.Sum(c => c.ContributionShare), 1e-10);
    }

    [Fact]
    public void Вклад_совпадает_с_долей_при_одинаковых_инструментах()
    {
        // Если все инструменты имеют одинаковое распределение и полностью
        // коррелированы, диверсификация отсутствует, и вклад каждой позиции
        // в риск равен её доле в портфеле.
        var single = Series(500, 0.02, 0);

        double[][] returns = [single, single, single];
        double[] weights = [0.5, 0.3, 0.2];

        var contributions = ComponentVarCalculator.Decompose(
            weights,
            CovarianceCalculator.Means(returns),
            CovarianceCalculator.Covariance(returns));

        for (var i = 0; i < weights.Length; i++)
        {
            Assert.Equal(weights[i], contributions[i].ContributionShare, 1e-8);

            // При полной корреляции выигрыш от диверсификации отсутствует.
            Assert.Equal(0.0, contributions[i].DiversificationBenefit, 1e-8);
        }
    }

    [Fact]
    public void Слабо_связанная_позиция_имеет_выигрыш_от_диверсификации()
    {
        double[][] returns =
        [
            Series(1000, 0.02, 0),
            Series(1000, 0.02, 331)
        ];

        double[] weights = [0.5, 0.5];

        var contributions = ComponentVarCalculator.Decompose(
            weights,
            CovarianceCalculator.Means(returns),
            CovarianceCalculator.Covariance(returns),
            portfolioValue: 1_000_000.0);

        foreach (var contribution in contributions)
        {
            Assert.True(
                contribution.DiversificationBenefit > 0.0,
                "При неполной корреляции компонентная мера риска должна быть меньше " +
                "обособленной, то есть выигрыш от диверсификации положителен.");
        }
    }

    [Fact]
    public void Разложение_ожидаемых_потерь_суммируется_в_ожидаемые_потери()
    {
        // Для ожидаемых потерь тождество выполняется точно по построению.
        double[][] scenarios =
        [
            Series(2000, 0.02, 0),
            Series(2000, 0.018, 17),
            Series(2000, 0.030, 53)
        ];

        double[] weights = [0.45, 0.35, 0.20];

        var contributions = ComponentVarCalculator.DecomposeExpectedShortfall(
            weights, scenarios, confidenceLevel: 0.95, portfolioValue: 1_000_000.0);

        // Ожидаемые потери портфеля, рассчитанные непосредственно.
        var portfolioScenarios = new double[2000];

        for (var s = 0; s < 2000; s++)
        {
            double value = 0.0;

            for (var i = 0; i < weights.Length; i++)
            {
                value += weights[i] * scenarios[i][s];
            }

            portfolioScenarios[s] = value;
        }

        var quantile = DescriptiveStatisticsCalculator.Quantile(portfolioScenarios, 0.05);
        var tail = portfolioScenarios.Where(v => v <= quantile).ToList();
        var expectedShortfall = -tail.Average() * 1_000_000.0;

        Assert.Equal(expectedShortfall, contributions.Sum(c => c.ComponentVar), 1e-6);
    }

    // -----------------------------------------------------------------------
    // Риск портфеля в целом
    // -----------------------------------------------------------------------

    [Fact]
    public void Риск_портфеля_не_превышает_суммы_обособленных_рисков()
    {
        double[][] returns =
        [
            Series(1000, 0.020, 0),
            Series(1000, 0.025, 61),
            Series(1000, 0.018, 137)
        ];

        var result = PortfolioRiskCalculator.Calculate(
            [0.4, 0.35, 0.25], returns,
            confidenceLevel: 0.99, horizonDays: 1,
            portfolioValue: 1_000_000.0, scenarioCount: 20_000);

        var parametric = result.Estimates[0];

        Assert.True(parametric.ValueAtRiskAbsolute <= result.SumOfStandaloneVar);
        Assert.True(result.DiversificationEffect > 0.0);
    }

    [Fact]
    public void Портфель_из_одного_инструмента_отличается_на_преобразование_доходности()
    {
        // Оценка риска портфеля выполняется по простым доходностям, так как
        // только они аддитивны по составу портфеля; обособленная оценка
        // по инструменту — по логарифмическим. Поэтому результаты связаны
        // точным соотношением
        //
        //     VaR(прост.) = 1 − exp(−VaR(лог.)),
        //
        // вытекающим из того, что преобразование r → exp(r) − 1 монотонно
        // и потому сохраняет квантили. При типичной дневной волатильности
        // расхождение составляет около двух процентов.
        var single = Series(1000, 0.02, 0);

        var portfolio = PortfolioRiskCalculator.Calculate(
            [1.0], [single], confidenceLevel: 0.99,
            portfolioValue: 1_000_000.0, scenarioCount: 20_000);

        var standalone = HistoricalVarCalculator.Calculate(single, 0.99, 1, 1_000_000.0);

        var expected = 1.0 - Math.Exp(-standalone.ValueAtRiskRelative);

        Assert.Equal(expected, portfolio.Estimates[1].ValueAtRiskRelative, 1e-8);

        // Оценка по простым доходностям ниже: сумма ряда Тейлора
        // exp(−x) − 1 = −x + x²/2 − ... даёт положительную поправку.
        Assert.True(portfolio.Estimates[1].ValueAtRiskRelative < standalone.ValueAtRiskRelative);
    }

    [Fact]
    public void Некорректная_сумма_долей_приводит_к_исключению()
    {
        double[][] returns = [Series(200, 0.02, 0), Series(200, 0.02, 7)];

        var exception = Assert.Throws<ArgumentException>(
            () => PortfolioRiskCalculator.Calculate([0.5, 0.4], returns));

        Assert.Contains("Сумма долей", exception.Message);
    }

    [Fact]
    public void Ряды_различной_длины_приводят_к_исключению()
    {
        double[][] returns = [Series(200, 0.02, 0), Series(150, 0.02, 7)];

        Assert.Throws<ArgumentException>(
            () => PortfolioRiskCalculator.Calculate([0.5, 0.5], returns));
    }
}
