namespace RiskAnalysis.RiskEngine.Performance;

/// <summary>
/// Результат оценки параметров модели CAPM и связанных с ней показателей.
/// </summary>
/// <param name="Count">Число наблюдений.</param>
/// <param name="Beta">Коэффициент бета — мера систематического риска.</param>
/// <param name="BetaStandardError">Стандартная ошибка оценки коэффициента бета.</param>
/// <param name="BetaTStatistic">Наблюдаемое значение t-статистики для коэффициента бета.</param>
/// <param name="AlphaPerPeriod">Альфа Йенсена за один период.</param>
/// <param name="AlphaAnnualized">Альфа Йенсена в годовом выражении.</param>
/// <param name="AlphaStandardError">Стандартная ошибка оценки альфы за период.</param>
/// <param name="AlphaTStatistic">Наблюдаемое значение t-статистики для альфы.</param>
/// <param name="AlphaIsSignificant">
/// Признак значимости альфы на уровне 0,05 по двустороннему критерию.
/// </param>
/// <param name="RSquared">Коэффициент детерминации.</param>
/// <param name="Correlation">Коэффициент корреляции с эталонным портфелем.</param>
/// <param name="SystematicRiskShare">Доля систематического риска в общей дисперсии.</param>
/// <param name="SpecificRiskShare">Доля специфического риска в общей дисперсии.</param>
/// <param name="SpecificVolatilityAnnualized">
/// Специфическая (несистематическая) волатильность в годовом выражении.
/// </param>
/// <param name="TreynorRatio">Коэффициент Трейнора.</param>
/// <param name="InformationRatio">Информационный коэффициент.</param>
/// <param name="TrackingErrorAnnualized">Ошибка следования в годовом выражении.</param>
/// <param name="Conclusion">Словесная характеристика полученных значений.</param>
public sealed record CapmResult(
    int Count,
    double Beta,
    double BetaStandardError,
    double BetaTStatistic,
    double AlphaPerPeriod,
    double AlphaAnnualized,
    double AlphaStandardError,
    double AlphaTStatistic,
    bool AlphaIsSignificant,
    double RSquared,
    double Correlation,
    double SystematicRiskShare,
    double SpecificRiskShare,
    double SpecificVolatilityAnnualized,
    double TreynorRatio,
    double InformationRatio,
    double TrackingErrorAnnualized,
    string Conclusion);

/// <summary>
/// Оценка параметров модели оценки капитальных активов (CAPM).
///
/// Модель разделяет риск вложения на две составляющие: систематический риск,
/// обусловленный движением рынка в целом, и специфический риск, присущий
/// конкретному инструменту. Первый не устраняется диверсификацией, второй
/// устраняется.
///
/// Параметры оцениваются методом наименьших квадратов по уравнению
///
///     r(i) − rf = α + β · (r(m) − rf) + ε,
///
/// где r(i) — доходность инструмента, r(m) — доходность эталонного портфеля
/// (индекса), rf — безрисковая ставка.
///
/// Коэффициент бета показывает чувствительность доходности инструмента к
/// доходности рынка. Значение больше единицы означает, что инструмент
/// изменяется сильнее рынка.
///
/// Альфа Йенсена представляет собой доходность, не объясняемую движением
/// рынка. Положительное значение означает доходность сверх обусловленной
/// принятым систематическим риском. Оценка альфы обладает значительной
/// стандартной ошибкой, поэтому одновременно рассчитывается t-статистика:
/// без проверки значимости вывод о наличии избыточной доходности
/// не обоснован.
///
/// Коэффициент детерминации показывает долю дисперсии доходности инструмента,
/// объясняемую движением рынка, и совпадает с долей систематического риска.
///
/// Коэффициент Трейнора соотносит доходность сверх безрисковой ставки
/// с систематическим риском:
///
///     Treynor = (R − Rf) / β.
///
/// В отличие от коэффициента Шарпа, относящего доходность к общему риску,
/// коэффициент Трейнора применим к инструменту, рассматриваемому как часть
/// хорошо диверсифицированного портфеля, где специфический риск уже устранён.
///
/// Информационный коэффициент характеризует результат активного управления:
/// отношение средней разности доходностей инструмента и эталонного портфеля
/// к среднеквадратическому отклонению этой разности (ошибке следования).
/// </summary>
public static class CapmCalculator
{
    /// <summary>
    /// Критическое значение двустороннего критерия Стьюдента на уровне
    /// значимости 0,05 при большом числе наблюдений. Для выборок свыше
    /// нескольких сотен наблюдений распределение Стьюдента практически
    /// совпадает со стандартным нормальным.
    /// </summary>
    private const double CriticalTValue = 1.96;

