using RiskAnalysis.RiskEngine.Performance;
using RiskAnalysis.RiskEngine.Returns;
using Xunit;

namespace RiskAnalysis.RiskEngine.Tests;

/// <summary>
/// Проверка корректности расчёта коэффициентов эффективности, просадок
/// и параметров модели CAPM.
/// </summary>
public class PerformanceTests
{
    private const double Tolerance = 1e-9;

    private static PriceObservation[] Prices(params double[] values)
    {
        var start = new DateOnly(2026, 1, 5);
        var result = new PriceObservation[values.Length];

        for (var i = 0; i < values.Length; i++)
        {
            result[i] = new PriceObservation(start.AddDays(i), values[i]);
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // Просадки
    // -----------------------------------------------------------------------

    [Fact]
    public void Максимальная_просадка_рассчитана_по_известному_ряду()
    {
        // Максимум 100 на первом наблюдении, минимум 60 на третьем:
        // просадка равна (100 − 60) / 100 = 0,4.
        var result = DrawdownCalculator.Calculate(Prices(100, 80, 60, 90, 120));

        Assert.Equal(0.4, result.MaxDrawdown, Tolerance);
        Assert.Equal(new DateOnly(2026, 1, 5), result.PeakDate);
        Assert.Equal(new DateOnly(2026, 1, 7), result.TroughDate);
        Assert.Equal(2, result.DeclineTradingDays);
    }

    [Fact]
    public void Дата_восстановления_определяется_по_достижению_прежнего_максимума()
    {
        var result = DrawdownCalculator.Calculate(Prices(100, 60, 80, 100, 110));

        Assert.Equal(new DateOnly(2026, 1, 8), result.RecoveryDate);
        Assert.Equal(2, result.RecoveryTradingDays);
    }

    [Fact]
    public void Незавершённая_просадка_не_имеет_даты_восстановления()
    {
        var result = DrawdownCalculator.Calculate(Prices(100, 60, 70, 80));

        Assert.Null(result.RecoveryDate);
        Assert.Equal(-1, result.RecoveryTradingDays);
        Assert.Equal(0.2, result.CurrentDrawdown, Tolerance);
    }

    [Fact]
    public void Возрастающий_ряд_не_имеет_просадки()
    {
        var result = DrawdownCalculator.Calculate(Prices(100, 110, 120, 130));

        Assert.Equal(0.0, result.MaxDrawdown, Tolerance);
        Assert.Equal(0.0, result.CurrentDrawdown, Tolerance);
    }

    [Fact]
    public void Учитывается_наибольшая_из_нескольких_просадок()
    {
        // Первая просадка 20 %, вторая — 50 % от нового максимума 150.
        var result = DrawdownCalculator.Calculate(Prices(100, 80, 100, 150, 75, 120));

        Assert.Equal(0.5, result.MaxDrawdown, Tolerance);
        Assert.Equal(new DateOnly(2026, 1, 8), result.PeakDate);
        Assert.Equal(new DateOnly(2026, 1, 9), result.TroughDate);
    }

    // -----------------------------------------------------------------------
    // Коэффициенты эффективности
    // -----------------------------------------------------------------------

    [Fact]
    public void Безрисковая_ставка_приводится_к_дневной_логарифмической()
    {
        // ln(1 + 0,14) / 252
        var rate = PerformanceCalculator.PeriodRiskFreeRate(0.14);

        Assert.Equal(Math.Log(1.14) / 252.0, rate, Tolerance);
    }

    [Fact]
    public void Коэффициент_Шарпа_равен_отношению_избыточной_доходности_к_волатильности()
    {
        // Ряд с известными средним и среднеквадратическим отклонением.
        double[] returns = [0.01, -0.01, 0.01, -0.01, 0.01, -0.01, 0.02, 0.00];

        var result = PerformanceCalculator.Calculate(returns, riskFreeRateAnnual: 0.0, maxDrawdown: 0.1);

        var expected = result.AnnualizedExcessReturn / result.AnnualizedVolatility;

        Assert.Equal(expected, result.SharpeRatio, Tolerance);

        // При нулевой безрисковой ставке избыточная доходность совпадает
        // с логарифмической доходностью в годовом выражении.
        Assert.Equal(result.AnnualizedLogReturn, result.AnnualizedExcessReturn, Tolerance);
    }

    [Fact]
    public void Коэффициент_Сортино_превышает_коэффициент_Шарпа_при_положительной_асимметрии()
    {
        // Ряд с редкими крупными положительными доходностями: отклонение
        // вниз мало по сравнению с общим среднеквадратическим отклонением.
        var returns = new double[200];

        for (var i = 0; i < returns.Length; i++)
        {
            returns[i] = -0.001;
        }

        for (var i = 0; i < 10; i++)
        {
            returns[i * 20] = 0.05;
        }

        var result = PerformanceCalculator.Calculate(returns, 0.0, 0.1);

        Assert.True(
            result.SortinoRatio > result.SharpeRatio,
            "При положительной асимметрии коэффициент Сортино должен превышать " +
            "коэффициент Шарпа: благоприятные отклонения не учитываются как риск.");
    }

    [Fact]
    public void Коэффициент_Сортино_не_ниже_коэффициента_Шарпа_при_положительной_доходности()
    {
        // При принятом определении отклонения вниз, где знаменатель суммы
        // равен общему числу наблюдений, справедливо DD ≤ σ, а значит
        // коэффициент Сортино не может оказаться ниже коэффициента Шарпа,
        // пока избыточная доходность положительна. Свойство проверяется
        // в том числе на распределении с отрицательной асимметрией.
        var negativelySkewed = new double[200];

        for (var i = 0; i < negativelySkewed.Length; i++)
        {
            negativelySkewed[i] = 0.002;
        }

        for (var i = 0; i < 5; i++)
        {
            negativelySkewed[i * 40] = -0.03;
        }

        var symmetric = new double[200];

        for (var i = 0; i < symmetric.Length; i++)
        {
            symmetric[i] = i % 2 == 0 ? 0.011 : -0.009;
        }

        foreach (var sample in new[] { negativelySkewed, symmetric })
        {
            var result = PerformanceCalculator.Calculate(sample, 0.0, 0.1);

            Assert.True(result.AnnualizedExcessReturn > 0.0);
            Assert.True(
                result.SortinoRatio >= result.SharpeRatio - 1e-12,
                $"Сортино {result.SortinoRatio:F4} оказался ниже Шарпа {result.SharpeRatio:F4}.");
        }
    }

    [Fact]
    public void Отклонение_вниз_не_превышает_среднеквадратического_отклонения()
    {
        // Непосредственная проверка неравенства DD ≤ σ, из которого следует
        // соотношение коэффициентов.
        var sample = new double[300];

        for (var i = 0; i < sample.Length; i++)
        {
            sample[i] = Math.Sin(i * 0.31) * 0.02 + 0.0008;
        }

        var result = PerformanceCalculator.Calculate(sample, 0.0, 0.1);

        Assert.True(result.DownsideDeviationAnnualized <= result.AnnualizedVolatility + 1e-12);
    }

    [Fact]
    public void Доходность_ниже_безрисковой_ставки_даёт_отрицательный_коэффициент_Шарпа()
    {
        var returns = Enumerable.Repeat(0.0, 100).ToArray();
        returns[0] = 0.001;
        returns[1] = -0.001;

        var result = PerformanceCalculator.Calculate(returns, riskFreeRateAnnual: 0.14, maxDrawdown: 0.1);

        Assert.True(result.SharpeRatio < 0.0);
        Assert.Contains("не превысила безрисковую ставку", result.Conclusion);
    }

    [Fact]
    public void Коэффициент_Кальмара_равен_отношению_доходности_к_просадке()
    {
        double[] returns = [0.01, 0.01, -0.005, 0.01, 0.005, -0.002, 0.008, 0.003];

        var result = PerformanceCalculator.Calculate(returns, 0.0, maxDrawdown: 0.25);

        Assert.Equal(result.AnnualizedReturn / 0.25, result.CalmarRatio, Tolerance);
    }

    // -----------------------------------------------------------------------
    // Модель CAPM
    // -----------------------------------------------------------------------

    [Fact]
    public void Бета_равна_единице_при_совпадении_с_эталонным_портфелем()
    {
        var market = new double[100];

        for (var i = 0; i < market.Length; i++)
        {
            market[i] = Math.Sin(i) * 0.01;
        }

        var result = CapmCalculator.Calculate(market, market, riskFreeRateAnnual: 0.0);

        Assert.Equal(1.0, result.Beta, 1e-10);
        Assert.Equal(0.0, result.AlphaPerPeriod, 1e-12);
        Assert.Equal(1.0, result.RSquared, 1e-10);
        Assert.Equal(1.0, result.SystematicRiskShare, 1e-10);
        Assert.Equal(0.0, result.SpecificRiskShare, 1e-10);
    }

    [Fact]
    public void Бета_удваивается_при_удвоении_чувствительности_к_рынку()
    {
        var market = new double[200];
        var asset = new double[200];

        for (var i = 0; i < market.Length; i++)
        {
            market[i] = Math.Sin(i * 0.7) * 0.01;
            asset[i] = 2.0 * market[i];
        }

        var result = CapmCalculator.Calculate(asset, market, riskFreeRateAnnual: 0.0);

        Assert.Equal(2.0, result.Beta, 1e-10);
        Assert.Equal(1.0, result.RSquared, 1e-10);
    }

    [Fact]
    public void Альфа_выделяется_как_доходность_сверх_рыночной()
    {
        // Инструмент повторяет рынок с постоянной надбавкой 0,001 за период.
        var market = new double[300];
        var asset = new double[300];

        for (var i = 0; i < market.Length; i++)
        {
            market[i] = Math.Sin(i * 0.4) * 0.012;
            asset[i] = market[i] + 0.001;
        }

        var result = CapmCalculator.Calculate(asset, market, riskFreeRateAnnual: 0.0);

        Assert.Equal(1.0, result.Beta, 1e-10);
        Assert.Equal(0.001, result.AlphaPerPeriod, 1e-12);
        Assert.Equal(0.001 * 252.0, result.AlphaAnnualized, 1e-10);
    }

    [Fact]
    public void Доля_систематического_риска_совпадает_с_коэффициентом_детерминации()
    {
        // Разложение дисперсии и коэффициент детерминации должны давать
        // одну и ту же величину. Расхождение указывало бы на ошибку
        // в одном из расчётов.
        var market = new double[500];
        var asset = new double[500];

        for (var i = 0; i < market.Length; i++)
        {
            market[i] = Math.Sin(i * 0.3) * 0.015;
            asset[i] = 1.3 * market[i] + Math.Cos(i * 1.7) * 0.008;
        }

        var result = CapmCalculator.Calculate(asset, market, riskFreeRateAnnual: 0.0);

        Assert.Equal(result.RSquared, result.SystematicRiskShare, 1e-9);
        Assert.Equal(1.0, result.SystematicRiskShare + result.SpecificRiskShare, 1e-12);
    }

    [Fact]
    public void Отрицательная_бета_при_противоположном_движении()
    {
        var market = new double[200];
        var asset = new double[200];

        for (var i = 0; i < market.Length; i++)
        {
            market[i] = Math.Sin(i * 0.5) * 0.01;
            asset[i] = -0.8 * market[i];
        }

        var result = CapmCalculator.Calculate(asset, market, riskFreeRateAnnual: 0.0);

        Assert.Equal(-0.8, result.Beta, 1e-10);
        Assert.True(result.Correlation < 0.0);
        Assert.Contains("противоположно рынку", result.Conclusion);
    }

    [Fact]
    public void Информационный_коэффициент_нулевой_при_совпадении_с_эталоном()
    {
        var market = new double[200];

        for (var i = 0; i < market.Length; i++)
        {
            market[i] = Math.Sin(i * 0.6) * 0.01;
        }

        var result = CapmCalculator.Calculate(market, market, riskFreeRateAnnual: 0.0);

        Assert.Equal(0.0, result.TrackingErrorAnnualized, 1e-12);
        Assert.Equal(0.0, result.InformationRatio, 1e-12);
    }

    [Fact]
    public void Случайный_шум_не_даёт_значимой_альфы()
    {
        // Инструмент полностью повторяет рынок с добавлением шума
        // с нулевым средним: альфа должна оказаться незначимой.
        var market = new double[1000];
        var asset = new double[1000];

        for (var i = 0; i < market.Length; i++)
        {
            market[i] = Math.Sin(i * 0.23) * 0.012;
            asset[i] = market[i] + (i % 2 == 0 ? 0.004 : -0.004);
        }

        var result = CapmCalculator.Calculate(asset, market, riskFreeRateAnnual: 0.0);

        Assert.False(
            result.AlphaIsSignificant,
            $"Альфа не должна быть значимой, t-статистика составила {result.AlphaTStatistic:F3}.");
        Assert.Contains("не значима", result.Conclusion);
    }

    [Fact]
    public void Ряды_различной_длины_приводят_к_исключению()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CapmCalculator.Calculate(new double[100], new double[50], 0.0));

        Assert.Contains("различную длину", exception.Message);
    }

    [Fact]
    public void Недостаточная_выборка_приводит_к_исключению()
    {
        Assert.Throws<ArgumentException>(
            () => CapmCalculator.Calculate(new double[10], new double[10], 0.0));
    }
}
