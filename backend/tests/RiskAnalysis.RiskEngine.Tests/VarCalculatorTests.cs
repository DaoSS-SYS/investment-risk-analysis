using MathNet.Numerics.Distributions;
using RiskAnalysis.RiskEngine.Statistics;
using RiskAnalysis.RiskEngine.Var;
using Xunit;

namespace RiskAnalysis.RiskEngine.Tests;

/// <summary>
/// Проверка корректности оценки стоимостной меры риска.
///
/// Эталонные значения параметрического метода получены аналитически:
/// при нулевом среднем и единичном среднеквадратическом отклонении
/// стоимостная мера риска уровня 0,99 равна квантилю стандартного нормального
/// распределения 2,326347874, а ожидаемые потери равны
/// φ(2,326347874) / 0,01 = 2,665214220.
/// </summary>
public class VarCalculatorTests
{
    private const double Tolerance = 1e-9;

    /// <summary>
    /// Детерминированная выборка с заданными средним и среднеквадратическим
    /// отклонением: значения равны квантилям нормального распределения
    /// в равноотстоящих точках.
    /// </summary>
    private static double[] NormalSample(int count, double mean = 0.0, double deviation = 1.0)
    {
        var raw = new double[count];

        for (var i = 0; i < count; i++)
        {
            raw[i] = Normal.InvCDF(0.0, 1.0, (i + 0.5) / count);
        }

        // Приведение к точно заданным выборочным среднему и отклонению,
        // чтобы эталонные значения вычислялись аналитически.
        var statistics = DescriptiveStatisticsCalculator.Calculate(raw);
        var sample = new double[count];

        for (var i = 0; i < count; i++)
        {
            sample[i] = (raw[i] - statistics.Mean) / statistics.StandardDeviation * deviation + mean;
        }

        return sample;
    }

    // -----------------------------------------------------------------------
    // Параметрический метод
    // -----------------------------------------------------------------------

    [Fact]
    public void Параметрический_VaR_равен_квантилю_нормального_распределения()
    {
        var result = ParametricVarCalculator.Calculate(
            mean: 0.0, standardDeviation: 1.0, observationCount: 1000,
            confidenceLevel: 0.99, horizonDays: 1, portfolioValue: 1.0);

        Assert.Equal(2.3263478740408408, result.ValueAtRiskRelative, 1e-10);
    }

    [Fact]
    public void Параметрические_ожидаемые_потери_равны_замкнутой_формуле()
    {
        var result = ParametricVarCalculator.Calculate(
            mean: 0.0, standardDeviation: 1.0, observationCount: 1000,
            confidenceLevel: 0.99);

        // φ(Φ⁻¹(0,99)) / (1 − 0,99)
        var expected = Normal.PDF(0.0, 1.0, Normal.InvCDF(0.0, 1.0, 0.99)) / 0.01;

        Assert.Equal(expected, result.ExpectedShortfallRelative, 1e-10);
        Assert.Equal(2.6652142204773979, result.ExpectedShortfallRelative, 1e-9);
    }

    [Fact]
    public void Параметрический_VaR_уровня_95_процентов()
    {
        var result = ParametricVarCalculator.Calculate(
            mean: 0.0, standardDeviation: 1.0, observationCount: 1000,
            confidenceLevel: 0.95);

        Assert.Equal(1.6448536269514729, result.ValueAtRiskRelative, 1e-10);
    }

    [Fact]
    public void Положительная_средняя_доходность_снижает_оценку_риска()
    {
        var withoutDrift = ParametricVarCalculator.Calculate(
            mean: 0.0, standardDeviation: 0.02, observationCount: 1000);

        var withDrift = ParametricVarCalculator.Calculate(
            mean: 0.001, standardDeviation: 0.02, observationCount: 1000);

        Assert.Equal(
            withoutDrift.ValueAtRiskRelative - 0.001,
            withDrift.ValueAtRiskRelative,
            Tolerance);
    }

    [Fact]
    public void Пересчёт_на_горизонт_следует_правилу_корня_из_времени()
    {
        var oneDay = ParametricVarCalculator.Calculate(
            mean: 0.0, standardDeviation: 0.02, observationCount: 1000, horizonDays: 1);

        var tenDays = ParametricVarCalculator.Calculate(
            mean: 0.0, standardDeviation: 0.02, observationCount: 1000, horizonDays: 10);

        Assert.Equal(
            oneDay.ValueAtRiskRelative * Math.Sqrt(10.0),
            tenDays.ValueAtRiskRelative,
            Tolerance);
    }

