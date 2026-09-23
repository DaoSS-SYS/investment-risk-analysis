using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Random;
using RiskAnalysis.RiskEngine.Statistics;

namespace RiskAnalysis.RiskEngine.Var;

/// <summary>Параметры моделирования методом Монте-Карло.</summary>
/// <param name="ScenarioCount">Число сценариев.</param>
/// <param name="Distribution">Закон распределения случайных возмущений.</param>
/// <param name="StudentTDegreesOfFreedom">
/// Число степеней свободы распределения Стьюдента. Значение 4–6 соответствует
/// эксцессу, наблюдаемому у доходностей акций.
/// </param>
/// <param name="RandomSeed">
/// Начальное значение генератора псевдослучайных чисел. Задание значения
/// обеспечивает воспроизводимость расчёта: повторный запуск с теми же
/// параметрами даёт тот же результат.
/// </param>
public sealed record MonteCarloOptions(
    int ScenarioCount = 10_000,
    SimulationDistribution Distribution = SimulationDistribution.Normal,
    double StudentTDegreesOfFreedom = 5.0,
    int? RandomSeed = 20260923);

/// <summary>
/// Метод Монте-Карло для оценки стоимостной меры риска.
///
/// Метод состоит в многократном моделировании изменения стоимости портфеля и
/// построении эмпирического распределения его доходности по полученным
/// сценариям. Оценки стоимостной меры риска и ожидаемых потерь определяются
/// как соответствующие характеристики этого распределения.
///
/// Моделирование взаимосвязанных доходностей. Доходности инструментов
/// портфеля не являются независимыми: падение рынка затрагивает бумаги
/// одновременно. Если моделировать их независимо, риск портфеля окажется
/// существенно заниженным за счёт мнимого эффекта диверсификации.
///
/// Требуемая структура взаимосвязей воспроизводится разложением Холецкого
/// ковариационной матрицы. Симметричная положительно определённая матрица Σ
/// представляется в виде произведения
///
///     Σ = L · Lᵀ,
///
/// где L — нижняя треугольная матрица. Если z — вектор независимых
/// стандартных нормальных величин, то вектор L · z имеет нулевое среднее
/// и ковариационную матрицу
///
///     E[(L·z)(L·z)ᵀ] = L · E[z·zᵀ] · Lᵀ = L · I · Lᵀ = Σ,
///
/// то есть в точности требуемую. Доходность инструмента i в сценарии
/// на горизонте h торговых дней определяется как
///
///     r(i) = μ(i) · h + √h · (L · z)(i).
///
/// Доходность портфеля рассчитывается через простые доходности инструментов,
/// поскольку именно они аддитивны по составу портфеля:
///
///     R = Σ w(i) · (exp(r(i)) − 1).
///
/// Законы распределения. Моделирование по нормальному распределению даёт
/// оценку, асимптотически совпадающую с параметрическим методом, и служит для
/// его проверки. Распределение Стьюдента и историческая бутстрэп-выборка
/// воспроизводят «тяжёлые хвосты» фактического распределения доходностей.
/// </summary>
public static class MonteCarloVarCalculator
{
    /// <summary>
    /// Рассчитывает стоимостную меру риска по одному инструменту.
    /// </summary>
    /// <param name="returns">Ряд логарифмических доходностей.</param>
    /// <param name="confidenceLevel">Уровень доверия.</param>
    /// <param name="horizonDays">Горизонт оценки в торговых днях.</param>
    /// <param name="portfolioValue">Стоимость портфеля.</param>
    /// <param name="options">Параметры моделирования.</param>
    public static VarResult Calculate(
        IReadOnlyList<double> returns,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        double portfolioValue = 1.0,
        MonteCarloOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(returns);

        return CalculatePortfolio(
            weights: [1.0],
            returns: [returns.ToArray()],
            confidenceLevel,
            horizonDays,
            portfolioValue,
            options);
    }

    /// <summary>
    /// Рассчитывает стоимостную меру риска портфеля.
    /// </summary>
    /// <param name="weights">
    /// Вектор долей инструментов в портфеле. Сумма долей должна быть равна
    /// единице.
    /// </param>
    /// <param name="returns">
    /// Матрица логарифмических доходностей: первый индекс — инструмент,
    /// второй — наблюдение. Ряды должны быть приведены к общему торговому
    /// календарю.
    /// </param>
    /// <param name="confidenceLevel">Уровень доверия.</param>
    /// <param name="horizonDays">Горизонт оценки в торговых днях.</param>
    /// <param name="portfolioValue">Стоимость портфеля.</param>
    /// <param name="options">Параметры моделирования.</param>
    public static VarResult CalculatePortfolio(
        IReadOnlyList<double> weights,
        IReadOnlyList<double[]> returns,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        double portfolioValue = 1.0,
        MonteCarloOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(returns);

        options ??= new MonteCarloOptions();

        VarConventions.ValidateConfidenceLevel(confidenceLevel);
        VarConventions.ValidateHorizon(horizonDays);
        VarConventions.ValidatePortfolioValue(portfolioValue);

        if (options.ScenarioCount < 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Число сценариев должно составлять не менее тысячи: при меньшем объёме " +
                "погрешность оценки квантиля недопустимо велика.");
        }

