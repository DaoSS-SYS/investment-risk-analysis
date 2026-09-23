using RiskAnalysis.Application.Models;
using RiskAnalysis.RiskEngine.Returns;

namespace RiskAnalysis.Application.Abstractions;

/// <summary>
/// Статистический анализ доходностей финансовых инструментов.
///
/// Связывает подсистему хранения данных с расчётным ядром: получает
/// скорректированный ценовой ряд, передаёт его в расчётное ядро и возвращает
/// результат. Математические операции выполняются исключительно в ядре.
/// </summary>
public interface IReturnAnalysisService
{
    /// <summary>
    /// Выполняет статистический анализ ряда доходностей инструмента.
    /// </summary>
    /// <param name="instrumentId">Идентификатор инструмента.</param>
    /// <param name="from">Начало периода.</param>
    /// <param name="to">Конец периода.</param>
    /// <param name="returnType">Способ расчёта доходности.</param>
    /// <param name="frequency">Периодичность доходностей.</param>
    /// <param name="includeReturns">Включать ли в результат сам ряд доходностей.</param>
    Task<ReturnAnalysisResult> AnalyzeAsync(
        int instrumentId,
        DateOnly from,
        DateOnly to,
        ReturnType returnType = ReturnType.Logarithmic,
        ReturnFrequency frequency = ReturnFrequency.Daily,
        bool includeReturns = false,
        CancellationToken cancellationToken = default);
}
