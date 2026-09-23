using RiskAnalysis.RiskEngine.Statistics;
using Xunit;

namespace RiskAnalysis.RiskEngine.Tests;

/// <summary>
/// Проверка корректности расчёта описательных статистик.
///
/// Основу проверки составляет эталонная выборка, для которой все показатели
/// вычислены вручную:
///
///     x = (2; 4; 4; 4; 5; 5; 7; 9), n = 8
///
///     среднее                       x̄ = 40 / 8 = 5
///     отклонения                    −3; −1; −1; −1; 0; 0; 2; 4
///     сумма квадратов               9 + 1 + 1 + 1 + 0 + 0 + 4 + 16 = 32
///     второй центральный момент     m₂ = 32 / 8 = 4
///     выборочная дисперсия          s² = 32 / 7 = 4,571428571
///     третий центральный момент     m₃ = (−27 − 1 − 1 − 1 + 0 + 0 + 8 + 64) / 8 = 5,25
///     четвёртый центральный момент  m₄ = (81 + 1 + 1 + 1 + 0 + 0 + 16 + 256) / 8 = 44,5
///     коэффициент асимметрии        g₁ = 5,25 / 4^1,5 = 5,25 / 8 = 0,65625
///     коэффициент эксцесса          g₂ = 44,5 / 16 − 3 = −0,21875
/// </summary>
public class DescriptiveStatisticsTests
{
    private const double Tolerance = 1e-10;

    private static readonly double[] ReferenceSample = [2, 4, 4, 4, 5, 5, 7, 9];

    [Fact]
    public void Среднее_и_дисперсия_эталонной_выборки()
    {
        var result = DescriptiveStatisticsCalculator.Calculate(ReferenceSample);

        Assert.Equal(8, result.Count);
        Assert.Equal(5.0, result.Mean, Tolerance);
        Assert.Equal(32.0 / 7.0, result.Variance, Tolerance);
        Assert.Equal(Math.Sqrt(32.0 / 7.0), result.StandardDeviation, Tolerance);
    }

    [Fact]
    public void Коэффициенты_формы_эталонной_выборки_оценка_по_моментам()
    {
        var result = DescriptiveStatisticsCalculator.Calculate(ReferenceSample);

        Assert.Equal(0.65625, result.Skewness, Tolerance);
        Assert.Equal(-0.21875, result.ExcessKurtosis, Tolerance);
    }

    [Fact]
    public void Коэффициенты_формы_эталонной_выборки_с_поправкой_на_объём()
    {
        // Значения совпадают с результатом функций СКОС и ЭКСЦЕСС
        // табличного процессора Microsoft Excel:
        //   G₁ = g₁ · √(n(n−1)) / (n−2) = 0,65625 · √56 / 6
        //   G₂ = (n−1) / ((n−2)(n−3)) · ((n+1)·g₂ + 6) = 7/30 · (9·(−0,21875) + 6)
        var result = DescriptiveStatisticsCalculator.Calculate(ReferenceSample);

        var expectedSkewness = 0.65625 * Math.Sqrt(56.0) / 6.0;
        var expectedKurtosis = 7.0 / 30.0 * (9.0 * -0.21875 + 6.0);

        Assert.Equal(expectedSkewness, result.SkewnessAdjusted, Tolerance);
        Assert.Equal(0.8184875533567997, result.SkewnessAdjusted, 1e-12);

        Assert.Equal(expectedKurtosis, result.ExcessKurtosisAdjusted, Tolerance);
        Assert.Equal(0.940625, result.ExcessKurtosisAdjusted, Tolerance);
    }

    [Fact]
    public void Минимум_максимум_и_медиана()
    {
        var result = DescriptiveStatisticsCalculator.Calculate(ReferenceSample);

        Assert.Equal(2.0, result.Minimum, Tolerance);
        Assert.Equal(9.0, result.Maximum, Tolerance);

        // Число наблюдений чётное: медиана равна полусумме
        // четвёртого и пятого членов вариационного ряда: (4 + 5) / 2.
        Assert.Equal(4.5, result.Median, Tolerance);
    }

    [Fact]
    public void Медиана_ряда_нечётной_длины()
    {
        Assert.Equal(3.0, DescriptiveStatisticsCalculator.Median([5, 1, 3, 2, 4]), Tolerance);
    }

    [Fact]
    public void Симметричная_выборка_имеет_нулевую_асимметрию()
    {
        var result = DescriptiveStatisticsCalculator.Calculate([-2, -1, 0, 1, 2]);

        Assert.Equal(0.0, result.Skewness, Tolerance);
    }

    [Fact]
    public void Пересчёт_в_годовое_выражение_по_правилу_корня_из_времени()
    {
        // Ряд с нулевым средним и известным среднеквадратическим отклонением.
        double[] sample = [-0.01, 0.01, -0.01, 0.01, -0.01, 0.01, -0.01, 0.01];

        var result = DescriptiveStatisticsCalculator.Calculate(sample, periodsPerYear: 252);

        Assert.Equal(result.Mean * 252.0, result.AnnualizedMean, Tolerance);
        Assert.Equal(result.StandardDeviation * Math.Sqrt(252.0), result.AnnualizedVolatility, Tolerance);
    }

    [Fact]
    public void Квантиль_соответствует_линейной_интерполяции()
    {
        // Для ряда (1; 2; 3; 4) и уровня 0,25 положение равно 0,25 · 3 = 0,75,
        // результат равен 1 · 0,25 + 2 · 0,75 = 1,75.
        // Совпадает с функцией ПРОЦЕНТИЛЬ табличного процессора Microsoft Excel.
        double[] sample = [1, 2, 3, 4];

        Assert.Equal(1.75, DescriptiveStatisticsCalculator.Quantile(sample, 0.25), Tolerance);
        Assert.Equal(2.5, DescriptiveStatisticsCalculator.Quantile(sample, 0.50), Tolerance);
        Assert.Equal(3.25, DescriptiveStatisticsCalculator.Quantile(sample, 0.75), Tolerance);
        Assert.Equal(1.0, DescriptiveStatisticsCalculator.Quantile(sample, 0.0), Tolerance);
        Assert.Equal(4.0, DescriptiveStatisticsCalculator.Quantile(sample, 1.0), Tolerance);
    }

    [Fact]
    public void Квантиль_не_зависит_от_порядка_наблюдений()
    {
        double[] ordered = [1, 2, 3, 4, 5];
        double[] shuffled = [4, 1, 5, 2, 3];

        Assert.Equal(
            DescriptiveStatisticsCalculator.Quantile(ordered, 0.3),
            DescriptiveStatisticsCalculator.Quantile(shuffled, 0.3),
            Tolerance);
    }

    [Fact]
    public void Выборка_короче_двух_наблюдений_приводит_к_исключению()
    {
        Assert.Throws<ArgumentException>(
            () => DescriptiveStatisticsCalculator.Calculate([1.0]));
    }

    [Fact]
    public void Недопустимый_уровень_квантиля_приводит_к_исключению()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DescriptiveStatisticsCalculator.Quantile([1, 2, 3], 1.5));
    }
}
