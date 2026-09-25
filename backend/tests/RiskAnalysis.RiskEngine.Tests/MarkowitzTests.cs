using MathNet.Numerics.Distributions;
using RiskAnalysis.RiskEngine.Optimization;
using RiskAnalysis.RiskEngine.Statistics;
using Xunit;

namespace RiskAnalysis.RiskEngine.Tests;

/// <summary>
/// Проверка корректности оптимизации структуры портфеля по модели Марковица.
///
/// Основные проверяемые свойства: соблюдение ограничений на доли, монотонность
/// эффективной границы и её расположение относительно множества портфелей
/// со случайной структурой.
/// </summary>
public class MarkowitzTests
{
    private const double Tolerance = 1e-8;

    /// <summary>
    /// Детерминированный ряд доходностей с заданными средним значением
    /// и среднеквадратическим отклонением.
    /// </summary>
    private static double[] Series(int count, double mean, double deviation, int shift)
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
            result[i] = (source - statistics.Mean) / statistics.StandardDeviation * deviation + mean;
        }

        return result;
    }

    private static double[][] ThreeAssets(int count = 800) =>
    [
        Series(count, 0.00040, 0.012, 0),
        Series(count, 0.00025, 0.009, 137),
        Series(count, 0.00055, 0.018, 401)
    ];

    // -----------------------------------------------------------------------
    // Ограничения на структуру портфеля
    // -----------------------------------------------------------------------

    [Fact]
    public void Доли_всех_портфелей_суммируются_в_единицу()
    {
        var result = MarkowitzOptimizer.Optimize(
            ThreeAssets(), riskFreeRateAnnual: 0.10, randomPortfolios: 200);

        foreach (var point in result.EfficientFrontier)
        {
            Assert.Equal(1.0, point.Weights.Sum(), 1e-9);
        }

        Assert.Equal(1.0, result.MinimumVariance.Weights.Sum(), 1e-9);
        Assert.Equal(1.0, result.MaximumSharpe.Weights.Sum(), 1e-9);

        foreach (var point in result.RandomPortfolios)
        {
            Assert.Equal(1.0, point.Weights.Sum(), 1e-9);
        }
    }

    [Fact]
    public void Короткие_продажи_не_допускаются()
    {
        var result = MarkowitzOptimizer.Optimize(
            ThreeAssets(), riskFreeRateAnnual: 0.10, randomPortfolios: 200);

        foreach (var point in result.EfficientFrontier)
        {
            Assert.All(point.Weights, weight => Assert.True(weight >= -1e-12));
        }
    }

    [Fact]
    public void Предельная_доля_инструмента_соблюдается()
    {
        const double maximumWeight = 0.40;

        var result = MarkowitzOptimizer.Optimize(
            ThreeAssets(), riskFreeRateAnnual: 0.10,
            maximumWeight: maximumWeight, randomPortfolios: 200);

        foreach (var point in result.EfficientFrontier)
        {
            Assert.All(point.Weights, weight => Assert.True(weight <= maximumWeight + 1e-9));
        }

        Assert.All(
            result.MinimumVariance.Weights,
            weight => Assert.True(weight <= maximumWeight + 1e-9));
    }

    [Fact]
    public void Недостижимая_предельная_доля_приводит_к_исключению()
    {
        // При трёх инструментах предельная доля не может быть меньше одной трети.
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => MarkowitzOptimizer.Optimize(
                ThreeAssets(), riskFreeRateAnnual: 0.10, maximumWeight: 0.20));

        Assert.Contains("недостижима", exception.Message);
    }

    [Fact]
    public void Предельная_доля_равная_обратному_числу_инструментов_даёт_равные_доли()
    {
        // Единственной допустимой структурой является равновзвешенный портфель.
        var result = MarkowitzOptimizer.Optimize(
            ThreeAssets(), riskFreeRateAnnual: 0.10,
            maximumWeight: 1.0 / 3.0, randomPortfolios: 50);

        foreach (var weight in result.MinimumVariance.Weights)
        {
            Assert.Equal(1.0 / 3.0, weight, 1e-6);
        }
    }

    // -----------------------------------------------------------------------
    // Свойства эффективной границы
    // -----------------------------------------------------------------------

    [Fact]
    public void Эффективная_граница_возрастает_по_доходности()
    {
        var result = MarkowitzOptimizer.Optimize(
            ThreeAssets(), riskFreeRateAnnual: 0.10, randomPortfolios: 100);

        // Точки упорядочены по возрастанию риска; по построению границы
        // доходность при этом также должна возрастать.
        for (var i = 1; i < result.EfficientFrontier.Count; i++)
        {
            Assert.True(
                result.EfficientFrontier[i].Volatility >=
                result.EfficientFrontier[i - 1].Volatility - 1e-9);

            Assert.True(
                result.EfficientFrontier[i].ExpectedReturn >=
                result.EfficientFrontier[i - 1].ExpectedReturn - 1e-9,
                $"Точка {i}: доходность {result.EfficientFrontier[i].ExpectedReturn:P4} " +
                $"ниже предыдущей {result.EfficientFrontier[i - 1].ExpectedReturn:P4}.");
        }
    }

    [Fact]
    public void Портфель_наименьшей_дисперсии_имеет_наименьший_риск()
    {
        var result = MarkowitzOptimizer.Optimize(
            ThreeAssets(), riskFreeRateAnnual: 0.10, randomPortfolios: 3000);

        // Ни один случайный портфель не должен иметь риск существенно ниже
        // портфеля наименьшей дисперсии.
        foreach (var point in result.RandomPortfolios)
        {
            Assert.True(
                point.Volatility >= result.MinimumVariance.Volatility - 1e-6,
                $"Случайный портфель с волатильностью {point.Volatility:P4} оказался ниже " +
                $"портфеля наименьшей дисперсии {result.MinimumVariance.Volatility:P4}.");
        }
    }

    [Fact]
    public void Касательный_портфель_имеет_наибольший_коэффициент_Шарпа()
    {
        var result = MarkowitzOptimizer.Optimize(
            ThreeAssets(), riskFreeRateAnnual: 0.08, randomPortfolios: 3000);

        foreach (var point in result.RandomPortfolios)
        {
            Assert.True(
                point.SharpeRatio <= result.MaximumSharpe.SharpeRatio + 1e-6,
                $"Случайный портфель с коэффициентом Шарпа {point.SharpeRatio:F4} превысил " +
                $"касательный портфель {result.MaximumSharpe.SharpeRatio:F4}.");
        }
    }

    [Fact]
    public void Ни_один_случайный_портфель_не_доминирует_точку_границы()
    {
        // Определение эффективности: портфель является эффективным, если не
        // существует другого портфеля, который одновременно имеет меньший
        // риск и большую доходность.
        //
        // Проверка ведётся именно в такой формулировке, а не сопоставлением
        // доходности при заданном уровне риска: эффективная граница
        // представлена конечным числом точек, и доходность в промежутке
        // между соседними точками не определена.
        var result = MarkowitzOptimizer.Optimize(
            ThreeAssets(), riskFreeRateAnnual: 0.10, randomPortfolios: 2000);

        const double epsilon = 1e-6;

        foreach (var random in result.RandomPortfolios)
        {
            foreach (var frontier in result.EfficientFrontier)
            {
                var dominates =
                    random.Volatility < frontier.Volatility - epsilon &&
                    random.ExpectedReturn > frontier.ExpectedReturn + epsilon;

                Assert.False(
                    dominates,
                    $"Случайный портфель (риск {random.Volatility:P4}, доходность " +
                    $"{random.ExpectedReturn:P4}) доминирует точку границы " +
                    $"(риск {frontier.Volatility:P4}, доходность {frontier.ExpectedReturn:P4}).");
            }
        }
    }

    [Fact]
    public void Граница_расположена_выше_случайных_портфелей_с_учётом_шага_сетки()
    {
        // Более слабая, но наглядная проверка: доходность ближайшей по риску
        // точки границы не должна быть ниже доходности случайного портфеля
        // больше, чем на шаг сетки по доходности.
        var result = MarkowitzOptimizer.Optimize(
            ThreeAssets(), riskFreeRateAnnual: 0.10, randomPortfolios: 1000);

        var frontier = result.EfficientFrontier;

        // Наибольший шаг сетки по доходности между соседними точками.
        double maximumStep = 0.0;

        for (var i = 1; i < frontier.Count; i++)
        {
            maximumStep = Math.Max(
                maximumStep,
                frontier[i].ExpectedReturn - frontier[i - 1].ExpectedReturn);
        }

        foreach (var point in result.RandomPortfolios)
        {
            var nearest = frontier
                .Where(f => f.Volatility <= point.Volatility + 1e-9)
                .MaxBy(f => f.ExpectedReturn);

            if (nearest is not null)
            {
                Assert.True(
                    nearest.ExpectedReturn >= point.ExpectedReturn - maximumStep,
                    $"Случайный портфель (риск {point.Volatility:P4}, доходность " +
                    $"{point.ExpectedReturn:P4}) превысил границу более чем на шаг сетки " +
                    $"{maximumStep:P4}.");
            }
        }
    }

    [Fact]
    public void Диверсификация_снижает_риск_ниже_наименее_рискованного_инструмента()
    {
        // Три слабо связанных инструмента: риск портфеля наименьшей
        // дисперсии должен быть ниже риска любого из них по отдельности.
        var returns = ThreeAssets();

        var result = MarkowitzOptimizer.Optimize(
            returns, riskFreeRateAnnual: 0.10, randomPortfolios: 100);

        var annualFactor = Math.Sqrt(252.0);

        for (var i = 0; i < returns.Length; i++)
        {
            var standalone =
                DescriptiveStatisticsCalculator.Calculate(returns[i]).StandardDeviation * annualFactor;

            Assert.True(
                result.MinimumVariance.Volatility < standalone,
                $"Риск портфеля {result.MinimumVariance.Volatility:P2} не ниже риска " +
                $"инструмента {i} ({standalone:P2}).");
        }
    }

    // -----------------------------------------------------------------------
    // Сопоставление с текущей структурой
    // -----------------------------------------------------------------------

    [Fact]
    public void Текущая_структура_оценивается_по_тем_же_правилам()
    {
        var returns = ThreeAssets();
        double[] current = [0.5, 0.3, 0.2];

        var result = MarkowitzOptimizer.Optimize(
            returns, riskFreeRateAnnual: 0.10, currentWeights: current, randomPortfolios: 100);

        Assert.NotNull(result.CurrentPortfolio);
        Assert.Equal(current, result.CurrentPortfolio!.Weights);

        // Текущая структура не может превосходить касательный портфель.
        Assert.True(result.CurrentPortfolio.SharpeRatio <= result.MaximumSharpe.SharpeRatio + 1e-6);
    }

    [Fact]
    public void Равновзвешенная_структура_уступает_оптимальной()
    {
        var returns = ThreeAssets();
        var equal = Enumerable.Repeat(1.0 / 3.0, 3).ToArray();

        var result = MarkowitzOptimizer.Optimize(
            returns, riskFreeRateAnnual: 0.10, currentWeights: equal, randomPortfolios: 100);

        Assert.True(
            result.MinimumVariance.Volatility <= result.CurrentPortfolio!.Volatility + 1e-9,
            "Портфель наименьшей дисперсии не может быть рискованнее равновзвешенного.");
    }

    [Fact]
    public void Единственный_инструмент_приводит_к_исключению()
    {
        Assert.Throws<ArgumentException>(
            () => MarkowitzOptimizer.Optimize(
                [Series(300, 0.0004, 0.012, 0)], riskFreeRateAnnual: 0.10));
    }

    [Fact]
    public void Ряды_различной_длины_приводят_к_исключению()
    {
        double[][] returns =
        [
            Series(300, 0.0004, 0.012, 0),
            Series(200, 0.0003, 0.010, 11)
        ];

        Assert.Throws<ArgumentException>(
            () => MarkowitzOptimizer.Optimize(returns, riskFreeRateAnnual: 0.10));
    }

    [Fact]
    public void Расчёт_воспроизводим_при_заданном_начальном_значении()
    {
        var returns = ThreeAssets();

        var first = MarkowitzOptimizer.Optimize(
            returns, 0.10, randomPortfolios: 500, randomSeed: 777);

        var second = MarkowitzOptimizer.Optimize(
            returns, 0.10, randomPortfolios: 500, randomSeed: 777);

        Assert.Equal(first.MaximumSharpe.SharpeRatio, second.MaximumSharpe.SharpeRatio, Tolerance);

        for (var i = 0; i < first.RandomPortfolios.Count; i++)
        {
            Assert.Equal(
                first.RandomPortfolios[i].Volatility,
                second.RandomPortfolios[i].Volatility,
                Tolerance);
        }
    }
}
