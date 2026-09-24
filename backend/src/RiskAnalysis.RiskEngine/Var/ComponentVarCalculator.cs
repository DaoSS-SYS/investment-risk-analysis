using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using RiskAnalysis.RiskEngine.Statistics;

namespace RiskAnalysis.RiskEngine.Var;

/// <summary>
/// Вклад отдельного инструмента в риск портфеля.
/// </summary>
/// <param name="Index">Порядковый номер инструмента в портфеле.</param>
/// <param name="Weight">Доля инструмента в портфеле.</param>
/// <param name="MarginalVar">
/// Предельная стоимостная мера риска — производная риска портфеля по доле
/// инструмента. Показывает, на сколько изменится риск портфеля при
/// бесконечно малом увеличении доли данного инструмента.
/// </param>
/// <param name="ComponentVar">
/// Компонентная стоимостная мера риска — вклад инструмента в общий риск
/// портфеля. Сумма компонентных мер по всем инструментам в точности равна
/// стоимостной мере риска портфеля.
/// </param>
/// <param name="ContributionShare">Доля инструмента в общем риске портфеля.</param>
/// <param name="StandaloneVar">
/// Стоимостная мера риска позиции, рассматриваемой обособленно, без учёта
/// её взаимосвязей с остальными позициями портфеля.
/// </param>
/// <param name="DiversificationBenefit">
/// Выигрыш от диверсификации по данной позиции: разность обособленной
/// и компонентной мер риска.
/// </param>
public sealed record RiskContribution(
    int Index,
    double Weight,
    double MarginalVar,
    double ComponentVar,
    double ContributionShare,
    double StandaloneVar,
    double DiversificationBenefit);

/// <summary>
/// Разложение риска портфеля по инструментам.
///
/// Стоимостная мера риска портфеля не равна сумме мер риска его составляющих:
/// вследствие неполной корреляции инструментов риск портфеля оказывается
/// меньше. Возникает вопрос о том, какую часть общего риска создаёт каждая
/// позиция.
///
/// Ответ даёт разложение Эйлера. Стоимостная мера риска является однородной
/// функцией первой степени от вектора долей: при пропорциональном увеличении
/// всех позиций риск возрастает во столько же раз. Для таких функций
/// справедливо тождество Эйлера
///
///     VaR(w) = Σ w(i) · ∂VaR / ∂w(i).
///
/// Слагаемые правой части называются компонентными мерами риска. Их сумма
/// в точности равна риску портфеля, что позволяет однозначно распределить
/// общий риск между позициями.
///
/// Для параметрического метода производная вычисляется аналитически.
/// Волатильность портфеля равна σ(p) = √(wᵀ·Σ·w), откуда
///
///     ∂σ(p) / ∂w(i) = (Σ·w)(i) / σ(p),
///
/// и, с учётом среднего значения доходности,
///
///     ∂VaR / ∂w(i) = −z(1−α) · (Σ·w)(i) / σ(p) − μ(i) · h.
///
/// Практическое значение разложения состоит в том, что позиция с небольшой
/// долей, но высокой корреляцией с остальным портфелем, может создавать
/// непропорционально большую часть риска, и наоборот — слабо связанная
/// позиция снижает общий риск, имея отрицательный вклад.
/// </summary>
public static class ComponentVarCalculator
{
    /// <summary>
    /// Выполняет разложение параметрической стоимостной меры риска портфеля
    /// по инструментам.
    /// </summary>
    /// <param name="weights">Вектор долей инструментов. Сумма долей равна единице.</param>
    /// <param name="means">Вектор средних доходностей инструментов за период.</param>
    /// <param name="covariance">Ковариационная матрица доходностей.</param>
    /// <param name="confidenceLevel">Уровень доверия.</param>
    /// <param name="horizonDays">Горизонт оценки в торговых днях.</param>
    /// <param name="portfolioValue">Стоимость портфеля.</param>
    public static IReadOnlyList<RiskContribution> Decompose(
        IReadOnlyList<double> weights,
        IReadOnlyList<double> means,
        Matrix<double> covariance,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        double portfolioValue = 1.0)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(means);
        ArgumentNullException.ThrowIfNull(covariance);

