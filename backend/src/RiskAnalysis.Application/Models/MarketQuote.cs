namespace RiskAnalysis.Application.Models;

/// <summary>
/// Дневная котировка, полученная от внешнего источника рыночных данных.
/// Представляет собой результат разбора ответа источника до его сохранения
/// в базе данных.
/// </summary>
/// <param name="TradeDate">Дата торгов.</param>
/// <param name="Open">Цена открытия.</param>
/// <param name="High">Максимальная цена за день.</param>
/// <param name="Low">Минимальная цена за день.</param>
/// <param name="Close">Цена закрытия.</param>
/// <param name="Volume">Объём торгов в штуках.</param>
/// <param name="Turnover">Оборот торгов в валюте инструмента.</param>
public sealed record MarketQuote(
    DateOnly TradeDate,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long? Volume,
    decimal? Turnover);
