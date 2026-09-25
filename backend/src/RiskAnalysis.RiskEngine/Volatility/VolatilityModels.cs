namespace RiskAnalysis.RiskEngine.Volatility;

/// <summary>
/// Результат оценки условной волатильности.
/// </summary>
/// <param name="Model">Наименование применённой модели.</param>
/// <param name="ConditionalVolatility">
/// Ряд оценок условного среднеквадратического отклонения. Длина ряда
/// совпадает с длиной ряда доходностей: значение с индексом t представляет
/// собой оценку волатильности, построенную по наблюдениям, предшествующим
/// моменту t.
/// </param>
/// <param name="Forecast">
/// Прогноз волатильности на следующий период. Именно эта величина
/// применяется при расчёте стоимостной меры риска.
/// </param>
/// <param name="UnconditionalVolatility">
/// Безусловная (выборочная) оценка волатильности, приводимая
/// для сопоставления.
/// </param>
/// <param name="MinimumVolatility">Наименьшее значение условной волатильности за период.</param>
/// <param name="MaximumVolatility">Наибольшее значение условной волатильности за период.</param>
/// <param name="Parameters">Оценённые параметры модели.</param>
/// <param name="LogLikelihood">
/// Значение логарифмической функции правдоподобия. Применяется
/// для сопоставления моделей между собой.
/// </param>
/// <param name="Description">Пояснение к результату.</param>
public sealed record VolatilityResult(
    string Model,
    IReadOnlyList<double> ConditionalVolatility,
    double Forecast,
    double UnconditionalVolatility,
    double MinimumVolatility,
    double MaximumVolatility,
    IReadOnlyDictionary<string, double> Parameters,
    double LogLikelihood,
    string Description);