        VarConventions.ValidateConfidenceLevel(confidenceLevel);
        VarConventions.ValidateHorizon(horizonDays);
        VarConventions.ValidatePortfolioValue(portfolioValue);

        var count = weights.Count;

        if (means.Count != count || covariance.RowCount != count)
        {
            throw new ArgumentException(
                "Размерности вектора долей, вектора средних доходностей и " +
                "ковариационной матрицы не согласованы.", nameof(weights));
        }

        var portfolioDeviation = CovarianceCalculator.PortfolioStandardDeviation(weights, covariance);

        if (portfolioDeviation <= 0.0)
        {
            throw new ArgumentException(
                "Волатильность портфеля равна нулю: разложение риска невозможно.",
                nameof(covariance));
        }

        var lowerQuantile = Normal.InvCDF(0.0, 1.0, 1.0 - confidenceLevel);
        var horizonScale = Math.Sqrt(horizonDays);

        var contributions = new RiskContribution[count];

        for (var i = 0; i < count; i++)
        {
            // Элемент произведения ковариационной матрицы на вектор долей:
            // (Σ·w)(i) = Σ(j) Σ(i,j) · w(j).
            double covarianceWithPortfolio = 0.0;

            for (var j = 0; j < count; j++)
            {
                covarianceWithPortfolio += covariance[i, j] * weights[j];
            }

            // Производная риска портфеля по доле инструмента.
            var marginal =
                -lowerQuantile * horizonScale * covarianceWithPortfolio / portfolioDeviation
                - means[i] * horizonDays;

            var component = weights[i] * marginal;

            // Риск позиции, рассматриваемой обособленно.
            var standaloneDeviation = Math.Sqrt(covariance[i, i]);

            var standalone = weights[i] *
                (-lowerQuantile * horizonScale * standaloneDeviation - means[i] * horizonDays);

            contributions[i] = new RiskContribution(
                Index: i,
                Weight: weights[i],
                MarginalVar: marginal * portfolioValue,
                ComponentVar: component * portfolioValue,
                ContributionShare: 0.0,
                StandaloneVar: standalone * portfolioValue,
                DiversificationBenefit: (standalone - component) * portfolioValue);
        }

        // Доли вклада рассчитываются после получения всех компонент:
        // их сумма равна риску портфеля по тождеству Эйлера.
        var total = contributions.Sum(c => c.ComponentVar);

        if (Math.Abs(total) > 1e-12)
        {
            for (var i = 0; i < count; i++)
            {
                contributions[i] = contributions[i] with
                {
                    ContributionShare = contributions[i].ComponentVar / total
                };
            }
        }

