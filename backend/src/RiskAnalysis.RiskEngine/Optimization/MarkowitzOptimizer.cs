using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Random;
using RiskAnalysis.RiskEngine.Returns;
using RiskAnalysis.RiskEngine.Statistics;

namespace RiskAnalysis.RiskEngine.Optimization;

/// <summary>
/// Точка множества портфелей: сочетание ожидаемой доходности, риска
/// и структуры вложений.
/// </summary>
/// <param name="ExpectedReturn">Ожидаемая доходность в годовом выражении.</param>
/// <param name="Volatility">Волатильность в годовом выражении.</param>
/// <param name="SharpeRatio">Коэффициент Шарпа.</param>
/// <param name="Weights">Доли инструментов в портфеле.</param>
public sealed record PortfolioPoint(
    double ExpectedReturn,
    double Volatility,
    double SharpeRatio,
    IReadOnlyList<double> Weights);

/// <summary>
/// Результат оптимизации структуры портфеля.
/// </summary>
/// <param name="EfficientFrontier">Точки эффективной границы.</param>
/// <param name="MinimumVariance">Портфель наименьшей дисперсии.</param>
/// <param name="MaximumSharpe">Касательный портфель — портфель наибольшего коэффициента Шарпа.</param>
/// <param name="CurrentPortfolio">Текущая структура портфеля, если она задана.</param>
/// <param name="RandomPortfolios">
/// Множество портфелей со случайной структурой. Служит наглядным
/// представлением области допустимых сочетаний доходности и риска.
/// </param>
/// <param name="RiskFreeRateAnnual">Безрисковая ставка в годовом выражении.</param>
/// <param name="MaximumWeight">Предельная доля одного инструмента.</param>
/// <param name="ObservationCount">Число наблюдений, по которым получены оценки.</param>
/// <param name="Conclusion">Вывод по результатам оптимизации.</param>
public sealed record MarkowitzResult(
    IReadOnlyList<PortfolioPoint> EfficientFrontier,
    PortfolioPoint MinimumVariance,
    PortfolioPoint MaximumSharpe,
    PortfolioPoint? CurrentPortfolio,
    IReadOnlyList<PortfolioPoint> RandomPortfolios,
    double RiskFreeRateAnnual,
    double MaximumWeight,
    int ObservationCount,
    string Conclusion);

/// <summary>
/// Оптимизация структуры инвестиционного портфеля по модели Марковица.
///
/// Модель исходит из того, что инвестор оценивает портфель двумя величинами:
/// ожидаемой доходностью и её дисперсией. Задача состоит в отыскании структуры
/// вложений, при которой для заданного уровня доходности дисперсия минимальна,
/// либо, что равносильно, для заданного уровня риска доходность максимальна.
/// Совокупность таких портфелей образует эффективную границу.
///
/// Ожидаемая доходность портфеля равна взвешенной сумме ожидаемых доходностей
/// инструментов, а дисперсия определяется квадратичной формой:
///
///     E[R] = wᵀ · μ,    σ² = wᵀ · Σ · w.
///
/// Именно недиагональные элементы ковариационной матрицы делают задачу
/// содержательной: при корреляции инструментов ниже единицы риск портфеля
/// оказывается меньше средневзвешенного риска составляющих.
///
/// Постановка задачи. Рассматривается задача с ограничениями
///
///     Σ w(i) = 1,    0 ≤ w(i) ≤ w(max),
///
/// то есть без коротких продаж и с предельной долей одного инструмента.
/// Запрет коротких продаж соответствует условиям деятельности большинства
/// инвесторов; ограничение доли препятствует получению решений,
/// сосредоточенных в одном инструменте.
///
/// Способ построения эффективной границы. Граница строится не перебором
/// целевых уровней доходности, а параметризацией по коэффициенту неприятия
/// риска γ: для каждого значения γ решается задача
///
///     max [ wᵀ · μ − (γ / 2) · wᵀ · Σ · w ]    при Σ w(i) = 1, 0 ≤ w(i) ≤ w(max).
///
/// При γ → 0 решением является портфель наибольшей доходности, при γ → ∞ —
/// портфель наименьшей дисперсии; промежуточные значения дают промежуточные
/// точки границы. Такая параметризация исключает необходимость проверки
/// достижимости целевой доходности и приводит задачу к виду, содержащему
/// только ограничения на область допустимых значений.
///
/// Метод решения. Применяется метод проекции градиента: на каждом шаге
/// выполняется шаг в направлении антиградиента, после чего полученная точка
/// проецируется на область допустимых значений. Проекция на множество
/// { w : Σ w(i) = 1, 0 ≤ w(i) ≤ w(max) } вычисляется отысканием такого
/// значения θ, при котором сумма усечённых величин clamp(v(i) − θ, 0, w(max))
/// равна единице. Эта сумма монотонно убывает по θ, поэтому θ отыскивается
/// делением отрезка пополам.
///
/// Длина шага принимается равной величине, обратной константе Липшица
/// градиента, равной γ, умноженному на наибольшее собственное число
/// ковариационной матрицы. При таком выборе метод сходится для любой
/// выпуклой задачи рассматриваемого вида.
/// </summary>
public static class MarkowitzOptimizer
{
    /// <summary>Число итераций метода проекции градиента.</summary>
    private const int MaximumIterations = 5000;