    [Fact]
    public void Денежная_оценка_пропорциональна_стоимости_портфеля()
    {
        var result = ParametricVarCalculator.Calculate(
            mean: 0.0, standardDeviation: 0.02, observationCount: 1000,
            portfolioValue: 1_000_000.0);

        Assert.Equal(result.ValueAtRiskRelative * 1_000_000.0, result.ValueAtRiskAbsolute, 1e-6);
    }

    // -----------------------------------------------------------------------
    // Метод исторического моделирования
    // -----------------------------------------------------------------------

    [Fact]
    public void Исторический_VaR_равен_выборочному_квантилю()
    {
        var sample = NormalSample(1000, mean: 0.0, deviation: 0.02);

        var result = HistoricalVarCalculator.Calculate(sample, confidenceLevel: 0.99);

        var expected = -DescriptiveStatisticsCalculator.Quantile(sample, 0.01);

        Assert.Equal(expected, result.ValueAtRiskRelative, 1e-12);
    }

    [Fact]
    public void Исторический_VaR_на_нормальной_выборке_близок_к_параметрическому()
    {
        // На выборке, построенной из нормального распределения, оба метода
        // должны давать близкие оценки. Расхождение указывало бы на ошибку
        // в одном из них.
        var sample = NormalSample(5000, mean: 0.0, deviation: 0.02);

        var historical = HistoricalVarCalculator.Calculate(sample, confidenceLevel: 0.99);
        var parametric = ParametricVarCalculator.Calculate(sample, confidenceLevel: 0.99);

        var difference = Math.Abs(historical.ValueAtRiskRelative - parametric.ValueAtRiskRelative);

        Assert.True(
            difference < 0.001,
            $"Расхождение оценок составило {difference:F6}, что превышает допустимое.");
    }

    [Fact]
    public void Ожидаемые_потери_не_меньше_стоимостной_меры_риска()
    {
        // Свойство выполняется для любого распределения: ожидаемые потери
        // представляют собой среднее по «хвосту», граница которого равна
        // стоимостной мере риска.
        var sample = NormalSample(2000, mean: 0.0005, deviation: 0.02);

        foreach (var level in new[] { 0.90, 0.95, 0.99 })
        {
            var historical = HistoricalVarCalculator.Calculate(sample, level);
            var parametric = ParametricVarCalculator.Calculate(sample, level);

            Assert.True(historical.ExpectedShortfallRelative >= historical.ValueAtRiskRelative);
            Assert.True(parametric.ExpectedShortfallRelative >= parametric.ValueAtRiskRelative);
        }
    }

    [Fact]
    public void Оценка_риска_возрастает_с_уровнем_доверия()
    {
        var sample = NormalSample(2000, mean: 0.0, deviation: 0.02);

        var atNinety = HistoricalVarCalculator.Calculate(sample, 0.90).ValueAtRiskRelative;
        var atNinetyFive = HistoricalVarCalculator.Calculate(sample, 0.95).ValueAtRiskRelative;
        var atNinetyNine = HistoricalVarCalculator.Calculate(sample, 0.99).ValueAtRiskRelative;

        Assert.True(atNinety < atNinetyFive);
        Assert.True(atNinetyFive < atNinetyNine);
    }

    [Fact]
    public void Исторический_метод_улавливает_асимметрию_распределения()
    {
        // Распределение с выраженной отрицательной асимметрией: подавляющее
        // большинство дней даёт небольшую положительную доходность, редкие
        // дни — крупный убыток. Доля убыточных наблюдений составляет три
        // процента, то есть превышает уровень 1 − α, поэтому выборочный
        // квантиль попадает внутрь области крупных убытков.
        //
        // Параметрический метод такую форму распределения не воспроизводит:
        // он располагает единственным параметром разброса и «размазывает»
        // влияние крупных убытков по всему распределению.
        var sample = new double[1000];

        for (var i = 0; i < sample.Length; i++)
        {
            sample[i] = 0.004;
        }

        for (var k = 0; k < 30; k++)
        {
            sample[k * 33] = -0.02 - 0.002 * k;
        }

        var historical = HistoricalVarCalculator.Calculate(sample, 0.99);
        var parametric = ParametricVarCalculator.Calculate(sample, 0.99);

        Assert.True(
            historical.ValueAtRiskRelative > parametric.ValueAtRiskRelative,
            $"Исторический метод дал оценку {historical.ValueAtRiskRelative:P2}, " +
            $"параметрический — {parametric.ValueAtRiskRelative:P2}.");
    }

