using RiskAnalysis.Domain.Enums;
using RiskAnalysis.RiskEngine.Var;

namespace RiskAnalysis.Application.Models;

/// <summary>Позиция портфеля с текущей оценкой.</summary>
/// <param name="Id">Идентификатор позиции.</param>
/// <param name="InstrumentId">Идентификатор инструмента.</param>
/// <param name="Ticker">Биржевой код инструмента.</param>
/// <param name="ShortName">Краткое наименование инструмента.</param>
/// <param name="Quantity">Количество единиц инструмента.</param>
/// <param name="PurchasePrice">Цена приобретения.</param>
/// <param name="PurchaseDate">Дата приобретения.</param>
/// <param name="LastPrice">Последняя известная цена.</param>
/// <param name="LastPriceDate">Дата последней известной цены.</param>
/// <param name="PurchaseValue">Стоимость позиции по цене приобретения.</param>
/// <param name="CurrentValue">Текущая стоимость позиции.</param>
/// <param name="ProfitLoss">Финансовый результат по позиции.</param>
/// <param name="ProfitLossPercent">Финансовый результат в долях от вложенных средств.</param>
/// <param name="Weight">Доля позиции в текущей стоимости портфеля.</param>
public sealed record PositionView(
    int Id,
    int InstrumentId,
    string Ticker,
    string ShortName,
    decimal Quantity,
    decimal PurchasePrice,
    DateOnly PurchaseDate,
    decimal? LastPrice,
    DateOnly? LastPriceDate,
    decimal PurchaseValue,
    decimal CurrentValue,
    decimal ProfitLoss,
    double ProfitLossPercent,
    double Weight);