    /// <summary>Порог сходимости по норме изменения вектора долей.</summary>
    private const double ConvergenceTolerance = 1e-12;

    /// <summary>Число итераций деления отрезка пополам при проекции.</summary>
    private const int ProjectionIterations = 200;

    /// <summary>
    /// Выполняет оптимизацию структуры портфеля.
    /// </summary>
    /// <param name="returns">
    /// Матрица логарифмических доходностей: первый индекс — инструмент,
    /// второй — наблюдение. Ряды должны быть приведены к общему торговому
    /// календарю.
    /// </param>
    /// <param name="riskFreeRateAnnual">Безрисковая ставка в годовом выражении.</param>
    /// <param name="currentWeights">
    /// Текущая структура портфеля для сопоставления с оптимальной.
    /// </param>
    /// <param name="maximumWeight">
    /// Предельная доля одного инструмента. Значение должно быть не меньше
    /// единицы, делённой на число инструментов, иначе область допустимых
    /// значений пуста.
    /// </param>
    /// <param name="frontierPoints">Число точек эффективной границы.</param>
    /// <param name="randomPortfolios">Число портфелей со случайной структурой.</param>
    /// <param name="randomSeed">Начальное значение генератора псевдослучайных чисел.</param>
    public static MarkowitzResult Optimize(
        IReadOnlyList<double[]> returns,
        double riskFreeRateAnnual,
        IReadOnlyList<double>? currentWeights = null,
        double maximumWeight = 1.0,
        int frontierPoints = 60,
        int randomPortfolios = 3000,
        int randomSeed = 20260925)
    {
        ArgumentNullException.ThrowIfNull(returns);

        var assetCount = returns.Count;

        if (assetCount < 2)
        {
            throw new ArgumentException(
                "Оптимизация структуры портфеля требует не менее двух инструментов.",
                nameof(returns));
        }

        var minimumFeasibleWeight = 1.0 / assetCount;

        if (maximumWeight < minimumFeasibleWeight - 1e-12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumWeight),
                $"Предельная доля {maximumWeight:P0} недостижима при {assetCount} инструментах: " +
                $"она должна составлять не менее {minimumFeasibleWeight:P1}.");
        }

        var observationCount = returns[0].Length;

        for (var i = 1; i < assetCount; i++)
        {
            if (returns[i].Length != observationCount)
            {
                throw new ArgumentException(
                    "Ряды доходностей имеют различную длину. Перед оптимизацией ряды должны " +
                    "быть приведены к общему торговому календарю.", nameof(returns));
            }
        }

        var means = CovarianceCalculator.Means(returns);
        var covariance = CovarianceCalculator.Covariance(returns);

        // Наибольшее собственное число ковариационной матрицы определяет
        // константу Липшица градиента и, следовательно, длину шага.
        var largestEigenvalue = covariance.Evd().EigenValues
            .Select(value => value.Real)
            .DefaultIfEmpty(0.0)
            .Max();

        if (largestEigenvalue <= 0.0)
        {
            throw new ArgumentException(
                "Ковариационная матрица вырождена: оптимизация невозможна.", nameof(returns));
        }

        var periodRiskFree = Performance.PerformanceCalculator.PeriodRiskFreeRate(riskFreeRateAnnual);

        // Эффективная граница. Значения коэффициента неприятия риска
        // распределены по логарифмической шкале: при малых значениях точки
        // сгущаются у портфеля наибольшей доходности, при больших —
        // у портфеля наименьшей дисперсии.
        var frontier = new List<PortfolioPoint>(frontierPoints);

        for (var i = 0; i < frontierPoints; i++)
        {
            var fraction = (double)i / Math.Max(1, frontierPoints - 1);
            var riskAversion = Math.Pow(10.0, -1.0 + 5.0 * fraction);

            var weights = SolveMeanVariance(
                means, covariance, riskAversion, maximumWeight, largestEigenvalue);

            frontier.Add(Evaluate(weights, means, covariance, periodRiskFree));
        }

        // Точки границы упорядочиваются по возрастанию риска, совпадающие
        // точки исключаются: при больших значениях коэффициента неприятия
        // риска решения практически не различаются.
        var orderedFrontier = frontier
            .OrderBy(point => point.Volatility)
            .Where((point, index) =>
                index == 0 ||
                Math.Abs(point.Volatility - frontier.OrderBy(p => p.Volatility)
                    .ElementAt(index - 1).Volatility) > 1e-9)
            .ToList();

        var minimumVariance = orderedFrontier.MinBy(point => point.Volatility)!;
        var maximumSharpe = orderedFrontier.MaxBy(point => point.SharpeRatio)!;

        var random = new MersenneTwister(randomSeed, threadSafe: false);
        var cloud = new List<PortfolioPoint>(randomPortfolios);

        for (var s = 0; s < randomPortfolios; s++)
        {
            var weights = RandomWeights(random, assetCount, maximumWeight);
            cloud.Add(Evaluate(weights, means, covariance, periodRiskFree));
        }

        PortfolioPoint? current = null;

        if (currentWeights is not null && currentWeights.Count == assetCount)
        {
            current = Evaluate(currentWeights, means, covariance, periodRiskFree);
        }

        return new MarkowitzResult(
            EfficientFrontier: orderedFrontier,
            MinimumVariance: minimumVariance,
            MaximumSharpe: maximumSharpe,
            CurrentPortfolio: current,
            RandomPortfolios: cloud,
            RiskFreeRateAnnual: riskFreeRateAnnual,
            MaximumWeight: maximumWeight,
            ObservationCount: observationCount,
            Conclusion: BuildConclusion(current, minimumVariance, maximumSharpe));
    }

    /// <summary>
    /// Решает задачу максимизации взвешенной разности ожидаемой доходности
    /// и дисперсии методом проекции градиента.
    /// </summary>
    private static double[] SolveMeanVariance(
        double[] means,
        Matrix<double> covariance,
        double riskAversion,
        double maximumWeight,
        double largestEigenvalue)
    {
        var count = means.Length;

        // Начальное приближение — равновзвешенный портфель, приведённый
        // к области допустимых значений.
        var weights = Project(
            Enumerable.Repeat(1.0 / count, count).ToArray(), maximumWeight);

        var step = 1.0 / (riskAversion * largestEigenvalue);
        var gradient = new double[count];
        var candidate = new double[count];

        for (var iteration = 0; iteration < MaximumIterations; iteration++)
        {
            // Градиент минимизируемой функции (γ/2)·wᵀΣw − wᵀμ
            // равен γ·Σw − μ.
            for (var i = 0; i < count; i++)
            {
                double value = 0.0;

                for (var j = 0; j < count; j++)
                {
                    value += covariance[i, j] * weights[j];
                }

                gradient[i] = riskAversion * value - means[i];
                candidate[i] = weights[i] - step * gradient[i];
            }

            var projected = Project(candidate, maximumWeight);

            double difference = 0.0;

            for (var i = 0; i < count; i++)
            {
                var delta = projected[i] - weights[i];
                difference += delta * delta;
            }

            Array.Copy(projected, weights, count);

            if (difference < ConvergenceTolerance)
            {
                break;
            }
        }

        return weights;
    }

    /// <summary>
    /// Проецирует вектор на множество { w : Σ w(i) = 1, 0 ≤ w(i) ≤ w(max) }.
    ///
    /// Проекция имеет вид w(i) = clamp(v(i) − θ, 0, w(max)), где θ выбирается
    /// из условия равенства суммы единице. Сумма монотонно убывает по θ,
    /// поэтому значение отыскивается делением отрезка пополам.
    /// </summary>
    private static double[] Project(IReadOnlyList<double> values, double maximumWeight)
    {
        var count = values.Count;

        var low = values.Min() - 1.0;
        var high = values.Max();

        for (var iteration = 0; iteration < ProjectionIterations; iteration++)
        {
            var middle = 0.5 * (low + high);

            double sum = 0.0;

            for (var i = 0; i < count; i++)
            {
                sum += Math.Clamp(values[i] - middle, 0.0, maximumWeight);
            }

            if (sum > 1.0)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        var theta = 0.5 * (low + high);
        var result = new double[count];

        double total = 0.0;

        for (var i = 0; i < count; i++)
        {
            result[i] = Math.Clamp(values[i] - theta, 0.0, maximumWeight);
            total += result[i];
        }

        // Устранение погрешности деления отрезка: сумма долей
        // приводится в точности к единице.
        if (total > 0.0)
        {
            for (var i = 0; i < count; i++)
            {
                result[i] /= total;
            }
        }

        return result;
    }

    /// <summary>
    /// Формирует случайную структуру портфеля, удовлетворяющую ограничениям.
    /// Доли получаются нормированием независимых экспоненциальных величин,
    /// что даёт равномерное распределение на симплексе, после чего
    /// выполняется приведение к области допустимых значений.
    /// </summary>
    private static double[] RandomWeights(Random random, int count, double maximumWeight)
    {
        var weights = new double[count];
        double total = 0.0;

        for (var i = 0; i < count; i++)
        {
            weights[i] = -Math.Log(1.0 - random.NextDouble());
            total += weights[i];
        }

        for (var i = 0; i < count; i++)
        {
            weights[i] /= total;
        }

        return maximumWeight >= 1.0 ? weights : Project(weights, maximumWeight);
    }

    /// <summary>
    /// Рассчитывает характеристики портфеля с заданной структурой
    /// в годовом выражении.
    /// </summary>
    private static PortfolioPoint Evaluate(
        IReadOnlyList<double> weights,
        double[] means,
        Matrix<double> covariance,
        double periodRiskFree)
    {
        double periodReturn = 0.0;

        for (var i = 0; i < weights.Count; i++)
        {
            periodReturn += weights[i] * means[i];
        }

        var periodDeviation = CovarianceCalculator.PortfolioStandardDeviation(weights, covariance);

        var annualFactor = ReturnCalculator.TradingDaysPerYear;
        var annualReturn = periodReturn * annualFactor;
        var annualVolatility = periodDeviation * Math.Sqrt(annualFactor);

        var excessReturn = (periodReturn - periodRiskFree) * annualFactor;

        var sharpe = annualVolatility > 0 ? excessReturn / annualVolatility : 0.0;

        return new PortfolioPoint(
            ExpectedReturn: annualReturn,
            Volatility: annualVolatility,
            SharpeRatio: sharpe,
            Weights: weights.ToArray());
    }

    private static string BuildConclusion(
        PortfolioPoint? current,
        PortfolioPoint minimumVariance,
        PortfolioPoint maximumSharpe)
    {
        var baseText =
            $"Портфель наименьшей дисперсии: волатильность {minimumVariance.Volatility:P2} " +
            $"годовых при ожидаемой доходности {minimumVariance.ExpectedReturn:P2}. " +
            $"Касательный портфель: коэффициент Шарпа {maximumSharpe.SharpeRatio:F3} " +
            $"при волатильности {maximumSharpe.Volatility:P2} и доходности " +
            $"{maximumSharpe.ExpectedReturn:P2} годовых.";

        if (current is null)
        {
            return baseText;
        }

        var volatilityReduction = current.Volatility > 0
            ? 1.0 - minimumVariance.Volatility / current.Volatility
            : 0.0;

        var comparison = current.SharpeRatio < maximumSharpe.SharpeRatio
            ? $"Текущая структура портфеля расположена ниже эффективной границы: " +
              $"коэффициент Шарпа {current.SharpeRatio:F3} против {maximumSharpe.SharpeRatio:F3} " +
              $"у касательного портфеля. Переход к портфелю наименьшей дисперсии снизил бы " +
              $"волатильность с {current.Volatility:P2} до {minimumVariance.Volatility:P2}, " +
              $"то есть на {volatilityReduction:P1}."
            : "Текущая структура портфеля не уступает оптимальной по коэффициенту Шарпа.";

        return $"{baseText} {comparison}";
    }
}
