using RiskAnalysis.RiskEngine.Returns;
using Xunit;

namespace RiskAnalysis.RiskEngine.Tests;

/// <summary>
/// Проверка корректности расчёта доходностей.
///
/// Эталонные значения получены аналитически: для ценового ряда из двух
/// наблюдений результат вычисляется непосредственно по формуле, что позволяет
/// проверить реализацию независимо от самой реализации.
/// </summary>
public class ReturnCalculatorTests
{
    private const double Tolerance = 1e-10;

    private static PriceObservation[] Series(params double[] prices)
    {
        var start = new DateOnly(2026, 1, 5);
        var result = new PriceObservation[prices.Length];

        for (var i = 0; i < prices.Length; i++)
        {
            result[i] = new PriceObservation(start.AddDays(i), prices[i]);
        }

        return result;
    }

    [Fact]
    public void Логарифмическая_доходность_соответствует_формуле()
    {
        // ln(110 / 100) = ln(1,1) = 0,0953101798043249
        var returns = ReturnCalculator.Calculate(Series(100, 110), ReturnType.Logarithmic);

        Assert.Single(returns);
        Assert.Equal(0.0953101798043249, returns[0].Value, Tolerance);
    }

    [Fact]
    public void Простая_доходность_соответствует_формуле()
    {
        // 110 / 100 − 1 = 0,1
        var returns = ReturnCalculator.Calculate(Series(100, 110), ReturnType.Simple);

        Assert.Single(returns);
        Assert.Equal(0.1, returns[0].Value, Tolerance);
    }

    [Fact]
    public void Число_доходностей_на_единицу_меньше_числа_цен()
    {
        var returns = ReturnCalculator.Calculate(Series(100, 110, 105, 120, 118));

        Assert.Equal(4, returns.Length);
    }

    [Fact]
    public void Логарифмические_доходности_аддитивны_по_времени()
    {
        // Свойство, обосновывающее применение логарифмической доходности
        // при пересчёте оценок риска на горизонт в несколько дней:
        // сумма доходностей за периоды равна доходности за весь период.
        var prices = Series(100, 110, 99, 120, 118);

        var returns = ReturnCalculator.Calculate(prices, ReturnType.Logarithmic);
        var sum = returns.Sum(r => r.Value);

        var total = Math.Log(prices[^1].Price / prices[0].Price);

        Assert.Equal(total, sum, Tolerance);
    }

    [Fact]
    public void Простые_доходности_не_аддитивны_по_времени()
    {
        // Обратное утверждение: простая доходность этим свойством не обладает,
        // что и служит основанием для выбора логарифмической доходности.
        var prices = Series(100, 110, 99);

        var returns = ReturnCalculator.Calculate(prices, ReturnType.Simple);
        var sum = returns.Sum(r => r.Value);

        var total = prices[^1].Price / prices[0].Price - 1.0;

        Assert.NotEqual(total, sum, 6);
    }

    [Fact]
    public void Преобразование_доходностей_обратимо()
    {
        const double logarithmic = 0.0953101798043249;

        var simple = ReturnCalculator.ToSimple(logarithmic);

        Assert.Equal(0.1, simple, Tolerance);
        Assert.Equal(logarithmic, ReturnCalculator.ToLogarithmic(simple), Tolerance);
    }

    [Fact]
    public void Месячный_ряд_строится_по_последнему_торговому_дню_месяца()
    {
        var prices = new[]
        {
            new PriceObservation(new DateOnly(2026, 1, 12), 100),
            new PriceObservation(new DateOnly(2026, 1, 30), 105),
            new PriceObservation(new DateOnly(2026, 2, 2), 107),
            new PriceObservation(new DateOnly(2026, 2, 27), 110),
            new PriceObservation(new DateOnly(2026, 3, 3), 112)
        };

        var resampled = ReturnCalculator.Resample(prices, ReturnFrequency.Monthly);

        Assert.Equal(3, resampled.Count);
        Assert.Equal(new DateOnly(2026, 1, 30), resampled[0].Date);
        Assert.Equal(new DateOnly(2026, 2, 27), resampled[1].Date);
        Assert.Equal(new DateOnly(2026, 3, 3), resampled[2].Date);
    }

    [Fact]
    public void Недельный_ряд_строится_по_последнему_торговому_дню_недели()
    {
        // 5 января 2026 года — понедельник, 9 января — пятница,
        // 12 января — понедельник следующей недели.
        var prices = new[]
        {
            new PriceObservation(new DateOnly(2026, 1, 5), 100),
            new PriceObservation(new DateOnly(2026, 1, 7), 102),
            new PriceObservation(new DateOnly(2026, 1, 9), 104),
            new PriceObservation(new DateOnly(2026, 1, 12), 106),
            new PriceObservation(new DateOnly(2026, 1, 16), 108)
        };

        var resampled = ReturnCalculator.Resample(prices, ReturnFrequency.Weekly);

        Assert.Equal(2, resampled.Count);
        Assert.Equal(new DateOnly(2026, 1, 9), resampled[0].Date);
        Assert.Equal(new DateOnly(2026, 1, 16), resampled[1].Date);
    }

    [Fact]
    public void Ряд_короче_двух_наблюдений_даёт_пустой_результат()
    {
        Assert.Empty(ReturnCalculator.Calculate(Series(100)));
        Assert.Empty(ReturnCalculator.Calculate(Series()));
    }

    [Fact]
    public void Неположительная_цена_приводит_к_исключению()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => ReturnCalculator.Calculate(Series(100, 0)));

        Assert.Contains("неположительную цену", exception.Message);
    }

    [Fact]
    public void Число_периодов_в_году_соответствует_периодичности()
    {
        Assert.Equal(252, ReturnCalculator.PeriodsPerYear(ReturnFrequency.Daily));
        Assert.Equal(52, ReturnCalculator.PeriodsPerYear(ReturnFrequency.Weekly));
        Assert.Equal(12, ReturnCalculator.PeriodsPerYear(ReturnFrequency.Monthly));
    }
}