        if (weights.Count != returns.Count)
        {
            throw new ArgumentException(
                "Число долей не соответствует числу рядов доходностей.", nameof(weights));
        }

        var scenarios = options.Distribution == SimulationDistribution.HistoricalBootstrap
            ? SimulateBootstrap(weights, returns, horizonDays, options)
            : SimulateParametric(weights, returns, horizonDays, options);

        return BuildResult(
            scenarios, weights, returns, confidenceLevel, horizonDays, portfolioValue, options);
    }

    // -----------------------------------------------------------------------
    // Моделирование по заданному закону распределения
    // -----------------------------------------------------------------------

    /// <summary>
    /// Формирует сценарии по нормальному распределению либо по распределению
    /// Стьюдента с применением разложения Холецкого.
    /// </summary>
    private static double[] SimulateParametric(
        IReadOnlyList<double> weights,
        IReadOnlyList<double[]> returns,
        int horizonDays,
        MonteCarloOptions options)
    {
        var assetCount = returns.Count;

        var means = CovarianceCalculator.Means(returns);
        var covariance = CovarianceCalculator.Covariance(returns);

        var factor = CholeskyFactor(covariance);

        var random = options.RandomSeed is null
            ? new MersenneTwister(threadSafe: false)
            : new MersenneTwister(options.RandomSeed.Value, threadSafe: false);

        // Нормирующий множитель распределения Стьюдента: дисперсия
        // распределения с ν степенями свободы равна ν / (ν − 2), поэтому
        // для получения единичной дисперсии величина делится на корень
        // из этого отношения.
        var degreesOfFreedom = options.StudentTDegreesOfFreedom;

        if (options.Distribution == SimulationDistribution.StudentT && degreesOfFreedom <= 2.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Число степеней свободы распределения Стьюдента должно превышать два: " +
                "при меньшем значении дисперсия распределения не определена.");
        }

        var studentScale = options.Distribution == SimulationDistribution.StudentT
            ? Math.Sqrt(degreesOfFreedom / (degreesOfFreedom - 2.0))
            : 1.0;

        var horizonScale = Math.Sqrt(horizonDays);

        var scenarios = new double[options.ScenarioCount];
        var shocks = new double[assetCount];
        var independent = new double[assetCount];

        for (var s = 0; s < options.ScenarioCount; s++)
        {
            // Независимые стандартные величины с единичной дисперсией.
            for (var i = 0; i < assetCount; i++)
            {
                independent[i] = options.Distribution == SimulationDistribution.StudentT
                    ? StudentT.Sample(random, 0.0, 1.0, degreesOfFreedom) / studentScale
                    : Normal.Sample(random, 0.0, 1.0);
            }

            // Применение разложения Холецкого: shocks = L · z.
            // Матрица нижняя треугольная, поэтому суммирование ведётся
            // только до текущего индекса.
            for (var i = 0; i < assetCount; i++)
            {
                double sum = 0.0;

                for (var j = 0; j <= i; j++)
                {
                    sum += factor[i, j] * independent[j];
                }

                shocks[i] = sum;
            }

            double portfolioReturn = 0.0;

            for (var i = 0; i < assetCount; i++)
            {
                var logReturn = means[i] * horizonDays + horizonScale * shocks[i];

                // Переход к простой доходности: только она аддитивна
                // по составу портфеля.
                portfolioReturn += weights[i] * (Math.Exp(logReturn) - 1.0);
            }

            scenarios[s] = portfolioReturn;
        }

        return scenarios;
    }

    /// <summary>
    /// Формирует сценарии исторической бутстрэп-выборкой.
    ///
    /// На каждый день горизонта случайно выбирается один из фактически
    /// наблюдавшихся торговых дней, и доходности всех инструментов берутся
    /// за этот день целиком. Совместный выбор сохраняет наблюдавшиеся
    /// взаимосвязи между инструментами без построения ковариационной матрицы
    /// и без предположений о законе распределения.
    /// </summary>
    private static double[] SimulateBootstrap(
        IReadOnlyList<double> weights,
        IReadOnlyList<double[]> returns,
        int horizonDays,
        MonteCarloOptions options)
    {
        var assetCount = returns.Count;
        var observationCount = returns[0].Length;

        for (var i = 1; i < assetCount; i++)
        {
            if (returns[i].Length != observationCount)
            {
                throw new ArgumentException(
                    "Ряды доходностей имеют различную длину. Перед моделированием ряды " +
                    "должны быть приведены к общему торговому календарю.",
                    nameof(returns));
            }
        }

        var random = options.RandomSeed is null
            ? new MersenneTwister(threadSafe: false)
            : new MersenneTwister(options.RandomSeed.Value, threadSafe: false);

        var scenarios = new double[options.ScenarioCount];
        var accumulated = new double[assetCount];

        for (var s = 0; s < options.ScenarioCount; s++)
        {
            Array.Clear(accumulated);

            for (var day = 0; day < horizonDays; day++)
            {
                var index = random.Next(observationCount);

                for (var i = 0; i < assetCount; i++)
                {
                    accumulated[i] += returns[i][index];
                }
            }

            double portfolioReturn = 0.0;

            for (var i = 0; i < assetCount; i++)
            {
                portfolioReturn += weights[i] * (Math.Exp(accumulated[i]) - 1.0);
            }

            scenarios[s] = portfolioReturn;
        }

        return scenarios;
    }

    // -----------------------------------------------------------------------
    // Вспомогательные операции
    // -----------------------------------------------------------------------

    /// <summary>
    /// Выполняет разложение Холецкого ковариационной матрицы.
    ///
    /// Разложение существует для симметричной положительно определённой
    /// матрицы. Выборочная ковариационная матрица может оказаться вырожденной
    /// при линейной зависимости рядов доходностей либо при числе наблюдений,
    /// не превышающем числа инструментов. В этом случае к главной диагонали
    /// добавляется малая величина, что соответствует приёму регуляризации
    /// и восстанавливает положительную определённость, практически не изменяя
    /// оценок риска.
    /// </summary>
    private static Matrix<double> CholeskyFactor(Matrix<double> covariance)
    {
        try
        {
            return covariance.Cholesky().Factor;
        }
        catch (ArgumentException)
        {
            var size = covariance.RowCount;

            double trace = 0.0;

            for (var i = 0; i < size; i++)
            {
                trace += covariance[i, i];
            }

            var ridge = Math.Max(trace / size * 1e-8, 1e-14);
            var regularized = covariance.Clone();

            for (var i = 0; i < size; i++)
            {
                regularized[i, i] += ridge;
            }

            return regularized.Cholesky().Factor;
        }
    }

    /// <summary>
    /// Формирует результат по полученному множеству сценариев.
    /// </summary>
    private static VarResult BuildResult(
        double[] scenarios,
        IReadOnlyList<double> weights,
        IReadOnlyList<double[]> returns,
        double confidenceLevel,
        int horizonDays,
        double portfolioValue,
        MonteCarloOptions options)
    {
        var tailProbability = 1.0 - confidenceLevel;

        var quantile = DescriptiveStatisticsCalculator.Quantile(scenarios, tailProbability);

        double tailSum = 0.0;
        var tailCount = 0;

        for (var s = 0; s < scenarios.Length; s++)
        {
            if (scenarios[s] <= quantile)
            {
                tailSum += scenarios[s];
                tailCount++;
            }
        }

        var tailMean = tailCount > 0 ? tailSum / tailCount : quantile;

        var statistics = DescriptiveStatisticsCalculator.Calculate(scenarios);

        var valueAtRisk = -quantile;
        var expectedShortfall = -tailMean;

        var distributionName = options.Distribution switch
        {
            SimulationDistribution.Normal => "нормальное распределение",
            SimulationDistribution.StudentT =>
                $"распределение Стьюдента, {options.StudentTDegreesOfFreedom:0.#} степеней свободы",
            SimulationDistribution.HistoricalBootstrap => "историческая бутстрэп-выборка",
            _ => "неизвестное распределение"
        };

        return new VarResult(
            Method: VarMethod.MonteCarlo,
            ConfidenceLevel: confidenceLevel,
            HorizonDays: horizonDays,
            ValueAtRiskRelative: valueAtRisk,
            ExpectedShortfallRelative: expectedShortfall,
            ValueAtRiskAbsolute: valueAtRisk * portfolioValue,
            ExpectedShortfallAbsolute: expectedShortfall * portfolioValue,
            PortfolioValue: portfolioValue,
            ObservationCount: scenarios.Length,
            Mean: statistics.Mean,
            StandardDeviation: statistics.StandardDeviation,
            Description:
                $"Метод Монте-Карло, уровень доверия {confidenceLevel:P0}, горизонт " +
                $"{horizonDays} торг. дн. Смоделировано {scenarios.Length} сценариев, " +
                $"закон распределения — {distributionName}. Инструментов в портфеле: " +
                $"{weights.Count}, наблюдений в выборке: {returns[0].Length}. " +
                $"Начальное значение генератора: " +
                $"{(options.RandomSeed?.ToString() ?? "не задано")}.");
    }
}
