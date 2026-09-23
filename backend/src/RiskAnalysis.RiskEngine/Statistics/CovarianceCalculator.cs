using MathNet.Numerics.LinearAlgebra;

namespace RiskAnalysis.RiskEngine.Statistics;

/// <summary>
/// Расчёт ковариационной и корреляционной матриц ряда доходностей нескольких
/// инструментов.
///
/// Ковариационная матрица является основой количественной оценки риска
/// портфеля: дисперсия доходности портфеля с вектором долей w определяется
/// квадратичной формой
///
///     σ²(портфеля) = wᵀ · Σ · w,
///
/// где Σ — ковариационная матрица доходностей. Именно недиагональные элементы
/// матрицы количественно выражают эффект диверсификации: при корреляции
/// инструментов ниже единицы риск портфеля оказывается меньше средневзвешенного
/// риска составляющих.
///
/// Все ряды доходностей должны быть приведены к общему торговому календарю:
/// сопоставление доходностей разных торговых дней исказило бы оценки
/// ковариаций.
/// </summary>
public static class CovarianceCalculator
{
    /// <summary>
    /// Рассчитывает выборочную ковариационную матрицу (несмещённая оценка,
    /// делитель n − 1).
    /// </summary>
    /// <param name="returns">
    /// Матрица доходностей: первый индекс — инструмент, второй — наблюдение.
    /// Все ряды должны иметь одинаковую длину.
    /// </param>
    public static Matrix<double> Covariance(IReadOnlyList<double[]> returns)
    {
        ValidateInput(returns);

        var assetCount = returns.Count;
        var observationCount = returns[0].Length;

        var means = new double[assetCount];

        for (var i = 0; i < assetCount; i++)
        {
            double sum = 0.0;

            for (var t = 0; t < observationCount; t++)
            {
                sum += returns[i][t];
            }

            means[i] = sum / observationCount;
        }

        var covariance = Matrix<double>.Build.Dense(assetCount, assetCount);

        for (var i = 0; i < assetCount; i++)
        {
            for (var j = i; j < assetCount; j++)
            {
                double sum = 0.0;

                for (var t = 0; t < observationCount; t++)
                {
                    sum += (returns[i][t] - means[i]) * (returns[j][t] - means[j]);
                }

                var value = sum / (observationCount - 1);

                covariance[i, j] = value;
                covariance[j, i] = value;
            }
        }

        return covariance;
    }

    /// <summary>
    /// Рассчитывает корреляционную матрицу Пирсона.
    /// </summary>
    public static Matrix<double> Correlation(IReadOnlyList<double[]> returns)
    {
        var covariance = Covariance(returns);
        return CorrelationFromCovariance(covariance);
    }

    /// <summary>
    /// Преобразует ковариационную матрицу в корреляционную делением на
    /// произведение среднеквадратических отклонений.
    /// </summary>
    public static Matrix<double> CorrelationFromCovariance(Matrix<double> covariance)
    {
        ArgumentNullException.ThrowIfNull(covariance);

        var size = covariance.RowCount;
        var deviations = new double[size];

        for (var i = 0; i < size; i++)
        {
            deviations[i] = Math.Sqrt(covariance[i, i]);
        }

        var correlation = Matrix<double>.Build.Dense(size, size);

        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                correlation[i, j] = deviations[i] > 0 && deviations[j] > 0
                    ? covariance[i, j] / (deviations[i] * deviations[j])
                    : (i == j ? 1.0 : 0.0);
            }
        }

        return correlation;
    }

    /// <summary>Рассчитывает вектор средних доходностей по инструментам.</summary>
    public static double[] Means(IReadOnlyList<double[]> returns)
    {
        ValidateInput(returns);

        var means = new double[returns.Count];

        for (var i = 0; i < returns.Count; i++)
        {
            double sum = 0.0;

            for (var t = 0; t < returns[i].Length; t++)
            {
                sum += returns[i][t];
            }

            means[i] = sum / returns[i].Length;
        }

        return means;
    }

    /// <summary>
    /// Рассчитывает среднеквадратическое отклонение доходности портфеля
    /// по ковариационной матрице и вектору долей: σ = √(wᵀ · Σ · w).
    /// </summary>
    /// <param name="weights">Вектор долей инструментов в портфеле.</param>
    /// <param name="covariance">Ковариационная матрица доходностей.</param>
    public static double PortfolioStandardDeviation(
        IReadOnlyList<double> weights,
        Matrix<double> covariance)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(covariance);

        if (weights.Count != covariance.RowCount)
        {
            throw new ArgumentException(
                "Размерность вектора долей не соответствует размерности " +
                "ковариационной матрицы.",
                nameof(weights));
        }

        double variance = 0.0;

        for (var i = 0; i < weights.Count; i++)
        {
            for (var j = 0; j < weights.Count; j++)
            {
                variance += weights[i] * weights[j] * covariance[i, j];
            }
        }

        // Накопление погрешности округления может дать малое отрицательное
        // значение при практически нулевой дисперсии.
        return variance > 0.0 ? Math.Sqrt(variance) : 0.0;
    }

    private static void ValidateInput(IReadOnlyList<double[]> returns)
    {
        ArgumentNullException.ThrowIfNull(returns);

        if (returns.Count == 0)
        {
            throw new ArgumentException("Не задано ни одного ряда доходностей.", nameof(returns));
        }

        var length = returns[0].Length;

        if (length < 2)
        {
            throw new ArgumentException(
                "Каждый ряд доходностей должен содержать не менее двух наблюдений.",
                nameof(returns));
        }

        for (var i = 1; i < returns.Count; i++)
        {
            if (returns[i].Length != length)
            {
                throw new ArgumentException(
                    "Ряды доходностей имеют различную длину. Перед расчётом ковариационной " +
                    "матрицы ряды должны быть приведены к общему торговому календарю.",
                    nameof(returns));
            }
        }
    }
}
