namespace RiskAnalysis.Domain.Entities;

/// <summary>
/// Дневная котировка инструмента (свеча). Первичные данные для всех расчётов риска.
/// Сочетание инструмента и даты торгов уникально.
/// </summary>
public class Quote
{
    public long Id { get; set; }

    public int InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    /// <summary>Дата торгов.</summary>
    public DateOnly TradeDate { get; set; }

    /// <summary>Цена открытия.</summary>
    public decimal Open { get; set; }

    /// <summary>Максимальная цена за день.</summary>
    public decimal High { get; set; }

    /// <summary>Минимальная цена за день.</summary>
    public decimal Low { get; set; }

    /// <summary>Цена закрытия. Является базовой величиной при расчёте доходностей.</summary>
    public decimal Close { get; set; }

    /// <summary>Объём торгов в штуках.</summary>
    public long? Volume { get; set; }

    /// <summary>Оборот торгов в валюте инструмента.</summary>
    public decimal? Turnover { get; set; }
}