        return contributions;
    }

    /// <summary>
    /// Выполняет разложение ожидаемых потерь по инструментам на основании
    /// множества сценариев.
    ///
    /// Для ожидаемых потерь разложение записывается в явном виде:
    ///
    ///     ES(p) = Σ w(i) · E[ r(i) | r(p) ≤ q ],
    ///
    /// где q — квантиль распределения доходности портфеля. Вклад инструмента
    /// определяется его средней доходностью именно в тех сценариях, которые
    /// образуют «хвост» распределения портфеля. Тождество выполняется точно,
    /// поскольку доходность портфеля в каждом сценарии равна взвешенной сумме
    /// доходностей инструментов.
    ///
    /// Способ применим как к историческим наблюдениям, так и к сценариям,
    /// полученным методом Монте-Карло, и не требует предположений о виде
    /// распределения.
    /// </summary>
    /// <param name="weights">Вектор долей инструментов.</param>
    /// <param name="assetScenarios">
    /// Простые доходности инструментов по сценариям: первый индекс —
    /// инструмент, второй — сценарий.
    /// </param>
    /// <param name="confidenceLevel">Уровень доверия.</param>
    /// <param name="portfolioValue">Стоимость портфеля.</param>
    public static IReadOnlyList<RiskContribution> DecomposeExpectedShortfall(
        IReadOnlyList<double> weights,
        IReadOnlyList<double[]> assetScenarios,
        double confidenceLevel = 0.99,
        double portfolioValue = 1.0)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(assetScenarios);

        VarConventions.ValidateConfidenceLevel(confidenceLevel);
        VarConventions.ValidatePortfolioValue(portfolioValue);

        var count = weights.Count;

        if (assetScenarios.Count != count)
        {
            throw new ArgumentException(
                "Число долей не соответствует числу рядов доходностей.", nameof(weights));
        }

        var scenarioCount = assetScenarios[0].Length;

        for (var i = 1; i < count; i++)
        {
            if (assetScenarios[i].Length != scenarioCount)
            {
                throw new ArgumentException(
                    "Ряды доходностей инструментов имеют различную длину.",
                    nameof(assetScenarios));
            }
        }

        // Доходность портфеля в каждом сценарии.
        var portfolioScenarios = new double[scenarioCount];

        for (var s = 0; s < scenarioCount; s++)
        {
            double value = 0.0;

            for (var i = 0; i < count; i++)
            {
                value += weights[i] * assetScenarios[i][s];
            }

            portfolioScenarios[s] = value;
        }

        var quantile = DescriptiveStatisticsCalculator.Quantile(
            portfolioScenarios, 1.0 - confidenceLevel);

        // Средние доходности инструментов по сценариям «хвоста».
        var tailSums = new double[count];
        var tailCount = 0;

        for (var s = 0; s < scenarioCount; s++)
        {
            if (portfolioScenarios[s] > quantile)
            {
                continue;
            }

            tailCount++;

            for (var i = 0; i < count; i++)
            {
                tailSums[i] += assetScenarios[i][s];
            }
        }

        if (tailCount == 0)
        {
            throw new InvalidOperationException(
                "В «хвосте» распределения не оказалось ни одного сценария: " +
                "увеличьте объём выборки либо снизьте уровень доверия.");
        }

        var contributions = new RiskContribution[count];
        double total = 0.0;

        for (var i = 0; i < count; i++)
        {
            // Вклад в ожидаемые потери берётся с обратным знаком:
            // потери выражаются положительной величиной.
            var component = -weights[i] * (tailSums[i] / tailCount) * portfolioValue;

            // Обособленные ожидаемые потери позиции: среднее по собственному
            // «хвосту» инструмента, а не по «хвосту» портфеля.
            var ownQuantile = DescriptiveStatisticsCalculator.Quantile(
                assetScenarios[i], 1.0 - confidenceLevel);

            double ownTailSum = 0.0;
            var ownTailCount = 0;

            for (var s = 0; s < scenarioCount; s++)
            {
                if (assetScenarios[i][s] <= ownQuantile)
                {
                    ownTailSum += assetScenarios[i][s];
                    ownTailCount++;
                }
            }

            var standalone = ownTailCount > 0
                ? -weights[i] * (ownTailSum / ownTailCount) * portfolioValue
                : 0.0;

            contributions[i] = new RiskContribution(
                Index: i,
                Weight: weights[i],
                MarginalVar: component / Math.Max(weights[i], 1e-12),
                ComponentVar: component,
                ContributionShare: 0.0,
                StandaloneVar: standalone,
                DiversificationBenefit: standalone - component);

            total += component;
        }

        if (Math.Abs(total) > 1e-12)
        {
            for (var i = 0; i < count; i++)
            {
                contributions[i] = contributions[i] with
                {
                    ContributionShare = contributions[i].ComponentVar / total
                };
            }
        }

        return contributions;
    }
}
