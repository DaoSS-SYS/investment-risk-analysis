using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.Domain.Enums;
using RiskAnalysis.Infrastructure.Persistence;

namespace RiskAnalysis.WebApi.Controllers;

/// <summary>
/// Предобработка данных: выявление аномалий ценовых рядов, их классификация
/// на корпоративные действия и рыночные события, подтверждение классификации
/// пользователем.
/// </summary>
[ApiController]
[Route("api/corporate-actions")]
[Produces("application/json")]
public class CorporateActionsController : ControllerBase
{
    private readonly RiskAnalysisDbContext _db;
    private readonly ICorporateActionService _service;

    public CorporateActionsController(RiskAnalysisDbContext db, ICorporateActionService service)
    {
        _db = db;
        _service = service;
    }

    /// <summary>
    /// Проверяет ценовой ряд инструмента и сохраняет обнаруженные аномалии.
    /// </summary>
    /// <param name="instrumentId">Идентификатор инструмента.</param>
    [HttpPost("detect/{instrumentId:int}")]
    public async Task<ActionResult<AnomalyDetectionResult>> Detect(
        int instrumentId,
        CancellationToken cancellationToken)
    {
        var result = await _service.DetectAsync(instrumentId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Проверяет ценовые ряды всех инструментов справочника.</summary>
    [HttpPost("detect-all")]
    public async Task<ActionResult<IReadOnlyList<AnomalyDetectionResult>>> DetectAll(
        CancellationToken cancellationToken)
    {
        var results = await _service.DetectAllAsync(cancellationToken);
        return Ok(results);
    }

    /// <summary>Возвращает журнал выявленных аномалий ценовых рядов.</summary>
    /// <param name="instrumentId">Ограничение выборки одним инструментом.</param>
    [HttpGet]
    public async Task<ActionResult<object>> GetAll(
        [FromQuery] int? instrumentId,
        CancellationToken cancellationToken)
    {
        var query = _db.CorporateActions.AsNoTracking();

        if (instrumentId is not null)
        {
            query = query.Where(a => a.InstrumentId == instrumentId);
        }

        var items = await query
            .OrderBy(a => a.Instrument!.Ticker)
            .ThenBy(a => a.ActionDate)
            .Select(a => new
            {
                a.Id,
                a.InstrumentId,
                Ticker = a.Instrument!.Ticker,
                a.ActionDate,
                ActionType = a.ActionType.ToString(),
                a.Ratio,
                a.AdjustmentFactor,
                a.ObservedRatio,
                a.ObservedLogReturn,
                Source = a.Source.ToString(),
                a.IsConfirmed,
                a.IsApplied,
                a.Comment
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    /// <summary>
    /// Изменяет классификацию аномалии. Применяется, если автоматическое
    /// решение признано пользователем неверным. Запись помечается как
    /// внесённая пользователем и при последующих запусках выявления
    /// не перезаписывается.
    /// </summary>
    /// <param name="id">Идентификатор записи.</param>
    /// <param name="request">Новая классификация.</param>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateCorporateActionRequest request,
        CancellationToken cancellationToken)
    {
        var action = await _db.CorporateActions
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (action is null)
        {
            return NotFound();
        }

        action.ActionType = request.ActionType;
        action.IsApplied = request.ActionType != CorporateActionType.MarketEvent && request.IsApplied;
        action.IsConfirmed = true;
        action.Source = DetectionSource.Manual;
        action.Comment = request.Comment ?? action.Comment;
        action.UpdatedAt = DateTimeOffset.UtcNow;

        if (request.Ratio is > 0m)
        {
            action.Ratio = request.Ratio.Value;
            action.AdjustmentFactor = request.ActionType switch
            {
                CorporateActionType.Split => 1m / request.Ratio.Value,
                CorporateActionType.ReverseSplit => request.Ratio.Value,
                _ => 1m
            };
        }
        else if (request.ActionType == CorporateActionType.MarketEvent)
        {
            action.AdjustmentFactor = 1m;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}

/// <summary>Запрос на изменение классификации аномалии ценового ряда.</summary>
/// <param name="ActionType">Классификация.</param>
/// <param name="IsApplied">Применять ли корректировку при построении ценового ряда.</param>
/// <param name="Ratio">Коэффициент корпоративного действия.</param>
/// <param name="Comment">Пояснение.</param>
public sealed record UpdateCorporateActionRequest(
    CorporateActionType ActionType,
    bool IsApplied,
    decimal? Ratio,
    string? Comment);
