using RiskAnalysis.Domain.Enums;

namespace RiskAnalysis.Application.Models;

/// <summary>
/// Точка ценового ряда: исходная цена закрытия и цена, скорректированная
/// на корпоративные действия. Скорректированные цены используются во всех
/// расчётах доходности и риска, исходные сохраняются неизменными.
/// </summary>
/// <param name="Date">Дата торгов.</param>
/// <param name="Close">Цена закрытия в том виде, в каком её опубликовала биржа.</param>
/// <param name="AdjustedClose">Цена закрытия после корректировки.</param>
public sealed record PricePoint(DateOnly Date, decimal Close, decimal AdjustedClose);

/// <summary>
/// Ценовой ряд инструмента, подготовленный для расчётов.
/// </summary>
/// <param name="InstrumentId">Идентификатор инструмента.</param>
/// <param name="Ticker">Биржевой код инструмента.</param>
/// <param name="Points">Точки ряда, упорядоченные по возрастанию даты.</param>
/// <param name="AppliedAdjustments">Число применённых корректировок.</param>
public sealed record PriceSeries(
    int InstrumentId,
    string Ticker,
    IReadOnlyList<PricePoint> Points,
    int AppliedAdjustments);

/// <summary>
/// Аномалия ценового ряда, выявленная алгоритмом предобработки данных.
/// </summary>
/// <param name="Date">Дата, на которую обнаружено аномальное изменение цены.</param>
/// <param name="PreviousClose">Цена закрытия предыдущего торгового дня.</param>
/// <param name="Close">Цена закрытия дня аномалии.</param>
/// <param name="ObservedRatio">Фактическое отношение цены к предыдущей цене.</param>
/// <param name="ObservedLogReturn">Логарифмическая доходность дня до корректировки.</param>
/// <param name="Classification">Результат классификации.</param>
/// <param name="Ratio">Коэффициент корпоративного действия либо фактическое отношение.</param>
/// <param name="AdjustmentFactor">Множитель для котировок, предшествующих дате события.</param>
/// <param name="Explanation">Обоснование классификации.</param>
public sealed record DetectedAnomaly(
    DateOnly Date,
    decimal PreviousClose,
    decimal Close,
    decimal ObservedRatio,
    decimal ObservedLogReturn,
    CorporateActionType Classification,
    decimal Ratio,
    decimal AdjustmentFactor,
    string Explanation);

/// <summary>
/// Итог выявления аномалий по инструменту.
/// </summary>
/// <param name="InstrumentId">Идентификатор инструмента.</param>
/// <param name="Ticker">Биржевой код инструмента.</param>
/// <param name="QuotesExamined">Число проверенных котировок.</param>
/// <param name="AnomaliesFound">Число обнаруженных аномалий.</param>
/// <param name="CorporateActionsDetected">Из них признано корпоративными действиями.</param>
/// <param name="MarketEventsDetected">Из них признано рыночными событиями.</param>
/// <param name="NewRecords">Число записей, добавленных в базу данных.</param>
/// <param name="Anomalies">Перечень обнаруженных аномалий.</param>
public sealed record AnomalyDetectionResult(
    int InstrumentId,
    string Ticker,
    int QuotesExamined,
    int AnomaliesFound,
    int CorporateActionsDetected,
    int MarketEventsDetected,
    int NewRecords,
    IReadOnlyList<DetectedAnomaly> Anomalies);
