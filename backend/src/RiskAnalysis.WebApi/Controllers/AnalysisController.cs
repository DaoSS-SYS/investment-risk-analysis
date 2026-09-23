using Microsoft.AspNetCore.Mvc;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.RiskEngine.Returns;

namespace RiskAnalysis.WebApi.Controllers;

/// <summary>
/// Статистический анализ доходностей финансовых инструментов.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AnalysisController : ControllerBase
{
    private readonly IReturnAnalysisService _analysis;

    public AnalysisController(IReturnAnalysisService analysis) => _analysis = analysis;

    /// <summary>
    /// Рассчитывает ряд доходностей инструмента и его описательные статистики,
    /// выполняет проверку гипотезы о нормальности распределения по критерию
    /// Жарка — Бера и строит гистограмму распределения с наложением
    /// теоретических частот нормального распределения.
    /// </summary>
    /// <param name="id">Идентификатор инструмента.</param>
    /// <param name="from">Начало периода. По умолчанию — десять лет назад.</param>
    /// <param name="to">Конец периода. По умолчанию — текущая дата.</param>
    /// <param name="returnType">Способ расчёта доходности. По умолчанию — логарифмическая.</param>
    /// <param name="frequency">Периодичность. По умолчанию — дневная.</param>
    /// <param name="includeReturns">Включить в ответ ряд доходностей.</param>
    [HttpGet("instruments/{id:int}/returns")]
    public async Task<ActionResult<ReturnAnalysisResult>> AnalyzeInstrument(
        int id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] ReturnType returnType = ReturnType.Logarithmic,
        [FromQuery] ReturnFrequency frequency = ReturnFrequency.Daily,
        [FromQuery] bool includeReturns = false,
        CancellationToken cancellationToken = default)
    {
        var dateTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dateFrom = from ?? dateTo.AddYears(-10);

        try
        {
            var result = await _analysis.AnalyzeAsync(
                id, dateFrom, dateTo, returnType, frequency, includeReturns, cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
