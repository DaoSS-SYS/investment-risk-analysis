using RiskAnalysis.Domain.Enums;

namespace RiskAnalysis.WebApi.Contracts;

/// <summary>Запрос на добавление инструмента в справочник системы.</summary>
/// <param name="Ticker">Биржевой код инструмента, например SBER или IMOEX.</param>
/// <param name="Board">
/// Режим торгов. Если не указан, используется основной режим торгов,
/// сообщаемый справочником биржи.
/// </param>
/// <param name="Sector">Отрасль эмитента. Заполняется пользователем.</param>
public sealed record AddInstrumentRequest(string Ticker, string? Board, string? Sector);

/// <summary>Инструмент справочника системы.</summary>
public sealed record InstrumentDto(
    int Id,
    string Ticker,
    string ShortName,
    string? FullName,
    SecurityType SecurityType,
    string Engine,
    string Market,
    string Board,
    string Currency,
    string? Isin,
    string? Sector,
    bool IsActive,
    DateOnly? HistoryFrom,
    DateOnly? HistoryTo,
    int QuoteCount);

/// <summary>Инструмент, найденный в справочнике биржи.</summary>
public sealed record SecuritySearchResultDto(
    string Ticker,
    string ShortName,
    string? FullName,
    SecurityType SecurityType,
    string Engine,
    string Market,
    string Board,
    string? Isin,
    bool AlreadyAdded);

/// <summary>Дневная котировка.</summary>
public sealed record QuoteDto(
    DateOnly TradeDate,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long? Volume,
    decimal? Turnover);
