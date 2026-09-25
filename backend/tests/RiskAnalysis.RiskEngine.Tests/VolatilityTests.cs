using MathNet.Numerics.Distributions;
using RiskAnalysis.RiskEngine.Statistics;
using RiskAnalysis.RiskEngine.Var;
using RiskAnalysis.RiskEngine.Volatility;
using Xunit;

namespace RiskAnalysis.RiskEngine.Tests;

/// <summary>
/// Проверка корректности моделей условной волатильности и построенных
/// на них оценок стоимостной меры риска.
/// </summary>
public class VolatilityTests
{
    private const double Tolerance = 1e-9;

    /// <summary>
    /// Ряд с постоянной волатильностью: значения равны квантилям нормального
    /// распределения, переставленным заданным образом.
    /// </summary>
    private static double[] ConstantVolatility(int count, double deviation, int shift = 0)
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

    /// <summary>
    /// Ряд с изменяющейся волатильностью: первая половина спокойная,
    /// вторая — с повышенной волатильностью.
    /// </summary>
    private static double[] RegimeChange(int count, double calm, double turbulent)
    {
        var result = ConstantVolatility(count, 1.0);

        for (var i = 0; i < count; i++)
        {
            result[i] *= i < count / 2 ? calm : turbulent;
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // Экспоненциально взвешенная оценка
    // -----------------------------------------------------------------------

    [Fact]
    public void Оценка_EWMA_следует_рекуррентному_соотношению()
    {
        var returns = ConstantVolatility(500, 0.02);

        var result = EwmaCalculator.Calculate(returns, lambda: 0.94);

        // Проверка соотношения σ²(t+1) = λ·σ²(t) + (1−λ)·r²(t)
        // на произвольном участке ряда.
        for (var t = 100; t < 110; t++)
        {
            var current = result.ConditionalVolatility[t];
            var next = result.ConditionalVolatility[t + 1];

            var expected = Math.Sqrt(
                0.94 * current * current + 0.06 * returns[t] * returns[t]);

            Assert.Equal(expected, next, 1e-12);
        }
    }

    [Fact]
    public void Период_полураспада_веса_соответствует_параметру_сглаживания()
    {
        var returns = ConstantVolatility(300, 0.02);

        var result = EwmaCalculator.Calculate(returns, lambda: 0.94);

        // ln(2) / ln(1 / 0,94) ≈ 11,2 торгового дня.
        Assert.Equal(
            Math.Log(2.0) / Math.Log(1.0 / 0.94),
            result.Parameters["halfLifeDays"],
            Tolerance);

        Assert.Equal(11.2, result.Parameters["halfLifeDays"], 1);
    }

    [Fact]
    public void Оценка_EWMA_реагирует_на_изменение_режима_волатильности()
    {
        // Волатильность возрастает вчетверо во второй половине ряда.
        var returns = RegimeChange(600, calm: 0.005, turbulent: 0.020);

        var result = EwmaCalculator.Calculate(returns);

        // Оценка в конце спокойного участка и в конце бурного участка
        // должны существенно различаться.
        var calmEstimate = result.ConditionalVolatility[290];
        var turbulentEstimate = result.ConditionalVolatility[599];

        Assert.True(
            turbulentEstimate > calmEstimate * 2.0,
            $"Оценка на спокойном участке {calmEstimate:F5}, на бурном {turbulentEstimate:F5}: " +
            "модель должна была отреагировать на изменение режима.");
    }

    [Fact]
    public void Безусловная_оценка_лежит_между_крайними_условными()
    {
        var returns = RegimeChange(600, calm: 0.005, turbulent: 0.020);

        var result = EwmaCalculator.Calculate(returns);

        Assert.True(result.MinimumVolatility < result.UnconditionalVolatility);
        Assert.True(result.MaximumVolatility > result.UnconditionalVolatility);
    }

    [Fact]
    public void Недопустимый_параметр_сглаживания_приводит_к_исключению()
    {
        var returns = ConstantVolatility(200, 0.02);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => EwmaCalculator.Calculate(returns, lambda: 1.0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => EwmaCalculator.Calculate(returns, lambda: 0.0));
    }

    // -----------------------------------------------------------------------
    // Модель GARCH(1,1)
    // -----------------------------------------------------------------------

    [Fact]
    public void Параметры_GARCH_удовлетворяют_ограничениям()
    {
        var returns = ConstantVolatility(1000, 0.02);

        var result = GarchCalculator.Calculate(returns);

        var omega = result.Parameters["omega"];
        var alpha = result.Parameters["alpha"];
        var beta = result.Parameters["beta"];

        Assert.True(omega > 0.0, "Постоянная составляющая должна быть положительной.");
        Assert.True(alpha >= 0.0, "Коэффициент при квадрате доходности неотрицателен.");
        Assert.True(beta >= 0.0, "Коэффициент при предыдущей дисперсии неотрицателен.");
        Assert.True(alpha + beta < 1.0, "Сумма коэффициентов должна быть меньше единицы.");
    }

    [Fact]
    public void Долгосрочная_волатильность_соответствует_формуле()
    {
        var returns = ConstantVolatility(800, 0.02);

        var result = GarchCalculator.Calculate(returns);

        var omega = result.Parameters["omega"];
        var persistence = result.Parameters["persistence"];

        // σ²(∞) = ω / (1 − α − β)
        var expected = Math.Sqrt(omega / (1.0 - persistence));

        Assert.Equal(expected, result.Parameters["longRunVolatility"], 1e-10);
    }

    [Fact]
    public void Правдоподобие_GARCH_не_ниже_правдоподобия_начального_приближения()
    {
        // Оценивание методом максимального правдоподобия обязано давать
        // значение не хуже начального приближения.
        var returns = RegimeChange(800, calm: 0.008, turbulent: 0.025);

        var result = GarchCalculator.Calculate(returns);

        Assert.False(double.IsNaN(result.LogLikelihood));
        Assert.False(double.IsNegativeInfinity(result.LogLikelihood));

        // Правдоподобие условной модели должно превышать правдоподобие
        // модели с постоянной дисперсией.
        var statistics = DescriptiveStatisticsCalculator.Calculate(returns);
        var constantVariance = statistics.Variance;

        double constantLogLikelihood = 0.0;

        foreach (var value in returns)
        {
            var centered = value - statistics.Mean;

            constantLogLikelihood -= 0.5 * (
                Math.Log(2.0 * Math.PI) + Math.Log(constantVariance) +
                centered * centered / constantVariance);
        }

        Assert.True(
            result.LogLikelihood > constantLogLikelihood,
            $"Правдоподобие GARCH {result.LogLikelihood:F2} не превысило правдоподобие " +
            $"модели с постоянной дисперсией {constantLogLikelihood:F2}.");
    }

    [Fact]
    public void Ряд_GARCH_реагирует_на_изменение_режима()
    {
        var returns = RegimeChange(800, calm: 0.006, turbulent: 0.024);

        var result = GarchCalculator.Calculate(returns);

        Assert.True(result.MaximumVolatility > result.MinimumVolatility * 2.0);
    }

    [Fact]
    public void Недостаточная_выборка_для_GARCH_приводит_к_исключению()
    {
        Assert.Throws<ArgumentException>(
            () => GarchCalculator.Calculate(ConstantVolatility(50, 0.02)));
    }

    // -----------------------------------------------------------------------
    // Оценки риска на условной волатильности
    // -----------------------------------------------------------------------

    [Fact]
    public void Оценка_на_EWMA_использует_прогноз_волатильности()
    {
        var returns = ConstantVolatility(500, 0.02);

        var volatility = EwmaCalculator.Calculate(returns);
        var statistics = DescriptiveStatisticsCalculator.Calculate(returns);

        var result = ConditionalVarCalculator.Ewma(returns, 0.99);

        // Оценка должна совпадать с параметрической, рассчитанной
        // по прогнозу условной волатильности.
        var expected = ParametricVarCalculator.Calculate(
            statistics.Mean, volatility.Forecast, returns.Length, 0.99);

        Assert.Equal(expected.ValueAtRiskRelative, result.ValueAtRiskRelative, 1e-12);
        Assert.Equal(VarMethod.EwmaParametric, result.Method);
    }

    [Fact]
    public void На_спокойном_участке_условная_оценка_ниже_безусловной()
    {
        // Ряд завершается спокойным участком после бурного: условная
        // оценка должна оказаться ниже безусловной, учитывающей
        // весь период с равными весами.
        var returns = ConstantVolatility(600, 1.0);

        for (var i = 0; i < returns.Length; i++)
        {
            returns[i] *= i < 300 ? 0.030 : 0.006;
        }

        var conditional = ConditionalVarCalculator.Ewma(returns, 0.99);
        var unconditional = ParametricVarCalculator.Calculate(returns, 0.99);

        Assert.True(
            conditional.ValueAtRiskRelative < unconditional.ValueAtRiskRelative,
            "После завершения периода повышенной волатильности условная оценка " +
            "должна снизиться быстрее безусловной.");
    }

    [Fact]
    public void На_бурном_участке_условная_оценка_выше_безусловной()
    {
        var returns = ConstantVolatility(600, 1.0);

        for (var i = 0; i < returns.Length; i++)
        {
            returns[i] *= i < 300 ? 0.006 : 0.030;
        }

        var conditional = ConditionalVarCalculator.Ewma(returns, 0.99);
        var unconditional = ParametricVarCalculator.Calculate(returns, 0.99);

        Assert.True(conditional.ValueAtRiskRelative > unconditional.ValueAtRiskRelative);
    }

    [Fact]
    public void Фильтрованное_моделирование_учитывает_тяжёлые_хвосты()
    {
        // Ряд с постоянной волатильностью, но с «тяжёлыми хвостами»:
        // квантиль стандартизованных доходностей по модулю превышает
        // квантиль нормального распределения, поэтому оценка должна
        // оказаться выше параметрической на том же прогнозе волатильности.
        var returns = ConstantVolatility(1000, 0.01);

        for (var i = 0; i < 20; i++)
        {
            returns[i * 47] = -0.09;
        }

        var filtered = ConditionalVarCalculator.FilteredHistorical(returns, 0.99);
        var parametric = ConditionalVarCalculator.Ewma(returns, 0.99);

        Assert.True(
            filtered.ValueAtRiskRelative > parametric.ValueAtRiskRelative,
            $"Фильтрованное моделирование дало {filtered.ValueAtRiskRelative:P3}, " +
            $"параметрическая оценка {parametric.ValueAtRiskRelative:P3}.");
    }

    [Fact]
    public void Ожидаемые_потери_не_меньше_меры_риска_во_всех_условных_методах()
    {
        var returns = RegimeChange(800, calm: 0.008, turbulent: 0.022);

        foreach (var result in new[]
                 {
                     ConditionalVarCalculator.Ewma(returns, 0.99),
                     ConditionalVarCalculator.Garch(returns, 0.99),
                     ConditionalVarCalculator.FilteredHistorical(returns, 0.99)
                 })
        {
            Assert.True(
                result.ExpectedShortfallRelative >= result.ValueAtRiskRelative,
                $"Метод {result.Method}: ожидаемые потери оказались ниже меры риска.");
        }
    }

    [Fact]
    public void Пересчёт_условной_оценки_на_горизонт_следует_правилу_корня()
    {
        var returns = ConstantVolatility(500, 0.02);

        var oneDay = ConditionalVarCalculator.Ewma(returns, 0.99, horizonDays: 1);
        var tenDays = ConditionalVarCalculator.Ewma(returns, 0.99, horizonDays: 10);

        // При нулевом среднем отношение оценок равно корню из десяти.
        // Среднее ряда близко к нулю, поэтому проверка ведётся с допуском.
        var ratio = tenDays.ValueAtRiskRelative / oneDay.ValueAtRiskRelative;

        Assert.Equal(Math.Sqrt(10.0), ratio, 0.05);
    }
}
