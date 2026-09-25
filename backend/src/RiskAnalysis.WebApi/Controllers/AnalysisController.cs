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
    private readonly IVarAnalysisService _var;
    private readonly IPerformanceAnalysisService _performance;
    private readonly IBacktestService _backtest;

    public AnalysisController(
        IReturnAnalysisService analysis,
        IVarAnalysisService varAnalysis,
        IPerformanceAnalysisService performance,
        IBacktestService backtest)
    {
        _analysis = analysis;
        _var = varAnalysis;
        _performance = performance;
        _backtest = backtest;
    }

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

    /// <summary>
    /// Рассчитывает стоимостную меру риска VaR и ожидаемые потери CVaR
    /// по инструменту всеми реализованными методами: параметрическим,
    /// историческим и тремя разновидностями метода Монте-Карло.
    /// Результаты сопоставляются между собой.
    /// </summary>
    /// <param name="id">Идентификатор инструмента.</param>
    /// <param name="from">Начало периода выборки. По умолчанию — десять лет назад.</param>
    /// <param name="to">Конец периода выборки. По умолчанию — текущая дата.</param>
    /// <param name="confidence">Уровень доверия. По умолчанию 0,99.</param>
    /// <param name="horizon">Горизонт оценки в торговых днях. По умолчанию один день.</param>
    /// <param name="value">Стоимость позиции. По умолчанию один миллион рублей.</param>
    /// <param name="scenarios">Число сценариев метода Монте-Карло.</param>
    [HttpGet("instruments/{id:int}/var")]
    public async Task<ActionResult<VarComparisonResult>> CalculateVar(
        int id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] double confidence = 0.99,
        [FromQuery] int horizon = 1,
        [FromQuery] double value = 1_000_000.0,
        [FromQuery] int scenarios = 100_000,
        CancellationToken cancellationToken = default)
    {
        var dateTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dateFrom = from ?? dateTo.AddYears(-10);

        try
        {
            var result = await _var.CompareMethodsAsync(
                id, dateFrom, dateTo, confidence, horizon, value, scenarios, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Рассчитывает коэффициенты эффективности вложения (Шарпа, Сортино,
    /// Трейнора, информационный, Кальмара), просадки и параметры модели CAPM
    /// относительно эталонного портфеля. Безрисковой ставкой служит ключевая
    /// ставка Банка России.
    /// </summary>
    /// <param name="id">Идентификатор инструмента.</param>
    /// <param name="benchmarkId">
    /// Идентификатор эталонного портфеля. Без него параметры модели CAPM
    /// не рассчитываются.
    /// </param>
    /// <param name="from">Начало периода. По умолчанию — десять лет назад.</param>
    /// <param name="to">Конец периода. По умолчанию — текущая дата.</param>
    /// <param name="includeDrawdownSeries">Включить кривую просадки.</param>
    [HttpGet("instruments/{id:int}/performance")]
    public async Task<ActionResult<PerformanceAnalysisResult>> AnalyzePerformance(
        int id,
        [FromQuery] int? benchmarkId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] bool includeDrawdownSeries = false,
        CancellationToken cancellationToken = default)
    {
        var dateTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dateFrom = from ?? dateTo.AddYears(-10);

        try
        {
            var result = await _performance.AnalyzeAsync(
                id, benchmarkId, dateFrom, dateTo, includeDrawdownSeries, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Выполняет корреляционный анализ группы инструментов и количественно
    /// оценивает эффект диверсификации: сопоставляет волатильность
    /// равновзвешенного портфеля со средневзвешенной волатильностью
    /// составляющих.
    /// </summary>
    /// <param name="ids">Идентификаторы инструментов через запятую.</param>
    /// <param name="from">Начало периода.</param>
    /// <param name="to">Конец периода.</param>
    [HttpGet("correlation")]
    public async Task<ActionResult<CorrelationAnalysisResult>> AnalyzeCorrelation(
        [FromQuery] string ids,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var parsed = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var value) ? value : (int?)null)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .Distinct()
            .ToList();

        if (parsed.Count < 2)
        {
            return BadRequest(new
            {
                error = "Укажите не менее двух идентификаторов инструментов, например ids=1,2,3."
            });
        }

        var dateTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dateFrom = from ?? dateTo.AddYears(-10);

        try
        {
            var result = await _performance.AnalyzeCorrelationAsync(
                parsed, dateFrom, dateTo, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Выполняет бэктестирование моделей оценки стоимостной меры риска:
    /// проверяет, согласуются ли оценки с фактически наблюдавшимися потерями.
    ///
    /// Для каждого дня периода проверки оценка риска рассчитывается только
    /// по предшествующим наблюдениям, после чего сопоставляется с фактической
    /// доходностью. Применяются критерий Купца, критерий Кристоферсена
    /// и подход Базельского комитета.
    /// </summary>
    /// <param name="id">Идентификатор инструмента.</param>
    /// <param name="from">Начало периода выборки. По умолчанию — десять лет назад.</param>
    /// <param name="to">Конец периода выборки. По умолчанию — текущая дата.</param>
    /// <param name="confidence">Уровень доверия. По умолчанию 0,99.</param>
    /// <param name="window">Глубина скользящего окна оценивания. По умолчанию 250 дней.</param>
    /// <param name="scenarios">Число сценариев метода Монте-Карло.</param>
    [HttpGet("instruments/{id:int}/backtest")]
    public async Task<ActionResult<BacktestReport>> Backtest(
        int id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] double confidence = 0.99,
        [FromQuery] int window = 250,
        [FromQuery] int scenarios = 10_000,
        CancellationToken cancellationToken = default)
    {
        var dateTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dateFrom = from ?? dateTo.AddYears(-10);

        try
        {
            var result = await _backtest.RunAsync(
                id, dateFrom, dateTo, confidence, window, scenarios, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
