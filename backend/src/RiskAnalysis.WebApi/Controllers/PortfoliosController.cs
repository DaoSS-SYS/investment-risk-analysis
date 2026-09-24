using Microsoft.AspNetCore.Mvc;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;

namespace RiskAnalysis.WebApi.Controllers;

/// <summary>
/// Инвестиционные портфели, их позиции и оценка риска.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PortfoliosController : ControllerBase
{
    private readonly IPortfolioService _portfolios;
    private readonly IPortfolioRiskService _risk;
    private readonly ICalculationService _calculations;

    public PortfoliosController(
        IPortfolioService portfolios,
        IPortfolioRiskService risk,
        ICalculationService calculations)
    {
        _portfolios = portfolios;
        _risk = risk;
        _calculations = calculations;
    }

    /// <summary>Возвращает перечень портфелей с текущей оценкой.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PortfolioView>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await _portfolios.GetAllAsync(cancellationToken));
    }

    /// <summary>Возвращает портфель с текущей оценкой позиций.</summary>
    /// <param name="id">Идентификатор портфеля.</param>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PortfolioView>> Get(int id, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolios.GetAsync(id, cancellationToken);

        return portfolio is null ? NotFound() : Ok(portfolio);
    }

    /// <summary>Создаёт портфель.</summary>
    /// <param name="request">Реквизиты портфеля.</param>
    [HttpPost]
    public async Task<ActionResult<PortfolioView>> Create(
        [FromBody] PortfolioRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var portfolio = await _portfolios.CreateAsync(request, cancellationToken);

            return CreatedAtAction(nameof(Get), new { id = portfolio.Id }, portfolio);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Изменяет реквизиты портфеля.</summary>
    /// <param name="id">Идентификатор портфеля.</param>
    /// <param name="request">Новые реквизиты.</param>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<PortfolioView>> Update(
        int id, [FromBody] PortfolioRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var portfolio = await _portfolios.UpdateAsync(id, request, cancellationToken);

            return portfolio is null ? NotFound() : Ok(portfolio);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Удаляет портфель вместе с позициями и историей расчётов.</summary>
    /// <param name="id">Идентификатор портфеля.</param>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        return await _portfolios.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }

    /// <summary>Добавляет позицию в портфель.</summary>
    /// <param name="id">Идентификатор портфеля.</param>
    /// <param name="request">Реквизиты позиции.</param>
    [HttpPost("{id:int}/positions")]
    public async Task<ActionResult<PortfolioView>> AddPosition(
        int id, [FromBody] PositionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var portfolio = await _portfolios.AddPositionAsync(id, request, cancellationToken);

            return portfolio is null ? NotFound() : Ok(portfolio);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Удаляет позицию портфеля.</summary>
    /// <param name="id">Идентификатор портфеля.</param>
    /// <param name="positionId">Идентификатор позиции.</param>
    [HttpDelete("{id:int}/positions/{positionId:int}")]
    public async Task<ActionResult<PortfolioView>> RemovePosition(
        int id, int positionId, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolios.RemovePositionAsync(id, positionId, cancellationToken);

        return portfolio is null ? NotFound() : Ok(portfolio);
    }

    /// <summary>
    /// Рассчитывает риск портфеля непосредственно, без постановки в очередь.
    /// Метод предназначен для портфелей небольшого объёма и умеренного числа
    /// сценариев; для длительных расчётов следует применять постановку
    /// в очередь.
    /// </summary>
    /// <param name="id">Идентификатор портфеля.</param>
    /// <param name="confidence">Уровень доверия.</param>
    /// <param name="horizon">Горизонт оценки в торговых днях.</param>
    /// <param name="from">Начало периода выборки.</param>
    /// <param name="to">Конец периода выборки.</param>
    /// <param name="scenarios">Число сценариев метода Монте-Карло.</param>
    [HttpGet("{id:int}/risk")]
    public async Task<ActionResult<PortfolioRiskReport>> CalculateRisk(
        int id,
        [FromQuery] double confidence = 0.99,
        [FromQuery] int horizon = 1,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int scenarios = 100_000,
        CancellationToken cancellationToken = default)
    {
        var parameters = new RiskCalculationParameters(confidence, horizon, from, to, scenarios);

        try
        {
            return Ok(await _risk.CalculateAsync(id, parameters, cancellationToken));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Ставит расчёт риска портфеля в очередь на выполнение. Возвращает код
    /// 202 и идентификатор расчёта; результат запрашивается отдельно
    /// по адресу, указанному в заголовке Location.
    /// </summary>
    /// <param name="id">Идентификатор портфеля.</param>
    /// <param name="parameters">Параметры расчёта.</param>
    [HttpPost("{id:int}/calculations")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<CalculationView>> EnqueueCalculation(
        int id,
        [FromBody] RiskCalculationParameters? parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            var calculation = await _calculations.EnqueueAsync(
                id, parameters ?? new RiskCalculationParameters(), cancellationToken);

            return Accepted($"/api/calculations/{calculation.Id}", calculation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Возвращает историю расчётов по портфелю.</summary>
    /// <param name="id">Идентификатор портфеля.</param>
    /// <param name="limit">Максимальное число записей.</param>
    [HttpGet("{id:int}/calculations")]
    public async Task<ActionResult<IReadOnlyList<CalculationView>>> GetCalculations(
        int id, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        return Ok(await _calculations.GetHistoryAsync(id, limit, cancellationToken));
    }
}

/// <summary>
/// Состояние и результаты асинхронных расчётов.
/// </summary>
[ApiController]
[Route("api/calculations")]
[Produces("application/json")]
public class CalculationsController : ControllerBase
{
    private readonly ICalculationService _calculations;
    private readonly ICalculationQueue _queue;

    public CalculationsController(ICalculationService calculations, ICalculationQueue queue)
    {
        _calculations = calculations;
        _queue = queue;
    }

    /// <summary>
    /// Возвращает состояние расчёта, а после успешного завершения — результат.
    /// </summary>
    /// <param name="id">Идентификатор расчёта.</param>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CalculationView>> Get(Guid id, CancellationToken cancellationToken)
    {
        var calculation = await _calculations.GetAsync(id, cancellationToken);

        return calculation is null ? NotFound() : Ok(calculation);
    }

    /// <summary>Возвращает текущую длину очереди расчётов.</summary>
    [HttpGet("queue")]
    public ActionResult<object> GetQueueState()
    {
        return Ok(new { QueueLength = _queue.Count });
    }
}
