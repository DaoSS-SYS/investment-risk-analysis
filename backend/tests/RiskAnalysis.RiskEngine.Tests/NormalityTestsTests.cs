using MathNet.Numerics.Distributions;
using RiskAnalysis.RiskEngine.Statistics;
using Xunit;

namespace RiskAnalysis.RiskEngine.Tests;

/// <summary>
/// Проверка корректности реализации критерия Жарка — Бера.
///
/// Для эталонной выборки x = (2; 4; 4; 4; 5; 5; 7; 9) при n = 8,
/// g₁ = 0,65625 и g₂ = −0,21875 статистика критерия равна
///
///     JB = 8 / 6 · (0,65625² + (−0,21875)² / 4)
///        = 1,333333 · (0,430664 + 0,011963)
///        = 0,590169.
/// </summary>
public class NormalityTestsTests
{
    private const double Tolerance = 1e-9;

    private static readonly double[] ReferenceSample = [2, 4, 4, 4, 5, 5, 7, 9];

    /// <summary>
    /// Детерминированная выборка, близкая к нормальной: значения равны
    /// квантилям стандартного нормального распределения в равноотстоящих
    /// точках. Способ построения исключает зависимость результата проверки
    /// от генератора псевдослучайных чисел.
    /// </summary>
    private static double[] NormalSample(int count)
    {
        var sample = new double[count];

        for (var i = 0; i < count; i++)
        {
            sample[i] = Normal.InvCDF(0.0, 1.0, (i + 0.5) / count);
        }

        return sample;
    }

    [Fact]
    public void Статистика_критерия_для_эталонной_выборки()
    {
        var result = NormalityTests.JarqueBera(ReferenceSample);

        Assert.Equal(0.5901692708333333, result.Statistic, Tolerance);
        Assert.Equal(0.65625, result.Skewness, Tolerance);
        Assert.Equal(-0.21875, result.ExcessKurtosis, Tolerance);
    }

    [Fact]
    public void Достигаемый_уровень_значимости_совпадает_с_замкнутой_формой()
    {
        // Для распределения хи-квадрат с двумя степенями свободы
        // P(X > x) = exp(−x / 2). Проверка подтверждает корректность
        // обращения к библиотеке MathNet.Numerics.
        var result = NormalityTests.JarqueBera(ReferenceSample);

        Assert.Equal(Math.Exp(-result.Statistic / 2.0), result.PValue, Tolerance);
    }

    [Fact]
    public void Критические_значения_соответствуют_табличным()
    {
        var atFivePercent = NormalityTests.JarqueBera(ReferenceSample, 0.05);
        var atOnePercent = NormalityTests.JarqueBera(ReferenceSample, 0.01);

        // Табличные значения распределения хи-квадрат с двумя степенями свободы.
        Assert.Equal(5.99, atFivePercent.CriticalValue, 2);
        Assert.Equal(9.21, atOnePercent.CriticalValue, 2);
    }

    [Fact]
    public void Гипотеза_о_нормальности_не_отвергается_для_нормальной_выборки()
    {
        var result = NormalityTests.JarqueBera(NormalSample(1000));

        Assert.False(result.IsNormalityRejected);
        Assert.True(result.Statistic < result.CriticalValue);
        Assert.Contains("не отвергается", result.Conclusion);
    }

    [Fact]
    public void Гипотеза_о_нормальности_отвергается_для_асимметричной_выборки()
    {
        // Квадраты нормальных величин подчиняются распределению хи-квадрат
        // с одной степенью свободы, обладающему выраженной правосторонней
        // асимметрией и большим эксцессом.
        var sample = NormalSample(1000).Select(x => x * x).ToArray();

        var result = NormalityTests.JarqueBera(sample);

        Assert.True(result.IsNormalityRejected);
        Assert.True(result.Statistic > result.CriticalValue);
        Assert.True(result.Skewness > 1.0);
        Assert.True(result.ExcessKurtosis > 1.0);
        Assert.Contains("отвергается", result.Conclusion);
    }

    [Fact]
    public void Тяжёлые_хвосты_приводят_к_отклонению_гипотезы()
    {
        // Ряд с одним выбросом: остальные наблюдения симметричны и однородны.
        var sample = new double[200];

        for (var i = 0; i < sample.Length; i++)
        {
            sample[i] = i % 2 == 0 ? 0.01 : -0.01;
        }

        sample[100] = -0.50;

        var result = NormalityTests.JarqueBera(sample);

        Assert.True(result.IsNormalityRejected);
        Assert.True(result.ExcessKurtosis > 3.0);
    }

    [Fact]
    public void Выборка_менее_восьми_наблюдений_приводит_к_исключению()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => NormalityTests.JarqueBera([1, 2, 3, 4, 5, 6, 7]));

        Assert.Contains("восьми наблюдений", exception.Message);
    }

    [Fact]
    public void Недопустимый_уровень_значимости_приводит_к_исключению()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NormalityTests.JarqueBera(ReferenceSample, 1.5));
    }
}