    [Fact]
    public void Соотношение_оценок_зависит_от_расположения_массы_хвоста()
    {
        // Обратный случай, подтверждающий, что превышение исторической оценки
        // над параметрической не является общим правилом. Если доля крупных
        // убытков в точности равна 1 − α, выборочный квантиль оказывается
        // на границе области убытков, тогда как те же наблюдения существенно
        // увеличивают среднеквадратическое отклонение. В результате
        // параметрическая оценка превышает историческую.
        //
        // Соотношение оценок определяется расположением массы «хвоста»
        // относительно уровня доверия и подлежит проверке на фактических
        // данных, а не принимается как известное заранее.
        var sample = new double[1000];

        for (var i = 0; i < sample.Length; i++)
        {
            sample[i] = i % 2 == 0 ? 0.005 : -0.005;
        }

        for (var k = 0; k < 10; k++)
        {
            sample[k * 37] = -0.15;
        }

        var historical = HistoricalVarCalculator.Calculate(sample, 0.99);
        var parametric = ParametricVarCalculator.Calculate(sample, 0.99);

        Assert.True(parametric.ValueAtRiskRelative > historical.ValueAtRiskRelative);
    }

    [Fact]
    public void Недостаточная_выборка_приводит_к_исключению()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => HistoricalVarCalculator.Calculate(new double[20], 0.99));

        Assert.Contains("тридцати наблюдений", exception.Message);
    }

    // -----------------------------------------------------------------------
    // Метод Монте-Карло
    // -----------------------------------------------------------------------

    [Fact]
    public void Монте_Карло_по_нормальному_закону_согласуется_с_параметрическим()
    {
        // Ключевая проверка корректности моделирования: при нормальном законе
        // распределения оценка методом Монте-Карло должна сходиться
        // к аналитической параметрической оценке.
        var sample = NormalSample(2000, mean: 0.0, deviation: 0.02);

        var parametric = ParametricVarCalculator.Calculate(sample, 0.99);

        var monteCarlo = MonteCarloVarCalculator.Calculate(
            sample, 0.99, horizonDays: 1, portfolioValue: 1.0,
            options: new MonteCarloOptions(ScenarioCount: 200_000, RandomSeed: 42));

        var relativeDifference =
            Math.Abs(monteCarlo.ValueAtRiskRelative - parametric.ValueAtRiskRelative) /
            parametric.ValueAtRiskRelative;

        Assert.True(
            relativeDifference < 0.03,
            $"Относительное расхождение составило {relativeDifference:P2}, " +
            "что превышает допустимую погрешность моделирования.");
    }

    [Fact]
    public void Расчёт_воспроизводим_при_заданном_начальном_значении()
    {
        var sample = NormalSample(1000, mean: 0.0, deviation: 0.02);
        var options = new MonteCarloOptions(ScenarioCount: 20_000, RandomSeed: 12345);

        var first = MonteCarloVarCalculator.Calculate(sample, 0.99, options: options);
        var second = MonteCarloVarCalculator.Calculate(sample, 0.99, options: options);

        Assert.Equal(first.ValueAtRiskRelative, second.ValueAtRiskRelative, 1e-15);
        Assert.Equal(first.ExpectedShortfallRelative, second.ExpectedShortfallRelative, 1e-15);
    }

    [Fact]
    public void Распределение_Стьюдента_даёт_более_высокую_оценку_риска()
    {
        // Распределение Стьюдента при той же дисперсии обладает более
        // «тяжёлыми хвостами», поэтому оценка риска при уровне доверия 0,99
        // должна оказаться выше, чем при нормальном распределении.
        var sample = NormalSample(2000, mean: 0.0, deviation: 0.02);

        var normal = MonteCarloVarCalculator.Calculate(
            sample, 0.99,
            options: new MonteCarloOptions(
                ScenarioCount: 100_000,
                Distribution: SimulationDistribution.Normal,
                RandomSeed: 7));

        var student = MonteCarloVarCalculator.Calculate(
            sample, 0.99,
            options: new MonteCarloOptions(
                ScenarioCount: 100_000,
                Distribution: SimulationDistribution.StudentT,
                StudentTDegreesOfFreedom: 4.0,
                RandomSeed: 7));

        Assert.True(
            student.ValueAtRiskRelative > normal.ValueAtRiskRelative,
            "Распределение Стьюдента должно давать более высокую оценку риска " +
            "на уровне доверия 0,99.");
    }

    [Fact]
    public void Разложение_Холецкого_воспроизводит_заданные_взаимосвязи()
    {
        // Два инструмента с высокой положительной корреляцией. Риск портфеля
        // из них должен быть близок к средневзвешенному риску составляющих:
        // эффект диверсификации практически отсутствует.
        var first = NormalSample(2000, mean: 0.0, deviation: 0.02);
        var second = new double[first.Length];

        for (var i = 0; i < first.Length; i++)
        {
            // Корреляция около 0,99.
            second[i] = first[i] * 0.99 + (i % 2 == 0 ? 0.0028 : -0.0028);
        }

        var correlated = MonteCarloVarCalculator.CalculatePortfolio(
            weights: [0.5, 0.5],
            returns: [first, second],
            confidenceLevel: 0.99,
            options: new MonteCarloOptions(ScenarioCount: 100_000, RandomSeed: 99));

        var single = MonteCarloVarCalculator.Calculate(
            first, 0.99,
            options: new MonteCarloOptions(ScenarioCount: 100_000, RandomSeed: 99));

        var ratio = correlated.ValueAtRiskRelative / single.ValueAtRiskRelative;

        Assert.True(
            ratio is > 0.9 and < 1.1,
            $"При корреляции около единицы риск портфеля должен быть близок к риску " +
            $"отдельного инструмента, фактическое отношение составило {ratio:F3}.");
    }

    [Fact]
    public void Диверсификация_снижает_риск_портфеля()
    {
        // Два независимых инструмента с одинаковым риском. Риск равновесного
        // портфеля должен быть существенно ниже риска каждого из них.
        var first = NormalSample(2000, mean: 0.0, deviation: 0.02);
        var second = new double[first.Length];

        // Перестановка значений разрушает взаимосвязь рядов,
        // сохраняя их распределение.
        for (var i = 0; i < first.Length; i++)
        {
            second[i] = first[(i * 887 + 13) % first.Length];
        }

        var portfolio = MonteCarloVarCalculator.CalculatePortfolio(
            weights: [0.5, 0.5],
            returns: [first, second],
            confidenceLevel: 0.99,
            options: new MonteCarloOptions(ScenarioCount: 100_000, RandomSeed: 2026));

        var single = MonteCarloVarCalculator.Calculate(
            first, 0.99,
            options: new MonteCarloOptions(ScenarioCount: 100_000, RandomSeed: 2026));

        Assert.True(
            portfolio.ValueAtRiskRelative < single.ValueAtRiskRelative * 0.85,
            "Диверсификация между слабо связанными инструментами должна " +
            "заметно снижать риск портфеля.");
    }

    [Fact]
    public void Бутстрэп_выборка_не_требует_предположений_о_распределении()
    {
        var sample = NormalSample(2000, mean: 0.0, deviation: 0.02);

        var result = MonteCarloVarCalculator.Calculate(
            sample, 0.99,
            options: new MonteCarloOptions(
                ScenarioCount: 100_000,
                Distribution: SimulationDistribution.HistoricalBootstrap,
                RandomSeed: 5));

        var historical = HistoricalVarCalculator.Calculate(sample, 0.99);

        var relativeDifference =
            Math.Abs(result.ValueAtRiskRelative - historical.ValueAtRiskRelative) /
            historical.ValueAtRiskRelative;

        Assert.True(
            relativeDifference < 0.10,
            $"Бутстрэп-выборка на горизонте в один день должна воспроизводить " +
            $"результат метода исторического моделирования, расхождение составило " +
            $"{relativeDifference:P2}.");
    }

    [Fact]
    public void Недопустимое_число_сценариев_приводит_к_исключению()
    {
        var sample = NormalSample(1000);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonteCarloVarCalculator.Calculate(
                sample, 0.99, options: new MonteCarloOptions(ScenarioCount: 100)));
    }

    [Fact]
    public void Недопустимый_уровень_доверия_приводит_к_исключению()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ParametricVarCalculator.Calculate(0.0, 1.0, 100, confidenceLevel: 0.4));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ParametricVarCalculator.Calculate(0.0, 1.0, 100, confidenceLevel: 1.0));
    }
}