    /// <summary>
    /// Оценивает параметры модели CAPM.
    /// </summary>
    /// <param name="assetReturns">Логарифмические доходности инструмента.</param>
    /// <param name="marketReturns">
    /// Логарифмические доходности эталонного портфеля. Ряд должен быть
    /// приведён к тому же торговому календарю, что и ряд инструмента.
    /// </param>
    /// <param name="riskFreeRateAnnual">Безрисковая ставка в годовом выражении.</param>
    /// <param name="periodsPerYear">Число периодов в году.</param>
    public static CapmResult Calculate(
        IReadOnlyList<double> assetReturns,
        IReadOnlyList<double> marketReturns,
        double riskFreeRateAnnual,
        int periodsPerYear = 252)
    {
        ArgumentNullException.ThrowIfNull(assetReturns);
        ArgumentNullException.ThrowIfNull(marketReturns);

        if (assetReturns.Count != marketReturns.Count)
        {
            throw new ArgumentException(
                "Ряды доходностей инструмента и эталонного портфеля имеют различную длину. " +
                "Перед оценкой ряды должны быть приведены к общему торговому календарю.",
                nameof(marketReturns));
        }

        var n = assetReturns.Count;

        if (n < 30)
        {
            throw new ArgumentException(
                "Для оценки параметров модели требуется не менее тридцати наблюдений.",
                nameof(assetReturns));
        }

        var riskFree = PerformanceCalculator.PeriodRiskFreeRate(riskFreeRateAnnual, periodsPerYear);

        // Избыточные доходности относительно безрисковой ставки.
        var x = new double[n];
        var y = new double[n];

        for (var i = 0; i < n; i++)
        {
            x[i] = marketReturns[i] - riskFree;
            y[i] = assetReturns[i] - riskFree;
        }

        double meanX = 0.0, meanY = 0.0;

        for (var i = 0; i < n; i++)
        {
            meanX += x[i];
            meanY += y[i];
        }

        meanX /= n;
        meanY /= n;

        // Метод наименьших квадратов.
        double crossProduct = 0.0, sumSquaresX = 0.0, sumSquaresY = 0.0;

        for (var i = 0; i < n; i++)
        {
            var deviationX = x[i] - meanX;
            var deviationY = y[i] - meanY;

            crossProduct += deviationX * deviationY;
            sumSquaresX += deviationX * deviationX;
            sumSquaresY += deviationY * deviationY;
        }

        if (sumSquaresX <= 0.0)
        {
            throw new ArgumentException(
                "Доходность эталонного портфеля постоянна: оценка коэффициента бета невозможна.",
                nameof(marketReturns));
        }

        var beta = crossProduct / sumSquaresX;
        var alpha = meanY - beta * meanX;

        // Остатки регрессии и их дисперсия. Число степеней свободы
        // уменьшено на два по числу оцениваемых параметров.
        double residualSumOfSquares = 0.0;

        for (var i = 0; i < n; i++)
        {
            var residual = y[i] - alpha - beta * x[i];
            residualSumOfSquares += residual * residual;
        }

        var residualVariance = residualSumOfSquares / (n - 2);

        var betaStandardError = Math.Sqrt(residualVariance / sumSquaresX);
        var alphaStandardError = Math.Sqrt(
            residualVariance * (1.0 / n + meanX * meanX / sumSquaresX));

        var betaTStatistic = betaStandardError > 0 ? beta / betaStandardError : 0.0;
        var alphaTStatistic = alphaStandardError > 0 ? alpha / alphaStandardError : 0.0;

        var rSquared = sumSquaresY > 0 ? 1.0 - residualSumOfSquares / sumSquaresY : 0.0;
        var correlation = Math.Sign(beta) * Math.Sqrt(Math.Max(rSquared, 0.0));

        // Разложение дисперсии: общая дисперсия складывается из
        // систематической составляющей β²·σ²(рынка) и остаточной дисперсии.
        // Доля систематической составляющей совпадает с коэффициентом
        // детерминации, что служит проверкой согласованности расчёта.
        var totalVariance = sumSquaresY / (n - 1);
        var marketVariance = sumSquaresX / (n - 1);
        var systematicVariance = beta * beta * marketVariance;

        var systematicShare = totalVariance > 0 ? systematicVariance / totalVariance : 0.0;

        var annualizedExcessReturn = meanY * periodsPerYear;

        var treynor = Math.Abs(beta) > 1e-12 ? annualizedExcessReturn / beta : 0.0;

        // Информационный коэффициент: активная доходность относительно
        // эталонного портфеля, а не относительно безрисковой ставки.
        double activeSum = 0.0;
        var active = new double[n];

        for (var i = 0; i < n; i++)
        {
            active[i] = assetReturns[i] - marketReturns[i];
            activeSum += active[i];
        }

        var activeMean = activeSum / n;

        double activeSumOfSquares = 0.0;

        for (var i = 0; i < n; i++)
        {
            var deviation = active[i] - activeMean;
            activeSumOfSquares += deviation * deviation;
        }

        var trackingError = Math.Sqrt(activeSumOfSquares / (n - 1)) * Math.Sqrt(periodsPerYear);

        var informationRatio = trackingError > 0
            ? activeMean * periodsPerYear / trackingError
            : 0.0;

        var alphaIsSignificant = Math.Abs(alphaTStatistic) > CriticalTValue;

        return new CapmResult(
            Count: n,
            Beta: beta,
            BetaStandardError: betaStandardError,
            BetaTStatistic: betaTStatistic,
            AlphaPerPeriod: alpha,
            AlphaAnnualized: alpha * periodsPerYear,
            AlphaStandardError: alphaStandardError,
            AlphaTStatistic: alphaTStatistic,
            AlphaIsSignificant: alphaIsSignificant,
            RSquared: rSquared,
            Correlation: correlation,
            SystematicRiskShare: systematicShare,
            SpecificRiskShare: 1.0 - systematicShare,
            SpecificVolatilityAnnualized: Math.Sqrt(residualVariance) * Math.Sqrt(periodsPerYear),
            TreynorRatio: treynor,
            InformationRatio: informationRatio,
            TrackingErrorAnnualized: trackingError,
            Conclusion: BuildConclusion(beta, alpha * periodsPerYear, alphaIsSignificant, systematicShare));
    }

    private static string BuildConclusion(
        double beta, double annualizedAlpha, bool alphaIsSignificant, double systematicShare)
    {
        var betaAssessment = beta switch
        {
            < 0.0 => "инструмент движется противоположно рынку",
            < 0.8 => "инструмент менее подвижен, чем рынок",
            <= 1.2 => "подвижность инструмента близка к рыночной",
            _ => "инструмент более подвижен, чем рынок"
        };

        var alphaAssessment = alphaIsSignificant
            ? $"Альфа Йенсена составляет {annualizedAlpha:P2} годовых и статистически " +
              "значима на уровне 0,05."
            : $"Альфа Йенсена составляет {annualizedAlpha:P2} годовых, однако статистически " +
              "не значима: вывод о наличии доходности сверх обусловленной рыночным " +
              "риском не обоснован.";

        return $"Коэффициент бета {beta:F3} — {betaAssessment}. Движением рынка объясняется " +
               $"{systematicShare:P1} дисперсии доходности инструмента, остальное приходится " +
               $"на специфический риск, устранимый диверсификацией. {alphaAssessment}";
    }
}
