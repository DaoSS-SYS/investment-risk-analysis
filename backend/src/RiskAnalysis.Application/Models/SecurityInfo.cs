using RiskAnalysis.Domain.Enums;

namespace RiskAnalysis.Application.Models;

/// <summary>
/// Сведения о финансовом инструменте из справочника биржи.
/// Используются при добавлении инструмента в справочник системы.
/// </summary>
/// <param name="Ticker">Биржевой код инструмента.</param>
/// <param name="ShortName">Краткое наименование.</param>
/// <param name="FullName">Полное наименование.</param>
/// <param name="SecurityType">Тип инструмента.</param>
/// <param name="Engine">Торговая система биржи.</param>
/// <param name="Market">Рынок в составе торговой системы.</param>
/// <param name="Board">Режим торгов.</param>
/// <param name="Isin">Международный идентификационный код ценной бумаги.</param>
/// <param name="Currency">Валюта номинала.</param>
/// <param name="LotSize">Размер лота.</param>
public sealed record SecurityInfo(
    string Ticker,
    string ShortName,
    string? FullName,
    SecurityType SecurityType,
    string Engine,
    string Market,
    string Board,
    string? Isin,
    string? Currency,
    int? LotSize);