/// <summary>Портфель с текущей оценкой позиций.</summary>
/// <param name="Id">Идентификатор портфеля.</param>
/// <param name="Name">Наименование.</param>
/// <param name="Description">Описание.</param>
/// <param name="BaseCurrency">Валюта оценки.</param>
/// <param name="BenchmarkInstrumentId">Идентификатор эталонного портфеля.</param>
/// <param name="BenchmarkTicker">Биржевой код эталонного портфеля.</param>
/// <param name="PurchaseValue">Стоимость портфеля по ценам приобретения.</param>
/// <param name="CurrentValue">Текущая стоимость портфеля.</param>
/// <param name="ProfitLoss">Финансовый результат.</param>
/// <param name="ProfitLossPercent">Финансовый результат в долях от вложенных средств.</param>
/// <param name="Positions">Позиции портфеля.</param>
/// <param name="CreatedAt">Дата создания.</param>
/// <param name="UpdatedAt">Дата последнего изменения.</param>
public sealed record PortfolioView(
    int Id,
    string Name,
    string? Description,
    string BaseCurrency,
    int? BenchmarkInstrumentId,
    string? BenchmarkTicker,
    decimal PurchaseValue,
    decimal CurrentValue,
    decimal ProfitLoss,
    double ProfitLossPercent,
    IReadOnlyList<PositionView> Positions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Запрос на создание или изменение портфеля.</summary>
/// <param name="Name">Наименование.</param>
/// <param name="Description">Описание.</param>
/// <param name="BaseCurrency">Валюта оценки.</param>
/// <param name="BenchmarkInstrumentId">Идентификатор эталонного портфеля.</param>
public sealed record PortfolioRequest(
    string Name,
    string? Description,
    string BaseCurrency = "RUB",
    int? BenchmarkInstrumentId = null);

/// <summary>Запрос на добавление или изменение позиции.</summary>
/// <param name="InstrumentId">Идентификатор инструмента.</param>
/// <param name="Quantity">Количество единиц.</param>
/// <param name="PurchasePrice">Цена приобретения.</param>
/// <param name="PurchaseDate">Дата приобретения.</param>
/// <param name="Note">Комментарий.</param>
public sealed record PositionRequest(
    int InstrumentId,
    decimal Quantity,
    decimal PurchasePrice,
    DateOnly PurchaseDate,
    string? Note = null);

/// <summary>
/// Параметры расчёта риска портфеля. Сохраняются вместе с результатом,
/// что обеспечивает воспроизводимость расчёта.
/// </summary>
/// <param name="ConfidenceLevel">Уровень доверия.</param>
/// <param name="HorizonDays">Горизонт оценки в торговых днях.</param>
/// <param name="From">Начало периода выборки.</param>
/// <param name="To">Конец периода выборки.</param>
/// <param name="ScenarioCount">Число сценариев метода Монте-Карло.</param>
/// <param name="PortfolioValueOverride">
/// Стоимость портфеля, задаваемая вместо расчётной. Применяется при анализе
/// гипотетической структуры вложений.
/// </param>
public sealed record RiskCalculationParameters(
    double ConfidenceLevel = 0.99,
    int HorizonDays = 1,
    DateOnly? From = null,
    DateOnly? To = null,
    int ScenarioCount = 100_000,
    double? PortfolioValueOverride = null);

/// <summary>Вклад позиции в риск портфеля с указанием инструмента.</summary>
/// <param name="InstrumentId">Идентификатор инструмента.</param>
/// <param name="Ticker">Биржевой код инструмента.</param>
/// <param name="Weight">Доля позиции в портфеле.</param>
/// <param name="MarginalVar">Предельная мера риска.</param>
/// <param name="ComponentVar">Компонентная мера риска.</param>
/// <param name="ContributionShare">Доля в общем риске портфеля.</param>
/// <param name="StandaloneVar">Обособленная мера риска позиции.</param>
/// <param name="DiversificationBenefit">Выигрыш от диверсификации.</param>
public sealed record PositionRiskContribution(
    int InstrumentId,
    string Ticker,
    double Weight,
    double MarginalVar,
    double ComponentVar,
    double ContributionShare,
    double StandaloneVar,
    double DiversificationBenefit);

/// <summary>Результат оценки риска портфеля.</summary>
/// <param name="PortfolioId">Идентификатор портфеля.</param>
/// <param name="PortfolioName">Наименование портфеля.</param>
/// <param name="From">Начало периода выборки.</param>
/// <param name="To">Конец периода выборки.</param>
/// <param name="ObservationCount">Число наблюдений общего торгового календаря.</param>
/// <param name="PortfolioValue">Стоимость портфеля, принятая при расчёте.</param>
/// <param name="ConfidenceLevel">Уровень доверия.</param>
/// <param name="HorizonDays">Горизонт оценки.</param>
/// <param name="PortfolioVolatilityAnnualized">Волатильность портфеля в годовом выражении.</param>
/// <param name="WeightedAverageVolatility">Средневзвешенная волатильность составляющих.</param>
/// <param name="DiversificationEffect">Эффект диверсификации.</param>
/// <param name="Estimates">Оценки риска различными методами.</param>
/// <param name="Contributions">Разложение риска по позициям.</param>
/// <param name="SumOfStandaloneVar">Сумма обособленных мер риска позиций.</param>
/// <param name="Conclusion">Вывод по результатам расчёта.</param>
/// <param name="DurationMs">Длительность расчёта в миллисекундах.</param>
public sealed record PortfolioRiskReport(
    int PortfolioId,
    string PortfolioName,
    DateOnly From,
    DateOnly To,
    int ObservationCount,
    double PortfolioValue,
    double ConfidenceLevel,
    int HorizonDays,
    double PortfolioVolatilityAnnualized,
    double WeightedAverageVolatility,
    double DiversificationEffect,
    IReadOnlyList<VarResult> Estimates,
    IReadOnlyList<PositionRiskContribution> Contributions,
    double SumOfStandaloneVar,
    string Conclusion,
    int DurationMs);

/// <summary>Состояние асинхронного расчёта.</summary>
/// <param name="Id">Идентификатор расчёта.</param>
/// <param name="PortfolioId">Идентификатор портфеля.</param>
/// <param name="CalculationType">Вид расчёта.</param>
/// <param name="Status">Состояние.</param>
/// <param name="Parameters">Параметры запуска.</param>
/// <param name="CreatedAt">Время постановки в очередь.</param>
/// <param name="StartedAt">Время начала выполнения.</param>
/// <param name="FinishedAt">Время завершения.</param>
/// <param name="DurationMs">Длительность выполнения.</param>
/// <param name="ErrorMessage">Текст ошибки при неуспешном завершении.</param>
/// <param name="Result">Результат расчёта. Заполняется после успешного завершения.</param>
public sealed record CalculationView(
    Guid Id,
    int PortfolioId,
    CalculationType CalculationType,
    CalculationStatus Status,
    RiskCalculationParameters? Parameters,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int? DurationMs,
    string? ErrorMessage,
    PortfolioRiskReport? Result);
